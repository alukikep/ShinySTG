using System.Collections.Generic;
using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 单条激光的运行时实体。对齐 Bullet 的角色,但走独立架构:
    ///   - 不挂 HitboxComponent(激光不参与 AABB 网格碰撞,见参考材料 §5)。
    ///   - 阵营透传由 Team 字段承担,被 LaserService 中心化碰撞时读取。
    ///   - 视觉由 <see cref="LaserRendererBase"/> 子组件负责(本类只推数据,不画图)。
    ///
    /// 生命周期(参考材料 §18):Warning → Expanding → Active → Shrinking → Dead。
    ///   Warning   :长度 = 0,CollisionEnabled 按 Data.CollideInWarning(默认 false)
    ///   Expanding :长度 0→TargetLength 插值(Linear),碰撞按 Data.CollideInExpanding
    ///   Active    :长度 = TargetLength,碰撞必开
    ///   Shrinking :长度 TargetLength→0 插值,碰撞按 Data.CollideInShrinking
    ///   Dead      :回收(由 SourcePool.Return 处理)
    /// </summary>
    [DisallowMultipleComponent]
    public class LaserEntity : MonoBehaviour
    {
        // ─── 几何与运动(运行时只读,Init 写入) ───
        public Vector2 Position { get; private set; }       // 起点(世界坐标)
        public float   Angle    { get; private set; }       // 弧度
        public float   TargetLength  { get; private set; }  // 最大长度
        public float   CurrentLength { get; private set; }  // 当前长度(随状态机变化)
        public Vector2 Velocity;                            // 位置速度(单位/秒)
        public float   AngularVelocity;                     // 角速度(弧度/秒)

        public float WarningTime;
        public float ExpandTime;
        public float ActiveTime;
        public float ShrinkTime;
        public float Timer;                                 // 累计秒(Init 后从 0 起)

        // ─── 视觉/判定分离(参考材料 §6) ───
        public float VisualWidth;
        public float CollisionWidth;

        // ─── 阵营(由 ownerHitbox 在 Init 时透传,供 LaserService 过滤) ───
        public CollisionTeam Team = CollisionTeam.Neutral;

        // ─── 状态机 ───
        public LaserState State { get; private set; } = LaserState.Warning;
        public bool CollisionEnabled { get; private set; }   // 由状态机根据 Data 字段写入

        // ─── 曲线激光节点(直线模式 = null) ───
        public Vector2[] CurveNodes;       // 曲线模式:当前世界坐标节点
        public Vector2[] BaseCurveNodes;   // 曲线模式:相对起点的基准偏移(每帧用 Position 重算世界节点)

        // ─── 数据源 ───
        public LaserData Data;             // 渲染参数/默认生命周期/默认宽度

        // ─── 修饰器状态 ───
        [HideInInspector] public bool _hasGrazed;  // 本条激光是否已对玩家触发过擦弹(防多次)

        // ─── 池引用 ───
        public LaserPool SourcePool;

        // ─── 修饰器列表(与 BulletModifier 对仗,纯 C# [SerializeReference]) ───
        readonly List<LaserModifier> _modifiers = new();
        public IReadOnlyList<LaserModifier> Modifiers => _modifiers;

        // ─── 渲染组件(子类挂 SpriteRenderer / LineRenderer / Mesh) ───
        public LaserRendererBase Renderer;

        // ★ 长度插值曲线(默认 Linear)。可在 LaserModifier 里动态切换为 EaseIn / EaseOut / 波形。 ★
        public System.Func<float, float> LengthEase = LinearEase;
        static float LinearEase(float t) => t;

        // ═══════════════════════════════════════════════════════════
        // Init:由 LaserPool.Get / GetCurve 在出栈时调用
        // ═══════════════════════════════════════════════════════════
        public void Init(LaserData data, Vector2 position, float angleRad, float targetLength,
                         HitboxComponent ownerHitbox)
        {
            Data = data;
            Position = position;
            Angle = angleRad;
            TargetLength = targetLength;
            CurrentLength = 0f; // Warning 期长度 = 0,Expanding 起才开始增长

            Velocity = Vector2.zero;
            AngularVelocity = 0f;
            Timer = 0f;
            CurveNodes = null;
            BaseCurveNodes = null;
            _hasGrazed = false;

            VisualWidth     = data != null ? data.VisualWidth     : 0.8f;
            CollisionWidth  = data != null ? data.CollisionWidth  : 0.15f;
            WarningTime     = data != null ? data.WarningTime     : 0.6f;
            ExpandTime      = data != null ? data.ExpandTime      : 0.3f;
            ActiveTime      = data != null ? data.ActiveTime      : 1.5f;
            ShrinkTime      = data != null ? data.ShrinkTime      : 0.3f;
            Team = ownerHitbox != null ? ownerHitbox.Team : CollisionTeam.Neutral;

            State = LaserState.Warning;
            UpdateCollisionEnabled();

            // 同步 transform(首次,后续 LateUpdate 持续同步)
            transform.position = Position;
            transform.rotation = Quaternion.Euler(0, 0, Angle * Mathf.Rad2Deg);

            // 抓 Renderer 子组件(若 prefab 上没挂,SpriteStretchLaserRenderer 会兜底)
            if (Renderer == null) Renderer = GetComponent<LaserRendererBase>();
            Renderer?.OnLaserInit(this);
        }

        /// <summary>
        /// 曲线激光专用 Init(在 Init 基础上挂节点)。
        /// </summary>
        public void InitCurve(LaserData data, Vector2 position, float angleRad,
                              Vector2[] curveNodes, HitboxComponent ownerHitbox)
        {
            Init(data, position, angleRad, 0f, ownerHitbox);
            if (curveNodes == null || curveNodes.Length < 2) return;
            CurveNodes = (Vector2[])curveNodes.Clone();
            int n = curveNodes.Length;
            BaseCurveNodes = new Vector2[n];
            for (int i = 0; i < n; i++) BaseCurveNodes[i] = curveNodes[i] - position;
        }

        // ═══════════════════════════════════════════════════════════
        // 主循环(LateUpdate,确保在所有 EnemyAction / Position 更新之后跑)
        // 对齐参考材料 §14 推荐更新流程
        // ═══════════════════════════════════════════════════════════
        void LateUpdate()
        {
            float dt = Time.deltaTime;

            // ── 1. 位置/角度更新(参考材料 §8) ──
            Position += Velocity * dt;
            Angle    += AngularVelocity * dt;
            transform.position = Position;
            transform.rotation = Quaternion.Euler(0, 0, Angle * Mathf.Rad2Deg);

            // 曲线节点跟随 position 移动(基准 + position delta)
            if (BaseCurveNodes != null && CurveNodes != null)
            {
                for (int i = 0; i < CurveNodes.Length; i++)
                    CurveNodes[i] = BaseCurveNodes[i] + Position;
            }

            // ── 2. 修饰器 Tick ──
            for (int i = 0; i < _modifiers.Count; i++) _modifiers[i].OnTick(this, dt);

            // ── 3. 状态机推进 ──
            Timer += dt;
            AdvanceState();

            // ── 4. 更新当前 length(随状态机 + LengthEase) ──
            UpdateCurrentLength();

            // ── 5. 渲染刷新 ──
            Renderer?.OnLaserTick(this);

            // ── 6. 死亡回收 ──
            if (State == LaserState.Dead)
            {
                SourcePool?.Return(this);
            }
        }

        void AdvanceState()
        {
            // 状态转移(参考材料 §18):
            //   Warning   → timer >= warningTime → Expanding
            //   Expanding → timer >= warningTime + expandTime → Active
            //   Active    → timer >= warningTime + expandTime + activeTime → Shrinking
            //   Shrinking → timer >= totalTime → Dead
            float t1 = WarningTime;
            float t2 = t1 + ExpandTime;
            float t3 = t2 + ActiveTime;
            float t4 = t3 + ShrinkTime;
            if      (State == LaserState.Warning   && Timer >= t1) State = LaserState.Expanding;
            else if (State == LaserState.Expanding && Timer >= t2) State = LaserState.Active;
            else if (State == LaserState.Active    && Timer >= t3) State = LaserState.Shrinking;
            else if (State == LaserState.Shrinking && Timer >= t4) State = LaserState.Dead;
            UpdateCollisionEnabled();
        }

        void UpdateCollisionEnabled()
        {
            if (Data == null) { CollisionEnabled = false; return; }
            switch (State)
            {
                case LaserState.Warning:   CollisionEnabled = Data.CollideInWarning;   break;
                case LaserState.Expanding: CollisionEnabled = Data.CollideInExpanding; break;
                case LaserState.Active:    CollisionEnabled = true;                    break;
                case LaserState.Shrinking: CollisionEnabled = Data.CollideInShrinking; break;
                default:                   CollisionEnabled = false;                   break;
            }
        }

        void UpdateCurrentLength()
        {
            float raw;
            switch (State)
            {
                case LaserState.Warning:   raw = 0f; break;
                case LaserState.Expanding:
                    raw = Mathf.Clamp01((Timer - WarningTime) / Mathf.Max(ExpandTime, 0.0001f));
                    raw = LengthEase != null ? LengthEase(raw) : raw;
                    break;
                case LaserState.Active:    raw = 1f; break;
                case LaserState.Shrinking:
                    raw = 1f - Mathf.Clamp01((Timer - (WarningTime + ExpandTime + ActiveTime))
                                              / Mathf.Max(ShrinkTime, 0.0001f));
                    raw = LengthEase != null ? LengthEase(raw) : raw;
                    break;
                default:                   raw = 0f; break;
            }
            CurrentLength = TargetLength * raw;
        }

        // ═══════════════════════════════════════════════════════════
        // 碰撞查询(供 LaserService 中心化调用)
        // ★ 直线模式用 EndPoint;曲线模式遍历节点数组 ★
        // ═══════════════════════════════════════════════════════════

        /// <summary>当前直线激光的有效端点(从 Position + Angle + CurrentLength 算)。曲线模式不应调。</summary>
        public Vector2 EndPoint => LaserGeometry.EndPoint(Position, Angle, CurrentLength);

        /// <summary>
        /// 玩家碰撞检测:返回 player 到当前有效线段(或曲线段)的距离平方 &lt; (playerR + collisionR)^2。
        /// 状态机未开启碰撞时返 false;曲线模式走节点数组。
        /// </summary>
        public bool CheckStraightHit(Vector2 playerPos, float playerRadius)
        {
            if (!CollisionEnabled) return false;
            if (CurveNodes != null && CurveNodes.Length >= 2)
            {
                return LaserGeometry.CheckCurvedHit(playerPos, CurveNodes, CollisionWidth, playerRadius);
            }
            float r = playerRadius + CollisionWidth;
            return LaserGeometry.DistanceSqPointToSegment(playerPos, Position, EndPoint) < r * r;
        }

        // ─── 修饰器 API(对齐 Bullet.AddModifier / ClearModifiers) ───
        public void AddModifier(LaserModifier m) { if (m != null) _modifiers.Add(m); }
        public void ClearModifiers()
        {
            for (int i = 0; i < _modifiers.Count; i++) _modifiers[i]?.OnDetach(this);
            _modifiers.Clear();
        }
        public void ResetAllModifierWindows()
        {
            for (int i = 0; i < _modifiers.Count; i++) _modifiers[i]?.ResetWindow();
        }

        void OnDisable()
        {
            // 被回收 / 场景卸载时的清理钩子
            ClearModifiers();
        }
    }
}
