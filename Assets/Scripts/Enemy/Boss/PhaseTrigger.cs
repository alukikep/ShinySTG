using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 一条阶段退出条件:监听 BossController.Signals 数组里某个 Signal 的值,满足 Op+Threshold 即触发。
    ///
    /// [重要]为什么不直接持有 BossSignal 实例(SerializeReference)?
    ///   SerializeReference 在 Unity 序列化的引用是**独立 .NET 对象**,即使字段值指向同一个 rid,
    ///   不同位置的 SerializeReference 在反序列化时是 2 份独立的实例。这意味着:
    ///     - PhaseTrigger.Signal._health 绑不上(PhaseTrigger 内部的 Signal 实例没收到 OnAttach 通知)
    ///     - CurrentValue 返回 100f(默认 fallback),永远无法满足 LessOrEqual + 0 这种切阶段条件
    ///   所以这里改成 SignalIndex:PhaseTrigger 只持有 int,通过 BossController.GetSignal(idx) 拿 Signal,
    ///   保证 OnAttach 时绑定的 _health 引用可访问。
    /// </summary>
    [Serializable]
    public class PhaseTrigger
    {
        [Tooltip("BossController.Signals 数组里的索引(0-based)。PhaseTrigger 通过索引取 Signal 实例," +
                 "确保和 Signals 数组里的实例是同一个(避免 SerializeReference 独立实例导致 _health 没绑)。")]
        public int SignalIndex = 0;

        public ComparisonOp Op = ComparisonOp.LessOrEqual;
        public float Threshold = 50f;

        /// <summary>
        /// 判定本条 Trigger 是否满足。由 BossPhase.ShouldExit 传入 BossController 上下文 + Signal 实例。
        /// </summary>
        public bool IsSatisfied(BossSignal signal)
        {
            if (signal == null) return false;
            float v = signal.CurrentValue;
            switch (Op)
            {
                case ComparisonOp.LessThan: return v < Threshold;
                case ComparisonOp.LessOrEqual: return v <= Threshold;
                case ComparisonOp.Equal:
                    // Mathf.Approximately 是相对+绝对容差,适合"离散整型信号近似相等"场景
                    // —— 比如 CurrentBarIndexSignal(Int 0/1/2) 触发 Equal。
                    // 不推荐用于 PhaseTimeSignal / ShotsFiredSignal 这类累加型浮点信号:
                    // 每帧 deltaTime 不一定能把 _elapsed 刚好凑到 Threshold 的容差带内,
                    // 触发时机不稳定。要做"打够 N 秒切阶段",请用 GreaterOrEqual + N。
                    return Mathf.Approximately(v, Threshold);
                case ComparisonOp.GreaterOrEqual: return v >= Threshold;
                case ComparisonOp.GreaterThan: return v > Threshold;
            }
            return false;
        }

        // 兼容旧 API:某些外部代码可能调过无参 IsSatisfied()。保留,内部通过 FindObjectOfType 取单例。
        // 不推荐使用 —— 走 BossPhase.ShouldExit(BossController ctx) 是正确路径。
        public bool IsSatisfied()
        {
            Debug.LogWarning("[PhaseTrigger] 调了无参 IsSatisfied(),请改用 IsSatisfied(signal) 传实例。当前会回退到 false。");
            return false;
        }
    }

    public enum ComparisonOp
    {
        LessThan,
        LessOrEqual,
        Equal,
        GreaterOrEqual,
        GreaterThan,
    }
}
