using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>分段配置；剩余血量由 BossHealth 持有，不写入配置。</summary>
    [Serializable]
    public sealed class BossHealthSegment
    {
        public enum SegmentKind { NonSpell, Spell }

        [Tooltip("本管内唯一的稳定 ID；排序和改名不改变此 ID。")]
        public string Id = "segment-1";
        [Tooltip("分段名称。")]
        public string Name = "Segment";
        [Tooltip("非符或符卡；颜色和时限可独立配置。")]
        public SegmentKind Kind;
        [Min(0.01f), Tooltip("本段实际血量；整管上限由各段相加。")]
        public float MaxHp = 1000f;
        [Min(0f), Tooltip("承伤倍率：1 为正常，0.5 为减伤 50%，0 为不扣血；允许大于 1。")]
        public float DamageTakenMultiplier = 1f;
        [Tooltip("本段血条颜色，不影响战斗规则。")]
        public Color Color = UnityEngine.Color.white;
        [Tooltip("是否启用本段独立时限。")]
        public bool HasTimeLimit = true;
        [Min(0.01f), Tooltip("本段时限，单位秒；关闭时限时不生效。")]
        public float TimeLimit = 60f;
        [SerializeReference, SR, Tooltip("本段内顺序执行的状态；血量阈值以本段为基准。")]
        public BossPhase[] States;

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool ValidateSegments(BossHealthSegment[] segments, out string error)
        {
            error = null;
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            double total = 0;
            foreach (var segment in segments)
            {
                if (segment == null || string.IsNullOrWhiteSpace(segment.Id) || !ids.Add(segment.Id))
                { error = "分段需要本管内唯一非空 ID。"; return false; }
                if (!(segment.MaxHp > 0f) || !IsFinite(segment.MaxHp))
                { error = "分段血量必须为有限正数。"; return false; }
                total += segment.MaxHp;
                if (total > float.MaxValue)
                { error = "分段合计血量超出有效范围。"; return false; }
                if (!IsFinite(segment.DamageTakenMultiplier) || segment.DamageTakenMultiplier < 0f)
                { error = "承伤倍率必须为有限非负数。"; return false; }
                if (segment.Kind != SegmentKind.NonSpell && segment.Kind != SegmentKind.Spell)
                { error = "分段类型无效。"; return false; }
                if (segment.HasTimeLimit && (!(segment.TimeLimit > 0f) || !IsFinite(segment.TimeLimit)))
                { error = "分段时限必须为有限正数。"; return false; }
                if (!ValidateStates(segment.States, out error)) return false;
            }
            return true;
        }

        static bool ValidateStates(BossPhase[] states, out string error)
        {
            error = null;
            if (states == null || states.Length == 0 || Array.Exists(states, state => state == null))
            { error = "每个分段至少需要一个非空状态。"; return false; }
            float previousPercent = 100f;
            for (int i = 0; i + 1 < states.Length; i++)
            {
                var state = states[i];
                if (state.AdvanceMode == BossPhase.StateAdvanceMode.Time)
                {
                    if (state.AdvanceAfterSeconds > 0f && IsFinite(state.AdvanceAfterSeconds)) continue;
                }
                else if (state.AdvanceMode == BossPhase.StateAdvanceMode.HealthPercent &&
                    IsFinite(state.AdvanceAtPercent) && state.AdvanceAtPercent > 0f &&
                    state.AdvanceAtPercent < previousPercent)
                {
                    previousPercent = state.AdvanceAtPercent;
                    continue;
                }
                error = "分段状态需要有效切换条件；血量阈值须在 0～100 之间并严格递减。";
                return false;
            }
            return true;
        }
    }
}
