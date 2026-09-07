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
    /// 性能策略(分两层,按 Inspector 切换):
    ///   Layer 1(默认开)— 阵营分桶 + 缓存:
    ///     - 子弹按 HitboxComponent.Team 分类:玩家弹 → 只撞敌人;敌人弹 → 只撞玩家。
    ///     - 每帧开头把所有参与者的 WorldBounds 一次缓存,后续判定全部走 _cachedBounds,
    ///       避免在双重循环中反复读 transform.lossyScale。
    ///     - 命中走 deferred return:本帧末统一 BulletPool.Return,避免 HashSet 迭代中修改。
    ///
    ///   Layer 2(可选)— 空间哈希:
    ///     - 活跃 Hitbox 数 > ~500 时开启。Inspector 勾 UseSpatialHash。
    ///     - CellSize 默认 4(单位世界距离)。太小则网格填不满效率差;太大则单 cell 内多对比较退化。
    ///
    /// 事件钩子(供特效/音效/计分订阅):
    ///   - OnPlayerBulletHitEnemy(Bullet, EnemyHealth)
    ///   - OnEnemyBulletHitPlayer(Bullet, PlayerHealth)
    ///
    /// 与既有架构的边界:
    ///   - 不读 Player / Enemy 的 HP 字段,只调 TakeDamage / TakeHit 入口。
    ///   - Boss 走 BossHealth(不在 EnemyHealth.Alive 里),本服务不处理 Boss vs 玩家弹。
    ///   - 不处理子弹 vs 子弹(STG 通常不处理,且 N² 浪费)。
    /// </summary>
    public class CollisionService : MonoBehaviour
    {
        public static CollisionService Instance { get; private set; }

        [Header("Performance")]
        [Tooltip("弹量较大(>500)时开启空间哈希。弹量小(<200)关闭以省常数开销。\n" +
                 "STG 符卡期间可临时开,平时关闭。")]
        public bool UseSpatialHash = false;

        [Tooltip("空间哈希 cell 尺寸(世界单位)。\n" +
                 "推荐:覆盖屏幕约 1/3 宽,默认 4 适用于玩家活动区 ±10 × ±20。")]
        public float CellSize = 4f;

        [Header("Behavior")]
        [Tooltip("玩家无敌帧内是否消耗敌人弹(true = 弹也会被吞掉;false = 弹继续飞行)。")]
        public bool ConsumeEnemyBulletsWhenInvincible = false;

        [Header("Debug")]
        [Tooltip("Scene 视图绘制空间哈希格子(仅 UseSpatialHash=true 时有效)。")]
        public bool DrawGridCells = false;

        // ─── 事件 ───
        public delegate void PlayerBulletHitEnemy(Bullet bullet, EnemyHealth enemy);
        public delegate void EnemyBulletHitPlayer(Bullet bullet, ShinySTG.Player.PlayerHealth player);
        public event PlayerBulletHitEnemy OnPlayerBulletHitEnemy;
        public event EnemyBulletHitPlayer OnEnemyBulletHitPlayer;

        // ─── 复用缓冲(零分配) ───
        readonly List<Bullet> _playerBulletSnapshot = new(512);
        readonly List<Bullet> _enemyBulletSnapshot  = new(512);
        readonly List<EnemyHealth> _enemySnapshot = new(64);
        readonly List<Bullet> _toReturn = new(64);

        UniformGrid _enemyGrid;
        UniformGrid _playerGrid;

        Rect _playerCachedBounds;
        bool _playerAlive;

        void Awake()
        {
            Instance = this;
            // 网格世界范围 = 屏幕活动区(与 Bullet.Update 出界 ±10/±20 对齐)
            _enemyGrid  = new UniformGrid(CellSize, new Vector2(-12f, -22f), new Vector2(12f, 22f));
            _playerGrid = new UniformGrid(CellSize, new Vector2(-12f, -22f), new Vector2(12f, 22f));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            if (BulletPool.Instance == null) return;

            // 1. 快照分类(零分配迭代 HashSet)
            _playerBulletSnapshot.Clear();
            _enemyBulletSnapshot.Clear();
            foreach (var b in BulletPool.Instance.ActiveBullets)
            {
                if (b == null || b.Hitbox == null) continue;
                switch (b.Hitbox.Team)
                {
                    case CollisionTeam.Player: _playerBulletSnapshot.Add(b); break;
                    case CollisionTeam.Enemy:  _enemyBulletSnapshot.Add(b);  break;
                }
            }

            // 2. 缓存所有命中目标的 bounds
            _enemySnapshot.Clear();
            var alive = EnemyHealth.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var e = alive[i];
                if (e == null || e.IsDead) continue;
                if (e.Hitbox == null) continue;
                e.Hitbox.RefreshCachedBounds();
                _enemySnapshot.Add(e);
            }

            _playerAlive = false;
            if (ShinySTG.Player.Player.Instance != null
                && ShinySTG.Player.Player.Instance.Hitbox != null
                && ShinySTG.Player.Player.Instance.Health != null
                && !ShinySTG.Player.Player.Instance.Health.IsDead)
            {
                ShinySTG.Player.Player.Instance.Hitbox.RefreshCachedBounds();
                _playerCachedBounds = ShinySTG.Player.Player.Instance.Hitbox._cachedBounds;
                _playerAlive = true;
            }

            // 3. 玩家弹 vs 敌人
            if (_enemySnapshot.Count > 0) TickPlayerBulletsVsEnemies();

            // 4. 敌人弹 vs 玩家
            if (_playerAlive && _enemyBulletSnapshot.Count > 0) TickEnemyBulletsVsPlayer();

            // 5. deferred return
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
        // ═══════════════════════════════════════════════════════════
        void TickPlayerBulletsVsEnemies()
        {
            _toReturn.Clear();
            int enemyCount = _enemySnapshot.Count;

            if (UseSpatialHash)
            {
                _enemyGrid.Clear();
                for (int i = 0; i < enemyCount; i++)
                    _enemyGrid.Insert(_enemySnapshot[i].Hitbox._cachedBounds, _enemySnapshot[i].Hitbox.GetInstanceID());

                for (int i = 0; i < _playerBulletSnapshot.Count; i++)
                {
                    var b = _playerBulletSnapshot[i];
                    if (b == null || b.Hitbox == null) continue;
                    b.Hitbox.RefreshCachedBounds();
                    Rect bRect = b.Hitbox._cachedBounds;

                    var cands = _enemyGrid.Query(bRect);
                    bool hit = false;
                    for (int c = 0; c < cands.Count && !hit; c++)
                    {
                        for (int e = 0; e < enemyCount; e++)
                        {
                            var enemy = _enemySnapshot[e];
                            if (enemy == null || enemy.IsDead || enemy.Hitbox == null) continue;
                            if (enemy.Hitbox.GetInstanceID() != cands[c]) continue;
                            if (HitboxMath.AABBOverlap(bRect, enemy.Hitbox._cachedBounds))
                            {
                                // 玩家弹伤害由 b.Damage 决定(由 FirePattern.Damage 经 pool.Get 写入)。
                                // 敌人弹不参与此分支(它的阵营是 Enemy,会被 _playerBulletSnapshot 跳过)。
                                enemy.TakeDamage(b.Damage);
                                OnPlayerBulletHitEnemy?.Invoke(b, enemy);
                                _toReturn.Add(b);
                                hit = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // 朴素 N×M。STG 弹量中等时(<500 弹 × 30 敌 ≈ 15k)每帧 < 1ms。
                for (int i = 0; i < _playerBulletSnapshot.Count; i++)
                {
                    var b = _playerBulletSnapshot[i];
                    if (b == null || b.Hitbox == null) continue;
                    b.Hitbox.RefreshCachedBounds();
                    Rect bRect = b.Hitbox._cachedBounds;

                    for (int e = 0; e < enemyCount; e++)
                    {
                        var enemy = _enemySnapshot[e];
                        if (enemy == null || enemy.IsDead || enemy.Hitbox == null) continue;
                        if (HitboxMath.AABBOverlap(bRect, enemy.Hitbox._cachedBounds))
                        {
                            // 玩家弹伤害由 b.Damage 决定(由 FirePattern.Damage 经 pool.Get 写入)。
                            enemy.TakeDamage(b.Damage);
                            OnPlayerBulletHitEnemy?.Invoke(b, enemy);
                            _toReturn.Add(b);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < _toReturn.Count; i++)
            {
                var b = _toReturn[i];
                if (b != null) BulletPool.Instance.Return(b);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 敌人弹 vs 玩家
        // ═══════════════════════════════════════════════════════════
        void TickEnemyBulletsVsPlayer()
        {
            _toReturn.Clear();
            var player = ShinySTG.Player.Player.Instance;
            var health = player.Health;
            bool invincible = health.IsInvincible;
            bool consumeWhenInvincible = ConsumeEnemyBulletsWhenInvincible;

            for (int i = 0; i < _enemyBulletSnapshot.Count; i++)
            {
                var b = _enemyBulletSnapshot[i];
                if (b == null || b.Hitbox == null) continue;
                b.Hitbox.RefreshCachedBounds();
                Rect bRect = b.Hitbox._cachedBounds;

                if (!HitboxMath.AABBOverlap(bRect, _playerCachedBounds)) continue;

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
            }

            for (int i = 0; i < _toReturn.Count; i++)
            {
                var b = _toReturn[i];
                if (b != null) BulletPool.Instance.Return(b);
            }
        }

        void OnDrawGizmos()
        {
            if (!DrawGridCells || !UseSpatialHash) return;
            if (!Application.isPlaying) return;
            Vector2 worldMin = new Vector2(-12f, -22f);
            Vector2 worldMax = new Vector2( 12f,  22f);
            int cols = Mathf.CeilToInt((worldMax.x - worldMin.x) / CellSize);
            int rows = Mathf.CeilToInt((worldMax.y - worldMin.y) / CellSize);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            for (int x = 0; x <= cols; x++)
            {
                float wx = worldMin.x + x * CellSize;
                Gizmos.DrawLine(new Vector3(wx, worldMin.y, 0), new Vector3(wx, worldMax.y, 0));
            }
            for (int y = 0; y <= rows; y++)
            {
                float wy = worldMin.y + y * CellSize;
                Gizmos.DrawLine(new Vector3(worldMin.x, wy, 0), new Vector3(worldMax.x, wy, 0));
            }
        }
    }
}