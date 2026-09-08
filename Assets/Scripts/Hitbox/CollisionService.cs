using System.Collections.Generic;
using UnityEngine;
using ShinySTG.EnemyAI; // EnemyHealth (正交解耦,只通过 TakeDamage / Alive 接口通信)
// 注意:不要 using ShinySTG.Player,因为它的 Player 类与 namespace 同名,
//       using 后 C# 会优先把 Player 解析为 namespace,导致 Player.Instance 找不到类成员。
//       下面所有 Player / PlayerHealth 都用全限定名 ShinySTG.Player.X。

namespace ShinySTG.Hitbox
{
    /// <summary>
    /// 统一碰撞服务。每帧 LateUpdate 一次性跑完所有"子弹 vs 实体"的 AABB 判定。
    ///
    /// 架构(Grid-First,网格是默认且唯一数据源):
    ///   1. 重建网格:本帧把活跃敌人 / 子弹 / 玩家全部 RefreshCachedBounds + Insert 到 _grid。
    ///   2. 玩家弹 vs 敌人:每颗玩家弹用 _grid.Query3x3(b.Position) 拿到 3×3 cell 内全部 hitbox,
    ///      按阵营过滤敌人后做 AABB,首条命中即返回。
    ///   3. 敌人弹 vs 玩家:每颗敌人弹 Query3x3,找玩家 hitbox(单 cell 内),做命中 + 擦弹分支。
    ///
    /// 性能特性:
    ///   - 每帧清网格 + 全量重建,O(N) 插入,N=活跃实体数(通常 < 1000)。常数开销小。
    ///   - 玩家弹 vs 敌人走 3×3 查询,平均 < 10 个候选,做 < 10 次 AABB,远低于朴素 N×M。
    ///   - 敌人弹 vs 玩家走 3×3 查询,候选里只关心玩家 hitbox,通常 1 个 AABB 即可判定。
    ///   - 阵营过滤在查询内做,不依赖单独的 snapshot 列表。
    ///   - 命中走 deferred return:本帧末统一 BulletPool.Return,避免 HashSet 迭代中修改。
    ///
    /// 事件钩子(供特效/音效/计分订阅):
    ///   - OnPlayerBulletHitEnemy(Bullet, EnemyHealth)
    ///   - OnEnemyBulletHitPlayer(Bullet, PlayerHealth)
    ///   - OnPlayerGrazeByEnemyBullet(Bullet, PlayerHealth)
    ///
    /// 与既有架构的边界:
    ///   - 不读 Player / Enemy 的 HP 字段,只调 TakeDamage / TakeHit 入口。
    ///   - Boss 走 BossHealth(不在 EnemyHealth.Alive 里),本服务不处理 Boss vs 玩家弹。
    ///   - 不处理子弹 vs 子弹(STG 通常不处理)。
    ///   - 网格是默认且唯一实现,无开关。
    /// </summary>
    public class CollisionService : MonoBehaviour
    {
        public static CollisionService Instance { get; private set; }

        [Header("Grid")]
        [Tooltip("网格 cell 尺寸(世界单位)。\n" +
                 "推荐:覆盖屏幕约 1/3 宽,默认 4 适用于玩家活动区 ±10 × ±20。\n" +
                 "太小则单 cell 内碰撞对象少、查询效率差;太大则单 cell 内多对比较退化。")]
        public float CellSize = 4f;

        [Header("Behavior")]
        [Tooltip("玩家无敌帧内是否消耗敌人弹(true = 弹也会被吞掉;false = 弹继续飞行)。")]
        public bool ConsumeEnemyBulletsWhenInvincible = false;

        [Header("Graze")]
        [Tooltip("是否开启擦弹检测(true = 敌人弹擦过玩家判定外圈时触发 OnPlayerGrazeByEnemyBullet)。\n" +
                 "经典 STG 行为:擦弹仅触发事件,不吞弹、不扣血;一颗弹最多擦 1 次(走 Bullet.HasGrazed 防重)。")]
        public bool GrazeEnabled = true;

        [Tooltip("擦弹环厚度(世界单位)。玩家 hitbox 沿四边向外膨胀此距离,膨胀环与原 hitbox 之间的环形区域算擦弹。\n" +
                 "经典推荐 0.4(STG 玩家判定约 0.1,环厚度约为判定半径的 4 倍)。值越大越宽容;值 0 等同关闭擦弹。")]
        public float GrazePadding = 0.4f;

        [Tooltip("同一颗敌人弹是否只能擦玩家 1 次(true = 一颗弹擦过一次后不再触发;\n" +
                 "false = 每帧只要还在外圈内就重复触发 —— 经典 STG 推荐 true)。")]
        public bool OneGrazePerBullet = true;

        [Header("Debug")]
        [Tooltip("Scene 视图绘制空间网格格子(运行期有效)。")]
        public bool DrawGridCells = false;

        // ─── 事件 ───
        public delegate void PlayerBulletHitEnemy(Bullet bullet, EnemyHealth enemy);
        public delegate void EnemyBulletHitPlayer(Bullet bullet, ShinySTG.Player.PlayerHealth player);
        /// <summary>敌人弹擦过玩家判定外圈时触发(经典 STG 擦弹,无伤害,弹继续飞行)。</summary>
        public delegate void PlayerGrazeByEnemyBullet(Bullet bullet, ShinySTG.Player.PlayerHealth player);
        public event PlayerBulletHitEnemy OnPlayerBulletHitEnemy;
        public event EnemyBulletHitPlayer OnEnemyBulletHitPlayer;
        public event PlayerGrazeByEnemyBullet OnPlayerGrazeByEnemyBullet;

        // ─── 网格 + 缓存 ───
        UniformGrid _grid;
        /// <summary>
        /// 公开只读访问网格,供追踪 modifier 等系统按半径查询候选。
        /// 调用方应自行按 hb.Team 过滤,且查询结果 List 不可长期持有(下次查询会被 Clear)。
        /// 注意:本属性返回的是同一实例,生命周期内有效;每帧 LateUpdate 开头 Clear + 重建。
        /// </summary>
        public UniformGrid Grid => _grid;
        /// <summary>HitboxComponent.GetInstanceID() → EnemyHealth 反查表,每帧从 EnemyHealth.Alive 重建。</summary>
        readonly Dictionary<int, EnemyHealth> _enemyByHitboxID = new(64);
        readonly List<Bullet> _toReturn = new(64);

        Rect _playerCachedBounds;
        ShinySTG.Player.PlayerHitbox _playerHitbox;
        bool _playerAlive;

        void Awake()
        {
            Instance = this;
            // 网格世界范围 = 屏幕活动区(与 Bullet.Update 出界 ±10/±20 对齐)
            _grid = new UniformGrid(CellSize, new Vector2(-12f, -22f), new Vector2(12f, 22f));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            if (BulletPool.Instance == null) return;

            // 1. 清网格,准备重建
            _grid.Clear();
            _enemyByHitboxID.Clear();

            // 2. 敌人 → 网格 + ID 缓存
            var alive = EnemyHealth.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var e = alive[i];
                if (e == null || e.IsDead) continue;
                if (e.Hitbox == null) continue;
                e.Hitbox.RefreshCachedBounds();
                _grid.Insert(e.Hitbox);
                _enemyByHitboxID[e.Hitbox.GetInstanceID()] = e;
            }

            // 3. 子弹 → 网格(玩家弹 + 敌人弹都进网格,由查询侧的阵营过滤来配对)
            foreach (var b in BulletPool.Instance.ActiveBullets)
            {
                if (b == null || b.Hitbox == null) continue;
                b.Hitbox.RefreshCachedBounds();
                _grid.Insert(b.Hitbox);
            }

            // 4. 玩家 → 网格(单例,也可能没活)
            _playerAlive = false;
            var player = ShinySTG.Player.Player.Instance;
            if (player != null
                && player.Hitbox != null
                && player.Health != null
                && !player.Health.IsDead)
            {
                player.Hitbox.RefreshCachedBounds();
                _grid.Insert(player.Hitbox);
                _playerHitbox = player.Hitbox;
                _playerCachedBounds = player.Hitbox._cachedBounds;
                _playerAlive = true;
            }

            // 5. 玩家弹 vs 敌人
            TickPlayerBulletsVsEnemies();

            // 6. 敌人弹 vs 玩家(含擦弹)
            if (_playerAlive) TickEnemyBulletsVsPlayer();

            // 7. deferred return
            FlushReturns();
        }

        void FlushReturns()
        {
            if (_toReturn.Count == 0) return;
            var pool = BulletPool.Instance;
            for (int i = 0; i < _toReturn.Count; i++)
            {
                var b = _toReturn[i];
                if (b != null) pool.Return(b);
            }
            _toReturn.Clear();
        }

        // ═══════════════════════════════════════════════════════════
        // 玩家弹 vs 敌人
        // 玩家弹用 _grid.Query3x3(b.Position) 拿到 3×3 候选,过滤敌人后做 AABB。
        // ═══════════════════════════════════════════════════════════
        void TickPlayerBulletsVsEnemies()
        {
            // 注意:不要在这里 Clear _toReturn,LateUpdate 开头已经 Clear 过一次。
            // 这里只是 Append,本帧末统一 FlushReturns(避免 HashSet 迭代中调用 pool.Return)。

            foreach (var b in BulletPool.Instance.ActiveBullets)
            {
                if (b == null || b.Hitbox == null) continue;
                if (b.Hitbox.Team != CollisionTeam.Player) continue;  // 阵营过滤

                Vector2 bPos = b.Hitbox._cachedBounds.center;
                var cands = _grid.Query3x3(bPos);
                Rect bRect = b.Hitbox._cachedBounds;
                bool hit = false;

                for (int i = 0; i < cands.Count && !hit; i++)
                {
                    var hb = cands[i];
                    if (hb == null) continue;
                    if (hb.Team != CollisionTeam.Enemy) continue;     // 3×3 内可能含玩家弹/玩家本身,过滤

                    // ID 缓存反查 EnemyHealth(替代 GetComponentInParent,O(1))
                    if (!_enemyByHitboxID.TryGetValue(hb.GetInstanceID(), out var enemy)) continue;
                    if (enemy.IsDead) continue;

                    if (HitboxMath.AABBOverlap(bRect, hb._cachedBounds))
                    {
                        // 玩家弹伤害由 b.Damage 决定(由 FirePattern.Damage 经 pool.Get 写入)。
                        enemy.TakeDamage(b.Damage);
                        OnPlayerBulletHitEnemy?.Invoke(b, enemy);
                        _toReturn.Add(b);
                        hit = true;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 敌人弹 vs 玩家 + 擦弹
        // 敌人弹 Query3x3,3×3 内主要关心玩家 hitbox;
        // 命中优先于擦弹(同一颗弹本帧只可能命中或擦,不会同时)。
        // ═══════════════════════════════════════════════════════════
        void TickEnemyBulletsVsPlayer()
        {
            // 注意:不要在这里 Clear _toReturn,LateUpdate 开头已经 Clear 过一次。
            // 这里只是 Append,本帧末统一 FlushReturns。
            var player = ShinySTG.Player.Player.Instance;
            var health = player.Health;
            bool invincible = health.IsInvincible;
            bool consumeWhenInvincible = ConsumeEnemyBulletsWhenInvincible;
            bool grazeEnabled = GrazeEnabled && GrazePadding > 0f;
            Rect grazeOuter = grazeEnabled
                ? InflateRect(_playerCachedBounds, GrazePadding)
                : default;

            foreach (var b in BulletPool.Instance.ActiveBullets)
            {
                if (b == null || b.Hitbox == null) continue;
                if (b.Hitbox.Team != CollisionTeam.Enemy) continue;  // 阵营过滤

                Vector2 bPos = b.Hitbox._cachedBounds.center;
                var cands = _grid.Query3x3(bPos);
                Rect bRect = b.Hitbox._cachedBounds;
                bool processed = false;

                for (int i = 0; i < cands.Count && !processed; i++)
                {
                    var hb = cands[i];
                    if (hb == null) continue;
                    if (hb.Team != CollisionTeam.Player) continue;  // 只关心玩家

                    // 玩家 hitbox 可能就是 1 个(单例),但 Query3x3 可能因邻 cell 也有"玩家"占位
                    // — 实际只会有一个 _playerHitbox。直接比对引用最快:
                    if (hb != _playerHitbox) continue;

                    // ── 分支 1:命中(玩家 hitbox 内) ──
                    if (HitboxMath.AABBOverlap(bRect, _playerCachedBounds))
                    {
                        if (!invincible)
                        {
                            player.OnHit(1f);
                            OnEnemyBulletHitPlayer?.Invoke(b, health);
                            _toReturn.Add(b);
                        }
                        else if (consumeWhenInvincible)
                        {
                            // 无敌期擦弹:吞掉弹(后续可触发擦弹加分事件)
                            _toReturn.Add(b);
                        }
                        // 否则:无敌且不吞噬 → 弹继续飞行,下一帧自然移出玩家范围
                        processed = true;
                    }
                    // ── 分支 2:擦弹(外圈内,内圈外) ──
                    else if (grazeEnabled
                             && (!OneGrazePerBullet || !b.HasGrazed)
                             && HitboxMath.AABBOverlap(bRect, grazeOuter))
                    {
                        // 经典 STG:擦弹不吞弹、不扣血;仅触发事件供 UI/计分订阅。
                        // 一颗弹最多擦 1 次(Bullet.HasGrazed 由 Init 重置)。
                        b.HasGrazed = true;
                        OnPlayerGrazeByEnemyBullet?.Invoke(b, health);
                        // 擦弹不 _toReturn:弹继续飞行(STG 经典行为)。
                    }
                }
            }
        }

        /// <summary>
        /// 把 Rect 沿四边各向外膨胀 pad(世界单位),返回新 Rect。
        /// Unity 自带 Rect.Inflate 会改 this,所以手动算四边返回新值,零分配。
        /// </summary>
        static Rect InflateRect(Rect r, float pad)
        {
            return new Rect(r.xMin - pad, r.yMin - pad,
                            r.width  + pad * 2f,
                            r.height + pad * 2f);
        }

        void OnDrawGizmos()
        {
            if (!DrawGridCells) return;
            if (!Application.isPlaying) return;
            Vector2 worldMin = new Vector2(-12f, -22f);
            Vector2 worldMax = new Vector2( 12f,  22f);
            float cs = Mathf.Max(0.01f, CellSize);
            int cols = Mathf.CeilToInt((worldMax.x - worldMin.x) / cs);
            int rows = Mathf.CeilToInt((worldMax.y - worldMin.y) / cs);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            for (int x = 0; x <= cols; x++)
            {
                float wx = worldMin.x + x * cs;
                Gizmos.DrawLine(new Vector3(wx, worldMin.y, 0), new Vector3(wx, worldMax.y, 0));
            }
            for (int y = 0; y <= rows; y++)
            {
                float wy = worldMin.y + y * cs;
                Gizmos.DrawLine(new Vector3(worldMin.x, wy, 0), new Vector3(worldMin.x + 24f, wy, 0));
            }
        }
    }
}