using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 两点巡逻。在入场位置(StartPos)和 EndPosition 之间来回移动。
    ///
    /// 循环模式:
    ///   - PingPong: 来回,到达一端后切到另一端(默认)
    ///   - Once: 单程,走到 EndPosition 就停下(可由外层 MoveAction.Duration 自然结束)
    ///
    /// 算法(每帧):
    ///   若 |pos - target| < ArrivedThreshold:
    ///     PingPong: 切换 target(Start ↔ End),_segmentStart = 旧 target
    ///     Once: return(停下)
    ///   t = 1 - dist / _segmentDist  (已走比例,供 Ease 用)
    ///   step = min(dist, Speed * Ease(...) * dt)
    ///   pos += dir * step
    ///
    /// SnapOnEnter 保证零瞬移(第一帧 dt=0 等价不动)。
    /// </summary>
    [Serializable, SRName("Move/Patrol")]
    public class PatrolMove : MoveBehaviour
    {
        public enum PatrolMode
        {
            PingPong, // 来回
            Once      // 单程
        }

        [Tooltip("终点绝对世界坐标(场景坐标系)。\n" +
                 "敌人会从入场位置走到这里,然后根据 LoopMode 决定停下还是往返。\n" +
                 "例:从 (0, 4) 入场,设 EndPosition = (0, -2) → 敌人向下走到 (0, -2)。")]
        public Vector2 EndPosition = new Vector2(3f, 0f);

        [Tooltip("移动速度(单位/秒)。实际速度会被 Ease 因子调制(Linear 时恒定)。")]
        public float Speed = 2f;

        [Tooltip("循环模式。PingPong = 来回(到达终点切回起点);" +
                 "Once = 单程(到终点停下,外层 MoveAction.Duration 决定总时长)。")]
        public PatrolMode LoopMode = PatrolMode.PingPong;

        [Tooltip("缓动曲线。Linear = 匀速(默认);InOutSine 等 = 端点慢中段快,适合「巡逻到位时减速」。\n" +
                 "OutBack 谨慎使用 —— 巡逻时来回反弹可能看起来很怪。")]
        public EaseMode Mode = EaseMode.Linear;

        [Tooltip("到达判定阈值(单位)。小于此距离算「到了」。0.05 = 5cm。")]
        public float ArrivedThreshold = 0.05f;

        [Tooltip("最小速度因子(0~1)。即使 Ease 曲线在起点/终点返回 0,实际速度也不会低于 Speed * 此值。\n" +
                 "0(默认) = 完全按 Ease 曲线;0.1 = 起步也有 10% 速度,保证巡逻不会「卡住」。")]
        public float MinSpeedFactor = 0f;

        // 运行时状态
        Vector2 _startPos;     // 入场位置
        Vector2 _target;       // 当前移动目标(start 或 end)
        Vector2 _segmentStart; // 当前段的起点(用于算 t)
        float   _segmentDist;  // 当前段的总长度

        // 本类是"绝对锚定"型;SnapOnEnter 触发但 dt=0 时 step=0,无实际移动
        public override bool SnapOnEnter => true;

        public override void OnEnter(Transform enemy)
        {
            _startPos     = enemy.position;
            _target       = EndPosition;
            _segmentStart = _startPos;
            _segmentDist  = Vector2.Distance(_segmentStart, _target);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            Vector2 current = enemy.position;
            Vector2 delta = _target - current;
            float dist = delta.magnitude;

            // 已到达当前段目标
            if (dist <= ArrivedThreshold)
            {
                if (LoopMode == PatrolMode.Once)
                {
                    // 单程:到达就停下
                    return;
                }

                // PingPong:切换 target
                _target = (_target == _startPos) ? EndPosition : _startPos;
                _segmentStart = current; // 新段从当前位置开始
                _segmentDist = Vector2.Distance(_segmentStart, _target);
                if (_segmentDist < ArrivedThreshold)
                {
                    // 极端情况:start == end,无需移动
                    return;
                }
                delta = _target - current;
                dist = delta.magnitude;
            }

            float t = _segmentDist > 0.0001f
                ? Mathf.Clamp01(dist / _segmentDist)
                : 1f;
            float speedFactor = Ease.Evaluate(Mode, t);
            if (speedFactor < MinSpeedFactor) speedFactor = MinSpeedFactor;
            float step = Mathf.Min(dist, Speed * speedFactor * dt);
            Vector2 dir = delta / dist;
            enemy.position = current + dir * step;
        }
    }
}
