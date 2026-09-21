using System.Collections.Generic;
using UnityEngine;
using ShinySTG.Hitbox;
using ShinySTG.Player;
using ShinySTG.Stage;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 激光全局服务。每帧 LateUpdate 跑"玩家 vs 激光"碰撞 + 出界回收。
    ///
    /// ★ 不在 CollisionService 里加分支,因为:
    ///   - AABB 不适合激光(参考材料 §5,激光长跨整个屏幕)。
    ///   - UniformGrid 失真(激光跨 N cell,要么插不进要么浪费)。
    ///   - 中心化服务避免给 CollisionService 加大量特例判断。
    ///
    /// 阵营过滤:复用 CollisionTeam(Enemy 阵营激光撞 Player 阵营玩家)。
    /// 擦弹:外圈检测，每条激光独立按时间间隔计数。
    /// </summary>
    public class LaserService : MonoBehaviour
    {
        public static LaserService Instance { get; private set; }

        [Header("Player")]
        [Tooltip("玩家 Hitbox 引用，可手动指定；为空时自动获取当前玩家，支持开局后动态创建。")]
        public HitboxComponent PlayerHitbox;

        [Tooltip("玩家判定半径(世界单位)。激光走点-线段距离,需要半径。\n" +
                 "经典 STG:0.1(对应玩家 hitbox Size=0.1 的 AABB 半宽)。")]
        public float PlayerRadius = 0.1f;

        [Header("Behavior")]
        [Tooltip("玩家无敌帧内是否消耗激光(true = 激光撞到无敌玩家也算命中但只回收不扣血)。\n" +
                 "对齐 CollisionService.ConsumeEnemyBulletsWhenInvincible。")]
        public bool ConsumeLasersWhenInvincible = false;

        [Header("Graze")]
        [Tooltip("是否开启擦弹检测(true = 激光擦过判定外圈时触发 OnPlayerGrazedByLaser)。")]
        public bool GrazeEnabled = true;

        [Tooltip("擦弹环厚度(世界单位)。\n" +
                 "经典推荐 0.4(STG 玩家判定约 0.1,环厚度约为判定半径的 4 倍)。")]
        public float GrazePadding = 0.4f;

        [Min(0.01f), Tooltip("同一条激光重复擦弹的最短间隔（游戏秒）；首次立即计数，离开范围不重置冷却。")]
        public float GrazeInterval = 0.3f;

        [Header("Culling")]
        [Tooltip("激光出界(超出 BoundsService.CullingArea)是否提前回收。\n" +
                 "默认 true —— 经典 STG 行为:激光飞出舞台立刻消失,避免长期占用池。")]
        public bool CullOutOfBounds = true;

        // ─── 事件(对齐 CollisionService 的事件签名风格) ───
        public delegate void PlayerHitByLaser(LaserEntity laser, PlayerHealth player);
        public delegate void PlayerGrazedByLaser(LaserEntity laser, PlayerHealth player);
        public event PlayerHitByLaser   OnPlayerHitByLaser;
        public event PlayerGrazedByLaser OnPlayerGrazedByLaser;

        LaserPool _pool;
        // 临时回收列表:避免在 foreach 迭代 _pool.ActiveLasers 时修改集合
        readonly List<LaserEntity> _toReturn = new(4);
        // ★ 本帧已对玩家扣血的激光集合(帧内去重,防止同一条激光在一帧内多次命中反复扣血)。
        //   每帧 LateUpdate 开头 Clear。下帧重新累计 —— 玩家若持续站在激光上,每帧都掉 1 次血。
        readonly HashSet<LaserEntity> _hitThisFrame = new(8);

        void Awake()
        {
            Instance = this;
            _pool = LaserPool.Instance;
            // 自动抓玩家 Hitbox(若 Player 已 Awake)
            if (PlayerHitbox == null && ShinySTG.Player.Player.Instance != null)
            {
                PlayerHitbox = ShinySTG.Player.Player.Instance.Hitbox;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 玩家总控可调本方法更新 PlayerHitbox / PlayerRadius。
        /// </summary>
        public void RegisterPlayer(HitboxComponent playerHitbox, float playerRadius)
        {
            PlayerHitbox = playerHitbox;
            PlayerRadius = playerRadius;
        }

        void LateUpdate()
        {
            if (ShinySTG.GameFlow.GameplayPause.IsPaused) return;

            // 玩家可能在 Awake 之后创建；池也可能晚于服务初始化或在场景切换时重建。
            if (_pool == null) _pool = LaserPool.Instance;
            // ★ 使用全限定名避免与 namespace ShinySTG.Player.Player 类同名陷阱(CONTRIBUTING §4.7)
            // ★ PlayerHealth 没有静态 Instance —— 它是 [RequireComponent] 挂在 Player 总控上的子组件,
            //   正确访问路径:ShinySTG.Player.Player.Instance?.Health(对齐 CollisionService.cs 的 _playerHitbox 抓取方式)。
            var playerObj = ShinySTG.Player.Player.Instance;
            if (PlayerHitbox == null && playerObj != null) PlayerHitbox = playerObj.Hitbox;
            if (PlayerHitbox == null || _pool == null) return;
            var player = playerObj != null ? playerObj.Health : null;
            if (player == null) return;

            Vector2 pPos = PlayerHitbox.Position;
            bool invincible = player.IsInvincible;
            _toReturn.Clear();
            _hitThisFrame.Clear(); // 每帧重置扣血记录,持续站激光 = 每帧扣 1 次

            var culling = BoundsService.Instance;
            float grazePad = GrazePadding;

            foreach (var laser in _pool.ActiveLasers)
            {
                if (laser == null) continue;
                if (laser.State == LaserState.Dead) continue;
                if (laser.Team != CollisionTeam.Enemy) continue; // 阵营过滤:只关心敌人激光

                // ── 出界提前回收 ──
                if (CullOutOfBounds && culling != null && !culling.ContainsCulling(laser.Position))
                {
                    _toReturn.Add(laser);
                    continue;
                }

                if (!player.CanInteract) continue;
                invincible = player.IsInvincible;

                // ── 命中检测 ──
                bool hit = laser.CheckStraightHit(pPos, PlayerRadius);

                if (hit)
                {
                    if (!invincible)
                    {
                        // ★ 帧内去重:同一条激光一帧内只扣 1 次血(防止位置抖动 / 多次 LateUpdate 重复扣血)。
                        //   玩家若持续站在激光上 → 下一帧 _hitThisFrame.Clear 后又可重新计入 → 每帧扣 1 次血。
                        if (_hitThisFrame.Add(laser))
                        {
                            // 经典 STG:激光一律 1 击。ShinySTG 现有子弹也是 1 击(player.OnHit(1f))
                            ShinySTG.Player.Player.Instance.OnHit(1f);
                            OnPlayerHitByLaser?.Invoke(laser, player);
                        }
                        // ★ 激光撞到玩家不立刻回收(对齐东方正作:激光是持续判定,走完五段状态机自然消亡)。
                        //   出界回收 / 状态机 Shrinking→Dead 回收 仍然正常生效。
                    }
                    else if (ConsumeLasersWhenInvincible)
                    {
                        // ★ 无敌期不再吞噬激光。ConsumeLasersWhenInvincible 字段保留以兼容旧资产,
                        //   但当前实现下此分支 no-op —— 激光继续走完生命周期,与有无敌开关无关。
                        //   玩家无敌期间不会扣血,激光也不会因"无敌碰撞"被提前回收。
                    }
                    // 否则:无敌且不吞噬 → 激光继续飞行,与新行为一致
                    continue;
                }

                // 只奖励可受伤时贴近有效判定的行为；一次检测最多计数一次，不补发历史次数。
                if (GrazeEnabled && grazePad > 0f && laser.CollisionEnabled && !invincible
                    && !ShinySTG.Level.BattleRestriction.IsActive
                    && (!laser._hasGrazed || laser.Timer >= laser.NextGrazeTime))
                {
                    float r = PlayerRadius + laser.CollisionWidth + grazePad;
                    bool grazed = CheckLaserRingOverlap(laser, pPos, r);
                    if (grazed)
                    {
                        laser._hasGrazed = true;
                        laser.NextGrazeTime = laser.Timer + Mathf.Max(0.01f, GrazeInterval);
                        player.RecordGraze(pPos);
                        OnPlayerGrazedByLaser?.Invoke(laser, player);
                        // 擦弹不回收,激光继续飞行(经典 STG 行为,与 CollisionService 一致)
                    }
                }
            }

            // 批量回收(避免在 foreach 中修改 _active)
            for (int i = 0; i < _toReturn.Count; i++) _pool.Return(_toReturn[i]);
        }

        /// <summary>
        /// 检查玩家位置到激光线段/曲线的距离是否小于 maxRadius。
        /// 直线 / 曲线统一入口,内部走 LaserGeometry。
        /// </summary>
        static bool CheckLaserRingOverlap(LaserEntity laser, Vector2 playerPos, float maxRadius)
        {
            if (laser.CurveNodes != null && laser.CurveNodes.Length >= 2)
            {
                return LaserGeometry.CheckCurvedGraze(playerPos, laser.CurveNodes, maxRadius);
            }
            // 直线:CurrentLength 可能为 0(Warning / 已结束期),此时端点 = 起点,距离平方退化正确
            Vector2 end = laser.EndPoint;
            return LaserGeometry.DistanceSqPointToSegment(playerPos, laser.Position, end) < maxRadius * maxRadius;
        }
    }
}
