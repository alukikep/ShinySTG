using System;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    public enum PhaseExitMode { LegacyTriggers = 0, Conditions = 1 }
    public enum PhaseConditionMatch { Any = 0, All = 1 }
    public enum PhaseExitConditionKind
    {
        BarDepleted = 0,
        BarPercentAtMost = 1,
        TotalHpPercentAtMost = 2,
        PhaseTimeAtLeast = 3,
        Signal = 4
    }

    /// <summary>直接表达阶段退出意图；无运行时状态，可供循环阶段重复使用。</summary>
    [Serializable]
    public sealed class PhaseExitCondition
    {
        [Tooltip("指定血管耗尽、指定管百分比、总血量百分比、阶段时间或高级信号。")]
        public PhaseExitConditionKind Kind;

        [Tooltip("血管条件使用的 BossHealth.Bars 中唯一 ID；不使用数组下标或显示名称。")]
        public string BarId = "";

        [Tooltip("百分比条件使用 0～100；阶段时间使用非负秒数。耗尽条件不使用此值。")]
        public float Threshold = 50f;

        [Tooltip("仅 Signal 条件使用，保留原有信号扩展能力和索引语义。")]
        public PhaseTrigger SignalTrigger = new PhaseTrigger();

        public bool IsSatisfied(BossController controller)
        {
            if (controller == null) return false;
            var health = controller.Health;
            switch (Kind)
            {
                case PhaseExitConditionKind.BarDepleted:
                    return health != null && health.TryGetBarPercent(BarId, out float depleted) && depleted <= 0f;
                case PhaseExitConditionKind.BarPercentAtMost:
                    return IsPercent(Threshold) && health != null &&
                        health.TryGetBarPercent(BarId, out float percent) && percent <= Threshold;
                case PhaseExitConditionKind.TotalHpPercentAtMost:
                    return IsPercent(Threshold) && health != null && health.TotalBarCount > 0 &&
                        health.TotalHpPercent <= Threshold;
                case PhaseExitConditionKind.PhaseTimeAtLeast:
                    return Threshold >= 0f && !float.IsInfinity(Threshold) && controller.PhaseElapsedSeconds >= Threshold;
                case PhaseExitConditionKind.Signal:
                    return SignalTrigger != null && SignalTrigger.IsSatisfied(controller.GetSignal(SignalTrigger.SignalIndex));
                default:
                    return false;
            }
        }

        static bool IsPercent(float value) => value >= 0f && value <= 100f;
    }
}
