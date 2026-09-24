using System;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI.Boss;
using ShinySTG.Audio;
using UnityEngine;
using ShinySTG.GameActions;

namespace ShinySTG.Level.Encounter
{
    [CreateAssetMenu(menuName = "STG/Boss Encounter", fileName = "BossEncounter")]
    public class BossEncounterDefinition : ScriptableObject
    {
        [Tooltip("这场遭遇生成的 Boss prefab。需要挂 Boss 总控。")]
        public GameObject BossPrefab;

        [Tooltip("Boss 全部血量清空后，继续占用时间轴的收尾秒数。")]
        [Min(0f)] public float DefeatOutroDelay = 1f;

        [Tooltip("Boss 出场时播放的音效。")]
        public SfxCue EncounterStartSfx;

        [Tooltip("Boss 被击败时播放的音效。")]
        public SfxCue DefeatSfx;

        [Tooltip("血管及其内部状态；实际顺序由开始血管和 NextBar 决定。")]
        public BossHealth.HealthBar[] Bars = { new BossHealth.HealthBar { Id = "bar-1" } };
        [Tooltip("必须选择开始血管。")]
        public string StartBarId = "bar-1";
        [SerializeReference, SR, Tooltip("阶段条件可引用的信号；每场遭遇创建独立实例。")]
        public BossSignal[] Signals;
        [SerializeReference, SR, Tooltip("完整阶段列表，行为、条件和演出随阶段一起排序。")]
        public BossPhase[] Phases;
        [Tooltip("最后阶段退出后重新进入首阶段。")]
        public bool Loop;

        public bool TryValidate(out string error)
        {
            error = null;
            if (Bars == null || Bars.Length == 0) { error = "至少配置一管血。"; return false; }
            var map = new System.Collections.Generic.Dictionary<string, BossHealth.HealthBar>(StringComparer.Ordinal);
            foreach (var bar in Bars)
            {
                if (bar == null || string.IsNullOrWhiteSpace(bar.Id) || map.ContainsKey(bar.Id) ||
                    !(bar.EffectiveMaxHp > 0f) || float.IsInfinity(bar.EffectiveMaxHp))
                { error = "血管需要唯一非空 ID 和有限正数血量。"; return false; }
                map.Add(bar.Id, bar);
                if (bar.HasSegments)
                {
                    if (!BossHealthSegment.ValidateSegments(bar.Segments, out error)) return false;
                    continue;
                }
                if (bar.HasTimeLimit && (!(bar.TimeLimit > 0f) || float.IsInfinity(bar.TimeLimit)))
                { error = $"血管 {bar.Name} 必须设置有限正数时限。"; return false; }
                if (bar.States == null || bar.States.Length == 0 || Array.Exists(bar.States, state => state == null))
                { error = $"血管 {bar.Name} 至少需要一个非空状态。"; return false; }
                for (int i = 0; i + 1 < bar.States.Length; i++)
                {
                    var state = bar.States[i];
                    bool valid = state.AdvanceMode == BossPhase.StateAdvanceMode.Time
                        ? state.AdvanceAfterSeconds > 0f && !float.IsInfinity(state.AdvanceAfterSeconds)
                        : state.AdvanceMode == BossPhase.StateAdvanceMode.HealthPercent && state.AdvanceAtPercent >= 0f && state.AdvanceAtPercent <= 100f;
                    if (!valid) { error = $"血管 {bar.Name} 的状态 {i + 1} 切换条件无效。"; return false; }
                }
            }
            if (string.IsNullOrWhiteSpace(StartBarId) || !map.ContainsKey(StartBarId))
            { error = "请选择有效的开始血管。"; return false; }
            foreach (var bar in Bars)
            {
                var visited = new System.Collections.Generic.HashSet<string>();
                var current = bar;
                while (current != null)
                {
                    if (!visited.Add(current.Id)) { error = "血管 NextBar 不允许形成循环。"; return false; }
                    if (string.IsNullOrEmpty(current.NextBarId)) break;
                    if (!map.TryGetValue(current.NextBarId, out current))
                    { error = "下一血管引用不存在。"; return false; }
                }
            }
            return true;
        }

        /// <summary>运行入口与编辑器使用相同的配置校验。</summary>
        public bool TryValidateForRuntime(out string error) => TryValidate(out error);

        // Called only after validation; array order is now an implementation detail of the selected path.
        public BossHealth.HealthBar[] GetOrderedBars()
        {
            if (!TryValidate(out var error)) throw new InvalidOperationException(error);
            var result = new System.Collections.Generic.List<BossHealth.HealthBar>();
            string id = StartBarId;
            while (!string.IsNullOrEmpty(id))
            {
                var bar = Array.Find(Bars, item => item.Id == id);
                result.Add(bar);
                id = bar.NextBarId;
            }
            return result.ToArray();
        }

        public BossSignal GetSignal(int index) =>
            Signals != null && index >= 0 && index < Signals.Length ? Signals[index] : null;

        /// <summary>Unity 序列化克隆隔离嵌套 managed reference，保留行为流、音效等资产引用。</summary>
        public BossEncounterDefinition CreateRuntimeCopy()
        {
            var copy = Instantiate(this);
            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }

        [Tooltip("开场动作；等待完成时暂不启动首阶段。")]
        public ActionSequence StartActions = new();
        [Tooltip("击破动作；等待完成时保留 Boss 对象供演出使用。")]
        public ActionSequence DefeatActions = new();
        [Tooltip("收尾延迟结束后的动作；等待完成后释放关卡时间轴。")]
        public ActionSequence CompleteActions = new();
    }

}
