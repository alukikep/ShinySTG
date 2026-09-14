using System;
using SerializeReferenceEditor;
using UnityEngine;
using Random = System.Random;  // 消歧(System 与 UnityEngine 都暴露 Random)

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 区域内随机直线移动(醉汉走路 STG 风格化版)。
    ///
    /// 行为概述:
    ///   - 进入时锁定一个矩形区域(RegionCenter + RegionSize),敌人永远不出框;
    ///   - 每次随机选一个方向 + 一个 <= MaxStepDistance 的随机距离,直线走过去;
    ///   - 到达终点后停顿 Interval ∈ [IntervalMin, IntervalMax] 秒,再选下一次;
    ///   - 循环往复,直到外层 MoveAction.Duration 到期切到下一条 Action。
    ///
    /// 速度曲线(Mode):
    ///   - Constant   : 全程匀速(PeakSpeed)
    ///   - FastToSlow : 起步满速,越走越慢,到终点时为 0(撞墙感,经典 STG 自爆冲锋的'卸力'版)
    ///   - SlowToFast : 起步 0 速,越走越快,到终点时满速(蓄力冲刺,适合 Boss 入场后突然冲刺)
    /// 速度曲线公式(光滑且始终 >= 0,无需 MinSpeedFactor 兜底):
    ///   Constant   -> 1
    ///   FastToSlow -> cos(t · π/2)  单调递减,t=0 -> 1, t=1 -> 0
    ///   SlowToFast -> sin(t · π/2)  单调递增,t=0 -> 0, t=1 -> 1
    ///
    /// 方向裁剪(关键:敌人永远不会跑出矩形):
    ///   1. 在 [DirectionCenterDeg ± DirectionSpreadDeg/2] 区间内均匀随机 angle
    ///   2. 算"从这个起点朝这个方向最多能走多远才不越出矩形":
    ///   maxDist = min(到左/右边界的距离, 到上/下边界的距离)
    ///   3. effectiveMax = min(MaxStepDistance, maxDist)   裁剪!
    ///   4. 实际单次距离 ∈ [ArrivalThreshold, effectiveMax] 随机
    ///   5. 极端兜底:若 effectiveMax < ArrivalThreshold(已在边界上),把方向镜像反转
    ///
    /// SnapOnEnter 选择 false(增量型):
    ///   第一帧 dt=0 -> step=0,enemy.position 不变 -> 零瞬移自动满足。
    ///   与 LinearMove / AccelerateMove / SineMove 同款。
    ///
    /// 与外层 MoveAction.Duration 的关系:
    ///   本类没有"自身 Duration",无限循环走,直到外层 MoveAction.Duration 到期切走。
    ///   估算:本类自然循环 1 步 ≈ 距离/平均速度 + 间隔。
    ///   例:PeakSpeed=3、EffectiveMaxDist=1.5、IntervalMax=0.8
    ///     -> 单次移动 ≈ 0.5s(匀速) + 间隔 0.3~0.8s ≈ 每步 0.8~1.3s
    ///   实用配法:外层 Duration 设为单步时长的整数倍,让最后停在 Idle 阶段。
    ///
    /// 不旋转 enemy.transform —— STG 视觉惯例(与 HomingMove / EaseMove / SineMove 一致)。
    ///
    /// 协作边界:
    ///   - 与 MoveAction: 0 改动,通过 [SRName] 自动出现在 MoveAction.Move 下拉。
    ///   - 与 BehaviorFlow: 纯值类型 + 运行时状态字段,MemberwiseClone 默认行为够用。
    ///   - 与 ParallelAction: 正常工作,可在 Parallel 里并行 + 同时开火。
    /// </summary>

    [Serializable, SRName("Move/Random Walk In Region")]
    public class RandomWalkInRegionMove : MoveBehaviour
    {
        public enum SpeedCurve
        {
            Constant,    // 匀速(默认)
            FastToSlow,  // 起步快后续减速
            SlowToFast   // 起步慢后续加速
        }

        // 子状态机阶段(运行时内部用)
        enum Phase { Moving, Idle }

        [Header("Region(矩形活动区域,绝对世界坐标)")]
        [Tooltip("区域中心。敌人永远不会跑出 (Center - Size/2) ~ (Center + Size/2) 这个矩形。\n" +
                 "经典配法:屏幕中央敌人设 (0, 0);上半场中 Boss 设 (0, 1);下半场小怪设 (0, -2)。")]
        public Vector2 RegionCenter = Vector2.zero;

        [Tooltip("区域尺寸(Width = X 方向长度, Height = Y 方向长度)。< 0.001 时兜底为 0.001。\n" +
                 "经典配法:Boss 半场 (6, 4);小怪巡逻区 (3, 2)。")]
        public Vector2 RegionSize = new Vector2(6f, 4f);

        [Header("Step(单次直线移动)")]
        [Tooltip("单次直线移动的距离上限(单位)。实际距离 ∈ [ArrivalThreshold, min(MaxStepDistance, 到边界最大距离)] 随机。\n" +
                 "若选定的方向 + MaxStepDistance 会越界,自动按到边界的最大距离裁剪。\n" +
                 "经典配法:Boss 走大步 1.5~2;小怪碎步 0.5~1。")]
        public float MaxStepDistance = 1.5f;

        [Tooltip("方向区间中心(度,0=右,90=上,180=左,270=下,与 BaseAngleFireExtension 一致)。\n" +
                 "Center=270 + Spread=120 -> 方向 ∈ [210, 330],典型 STG 上半场 Boss 朝下飘移。\n" +
                 "Center=0   + Spread=360 -> 全方向随机(默认)。")]
        public float DirectionCenterDeg = 0f;

        [Tooltip("方向区间角度宽度(度)。0 = 固定走 DirectionCenterDeg 一个方向;360 = 全方向均匀。\n" +
                 "<0 或 >360 会被 Clamp 到 [0, 360]。")]
        public float DirectionSpreadDeg = 360f;

        [Header("Interval(两次移动之间的停顿)")]
        [Tooltip("最小间隔(秒)。0 = 允许背靠背(到达立即选下一次)。")]
        public float IntervalMin = 0.3f;

        [Tooltip("最大间隔(秒)。实际间隔 ∈ [IntervalMin, IntervalMax] 随机。\n" +
                 "< IntervalMin 时自动 Clamp 到 IntervalMin(避免配置错误)。\n" +
                 "经典配法:Boss 慢节奏 (0.5, 1.0);小怪紧凑 (0.1, 0.3)。")]
        public float IntervalMax = 0.8f;

        [Header("Speed(单次移动内部的速度曲线)")]
        [Tooltip("Constant(默认,匀速)/ FastToSlow(起步快后续减速,撞墙感)/ SlowToFast(起步慢后续加速,蓄力冲刺)。\n" +
                 "三条曲线都光滑、始终 >= 0,无需 MinSpeedFactor 兜底。")]
        public SpeedCurve Mode = SpeedCurve.Constant;

        [Tooltip("峰值速度(单位/秒)。实际瞬时速度 = PeakSpeed · CurveFactor(Mode, t01),t01 = 已用/总时长。\n" +
                 "经典配法:Boss 3;小怪 2;蓄力冲刺 5。")]
        public float PeakSpeed = 3f;

        [Header("Termination")]
        [Tooltip("到达判定阈值(单位)。距离终点 < 此值视为到达,进入 Idle。< 0 时兜底为 0.05。")]
        public float ArrivedThreshold = 0.05f;

        [Tooltip("随机种子偏移(整数)。同一资产下生成的多个敌人,加不同 Offset 可让它们走不同路径(避免完全同步)。\n" +
                 "0 = 用系统时间作种子(每次不同);非 0 = 固定种子 + Offset(可重现)。")]
        public int RandomSeedOffset = 0;

        // ───────────── 运行时状态 ─────────────
        // (BehaviorFlow.Instantiate 用 Object.Instantiate(SO) 深拷贝,纯值类型 + System.Random 无引用共享,per-instance 安全)
        Phase   _phase;             // Moving / Idle
        Vector2 _moveDir;           // 本次移动的方向(归一化)
        float   _moveDist;          // 本次移动的总距离
        Vector2 _moveStart;         // 本次移动的起点
        float   _moveDuration;      // 本次移动预计总时长(秒;用于归一化 t01)
        float   _moveT;             // 本次移动已用秒数
        float   _idleDuration;      // 本次间隔总时长
        float   _idleT;             // 本次间隔已用秒数
        Random  _rng;               // per-instance 随机数发生器

        // 增量型:dt=0 时 step=0,无需 SnapOnEnter,零瞬移自动满足。
        public override bool SnapOnEnter => false;

        public override void OnEnter(Transform enemy)
        {
            // 种子:固定偏移 -> 可重现;否则 -> 用系统时间(每个 instance 独立)
            if (RandomSeedOffset != 0)
                _rng = new Random(RandomSeedOffset);
            else
                _rng = new Random(Environment.TickCount ^ (enemy != null ? enemy.GetInstanceID() : 0));

            _phase        = Phase.Moving;
            _moveT        = 0f;
            _idleT        = 0f;
            _moveDir      = Vector2.zero;
            _moveDist     = 0f;
            _moveStart    = enemy != null ? (Vector2)enemy.position : Vector2.zero;
            _moveDuration = 0.0001f;
            _idleDuration = 0f;

            // 立即算下一次移动(第一帧就走)
            PickNextMove(enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (enemy == null || dt <= 0f) return;

            if (_phase == Phase.Moving)
            {
                _moveT += dt;
                float t01 = _moveDuration > 0.0001f ? Mathf.Clamp01(_moveT / _moveDuration) : 1f;
                float speedNow = Mathf.Max(0f, PeakSpeed) * CurveFactor(Mode, t01);

                Vector2 pos = (Vector2)enemy.position + _moveDir * speedNow * dt;
                enemy.position = pos;

                // 到达判定
                Vector2 remaining = (_moveStart + _moveDir * _moveDist) - pos;
                float remainDist = remaining.magnitude;
                float thr = ArrivedThreshold < 0f ? 0.05f : ArrivedThreshold;

                // 两种触发条件:① 距离终点够近;② 已走到 100% 但曲线仍在驱动(理论兜底)
                if (remainDist <= thr || t01 >= 1f)
                {
                    // 强制对齐到终点(避免最后几帧在曲线减速尾巴上没到位)
                    enemy.position = _moveStart + _moveDir * _moveDist;

                    // 进入 Idle,Pick 间隔时长
                    _phase = Phase.Idle;
                    float minI = Mathf.Max(0f, IntervalMin);
                    float maxI = Mathf.Max(minI, IntervalMax);
                    _idleDuration = Mathf.Lerp(minI, maxI, (float)_rng.NextDouble());
                    _idleT = 0f;
                }
            }
            else // Phase.Idle
            {
                _idleT += dt;
                if (_idleT >= _idleDuration)
                {
                    PickNextMove(enemy);
                }
            }
        }

        public override void OnExit(Transform enemy)
        {
            // 清运行时状态,下次 OnEnter 时重建(per-instance 安全)
            _phase = Phase.Moving;
            _moveT = 0f;
            _idleT = 0f;
            _moveDir = Vector2.zero;
            _moveDist = 0f;
            _moveStart = Vector2.zero;
            _moveDuration = 0.0001f;
            _idleDuration = 0f;
            _rng = null;
        }

        // ───────────── 私有 helper ─────────────

        /// <summary>
        /// 选下一次移动:随机方向 → 算最大不越界距离 → 裁剪 → 在 [阈值, effectiveMax] 随机距离 → 估算时长。
        /// </summary>
        void PickNextMove(Transform enemy)
        {
            Vector2 start = enemy != null ? (Vector2)enemy.position : _moveStart;
            _moveStart = start;
            _moveT = 0f;
            _phase = Phase.Moving;

            // 矩形 AABB(尺寸兜底避免 0)
            Vector2 half = RegionSize * 0.5f;
            if (half.x < 0.0005f) half.x = 0.0005f;
            if (half.y < 0.0005f) half.y = 0.0005f;
            Vector2 regionMin = RegionCenter - half;
            Vector2 regionMax = RegionCenter + half;

            // 1. 随机方向(区间 [Center - Spread/2, Center + Spread/2])
            float spread = Mathf.Clamp(DirectionSpreadDeg, 0f, 360f);
            float center = DirectionCenterDeg;
            float minA = center - spread * 0.5f;
            float maxA = center + spread * 0.5f;
            // 边界:spread=0 时直接用 center(避免 NextDouble 在等值时浪费)
            float angleDeg = spread <= 0.0001f ? center : Mathf.Lerp(minA, maxA, (float)_rng.NextDouble());
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            // 2. 算最大不越界距离(到任一边界前能走的最大距离)
            float maxDist = MaxDistanceInsideBox(start, dir, regionMin, regionMax);

            // 3. 裁剪
            float effMax = Mathf.Min(Mathf.Max(0f, MaxStepDistance), maxDist);

            // 4. 兜底:effMax < 阈值(已在边界 / 边界上) -> 反转方向
            float thr = ArrivedThreshold < 0f ? 0.05f : ArrivedThreshold;
            if (effMax < thr)
            {
                dir = -dir;
                maxDist = MaxDistanceInsideBox(start, dir, regionMin, regionMax);
                effMax = Mathf.Min(Mathf.Max(0f, MaxStepDistance), maxDist);
            }

            // 5. 实际距离 ∈ [thr, effMax] 随机
            float dist;
            if (effMax <= thr)
            {
                // 终极兜底:区域退化为一个点(Size 极小),就走 0 距离,下一帧由到达判定立刻进 Idle
                dist = 0f;
            }
            else
            {
                dist = Mathf.Lerp(thr, effMax, (float)_rng.NextDouble());
            }

            _moveDir = dist > 0.0001f ? dir : Vector2.zero;
            _moveDist = dist;

            // 6. 估算总时长:用 PeakSpeed 与"曲线平均速度因子"做反推,得到 t01 的归一化分母
            //    Constant: avgFactor = 1;FastToSlow: avgFactor = 2/π ≈ 0.6366;SlowToFast: avgFactor = 2/π
            float avgFactor = AverageCurveFactor(Mode);
            float avgSpeed = Mathf.Max(0.0001f, PeakSpeed) * avgFactor;
            _moveDuration = dist / avgSpeed;
            if (_moveDuration < 0.0001f) _moveDuration = 0.0001f;
        }

        /// <summary>
        /// 从 start 出发,沿 dir(不必归一化)走多远会撞到 box 边界。
        /// 算法:对 X / Y 各自算到对应边的参数 t,取 min。
        ///   tX = (dir.x > 0) ? (maxX - start.x) / dir.x
        ///      : (dir.x < 0) ? (minX - start.x) / dir.x
        ///      : +∞
        /// (对 Y 同理)
        /// </summary>
        static float MaxDistanceInsideBox(Vector2 start, Vector2 dir, Vector2 boxMin, Vector2 boxMax)
        {
            float tX, tY;
            const float INF = float.PositiveInfinity;

            if (dir.x >  0.00001f) tX = (boxMax.x - start.x) / dir.x;
            else if (dir.x < -0.00001f) tX = (boxMin.x - start.x) / dir.x;
            else tX = INF;

            if (dir.y >  0.00001f) tY = (boxMax.y - start.y) / dir.y;
            else if (dir.y < -0.00001f) tY = (boxMin.y - start.y) / dir.y;
            else tY = INF;

            float t = Mathf.Min(tX, tY);
            if (t < 0f) t = 0f; // 已在边界外(理论上不该发生,但兜底)
            return t;
        }

        /// <summary>
        /// 速度曲线因子:输入 t01 ∈ [0,1](本次移动已用 / 总时长),返回 [0,1] 的速度比例。
        ///   Constant   -> 1
        ///   FastToSlow -> cos(t01 · π/2)   单调递减,t=0 -> 1, t=1 -> 0
        ///   SlowToFast -> sin(t01 · π/2)   单调递增,t=0 -> 0, t=1 -> 1
        /// </summary>
        static float CurveFactor(SpeedCurve mode, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            switch (mode)
            {
                case SpeedCurve.Constant:   return 1f;
                case SpeedCurve.FastToSlow: return Mathf.Cos(t01 * Mathf.PI * 0.5f);
                case SpeedCurve.SlowToFast: return Mathf.Sin(t01 * Mathf.PI * 0.5f);
                default: return 1f;
            }
        }

        /// <summary>
        /// 曲线在 [0,1] 上的平均速度因子,用于反推 moveDuration。
        ///   Constant   -> 1
        ///   FastToSlow -> (2/π) ≈ 0.6366   cos 在 [0, π/2] 的均值
        ///   SlowToFast -> (2/π) ≈ 0.6366   sin 在 [0, π/2] 的均值
        /// </summary>
        static float AverageCurveFactor(SpeedCurve mode)
        {
            switch (mode)
            {
                case SpeedCurve.Constant:   return 1f;
                case SpeedCurve.FastToSlow: return 2f / Mathf.PI;
                case SpeedCurve.SlowToFast: return 2f / Mathf.PI;
                default: return 1f;
            }
        }
    }
}

