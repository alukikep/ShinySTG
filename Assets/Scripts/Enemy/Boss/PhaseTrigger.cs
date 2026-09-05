using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 一条阶段退出条件:监听某个 BossSignal,值满足 Op+Threshold 即触发。
    /// </summary>
    [Serializable]
    public class PhaseTrigger
    {
        [SerializeReference, SR]
        [Tooltip("被监听的信号。下拉选:HpSignal / ShotsFiredSignal / PhaseTimeSignal / 自定义。")]
        public BossSignal Signal;

        public ComparisonOp Op = ComparisonOp.LessOrEqual;
        public float Threshold = 50f;

        public bool IsSatisfied()
        {
            if (Signal == null) return false;
            float v = Signal.CurrentValue;
            switch (Op)
            {
                case ComparisonOp.LessThan: return v < Threshold;
                case ComparisonOp.LessOrEqual: return v <= Threshold;
                case ComparisonOp.Equal: return Mathf.Approximately(v, Threshold);
                case ComparisonOp.GreaterOrEqual: return v >= Threshold;
                case ComparisonOp.GreaterThan: return v > Threshold;
            }
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
