using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 二次贝塞尔曲线移动。常用于"飞入场 → 走弧线 → 落点 / Boss 退场弧线"。
    ///
    /// 位置公式(每帧):
    ///   t = clamp(_elapsed / Duration, 0, 1)
    ///   B(t) = (1−t)² · P0 + 2(1−t)·t · P1 + t² · P2
    ///
    /// 时长与外层 MoveAction.Duration 的关系:
    ///   - 本类有独立的 Duration 字段(曲线 t=0..1 跨多久);
    ///   - 外层 MoveAction.Duration 决定"整条 Move 行为"持续多久;
    ///   - 当 _elapsed &lt; Duration  → 沿曲线走;
    ///   - 当 _elapsed ≥ Duration  → 固定停在 P2(后续帧不移动,直到外层切走);
    ///   - 若外层 Duration &lt; 本类 Duration → 曲线被截断,敌人停在 t &lt; 1 的位置
    ///     (因为外层 OnExit 会强制切到下一条 Action)。
    ///   实用建议:把外层 Duration 设为 ≥ 本类 Duration,让曲线走完整。
    ///
    /// 约定:相对锚定 —— OnEnter 时记录 P0(进入位置)和控制点,后续每帧算曲线绝对位置。
    /// (P2 可切换为绝对世界坐标,见 P2IsAbsolute 字段。)
    /// </summary>
    [Serializable, SRName("Move/Bezier")]
    public class BezierMove : MoveBehaviour
    {
        public enum StartPointMode
        {
            EntryPosition, // 起点 = 进入该行为时的敌人位置(P0 不读 StartPoint)
            Custom         // 起点 = StartPoint(绝对世界坐标)
        }

        [Tooltip("P0(起点)模式。EntryPosition = 进入行为时的敌人位置;" +
                 "Custom = 用 StartPoint 字段(绝对世界坐标)。")]
        public StartPointMode StartMode = StartPointMode.EntryPosition;

        [Tooltip("当 StartMode=Custom 时,P0 的绝对世界坐标。")]
        public Vector2 StartPoint = Vector2.zero;

        [Tooltip("P1(第一个控制点)位置。\n" +
                 "当 P1IsRelative=true(默认):相对 P0 的偏移;" +
                 "当 P1IsRelative=false:绝对世界坐标。\n" +
                 "控制点不一定要在曲线上,只决定曲线弯曲方向。\n" +
                 "常见配法:P1 在 P0 斜上方 + P2 在 P0 斜下方 → 形成 S 形弧。")]
        public Vector2 ControlPoint1 = new Vector2(1f, 1f);

        [Tooltip("P1 坐标基准。true = 相对 P0(偏移量,推荐);false = 绝对世界坐标。")]
        public bool P1IsRelative = true;

        [Tooltip("P2(终点)位置。\n" +
                 "当 P2IsAbsolute=false(默认):相对 P0 的偏移;" +
                 "当 P2IsAbsolute=true:绝对世界坐标(如\"飞到屏幕中央 (0,0)\")。\n" +
                 "曲线在 _elapsed >= Duration 后停在 P2。")]
        public Vector2 ControlPoint2 = new Vector2(2f, 0f);

        [Tooltip("P2 坐标基准。false = 相对 P0(偏移量,推荐);true = 绝对世界坐标。")]
        public bool P2IsAbsolute = false;

        [Tooltip("曲线总时长(秒)。t = clamp(_elapsed / Duration, 0, 1)。\n" +
                 "<=0 时兜底为 2。\n" +
                 "建议把外层 MoveAction.Duration 设为 ≥ 此值,否则曲线会被外层截断。")]
        public float Duration = 2f;

        // 运行时状态
        Vector2 _p0;
        Vector2 _p1;
        Vector2 _p2;
        float   _elapsed;

        // 本类是"绝对锚定"型,OnEnter 后立即把 enemy.position 同步到曲线 t=0 时的起点 = P0
        // (避免从上一条 Action 终点瞬移到 P0)
        public override bool SnapOnEnter => true;

        public override void OnEnter(Transform enemy)
        {
            _p0 = (StartMode == StartPointMode.Custom) ? StartPoint : (Vector2)enemy.position;

            // P1 / P2 解析:根据 IsRelative/IsAbsolute 决定是偏移还是绝对
            _p1 = P1IsRelative ? (_p0 + ControlPoint1) : ControlPoint1;
            _p2 = P2IsAbsolute ? ControlPoint2 : (_p0 + ControlPoint2);

            _elapsed = 0f;
        }

        public override void OnTick(Transform enemy, float dt)
        {
            _elapsed += dt;

            float duration = Mathf.Max(0.0001f, Duration);
            float t = Mathf.Clamp01(_elapsed / duration);

            float u = 1f - t;
            Vector2 pos = u * u * _p0
                        + 2f * u * t * _p1
                        + t * t * _p2;

            enemy.position = pos;
        }
    }
}
