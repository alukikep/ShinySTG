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

        [Tooltip("血管配置。阶段通过稳定 ID 引用，独立于阶段数量。")]
        public BossHealth.HealthBar[] Bars = { new BossHealth.HealthBar { Id = "bar-1" } };
        [SerializeReference, SR, Tooltip("阶段条件可引用的信号；每场遭遇创建独立实例。")]
        public BossSignal[] Signals;
        [SerializeReference, SR, Tooltip("完整阶段列表，行为、条件和演出随阶段一起排序。")]
        public BossPhase[] Phases;
        [Tooltip("最后阶段退出后重新进入首阶段。")]
        public bool Loop;

        public bool TryValidate(out string error)
        {
            error = null;
            if (Bars == null || Bars.Length == 0) error = "至少配置一管血。";
            else
            {
                var ids = new System.Collections.Generic.HashSet<string>();
                foreach (var bar in Bars)
                    if (bar == null || string.IsNullOrWhiteSpace(bar.Id) || !ids.Add(bar.Id) ||
                        !(bar.MaxHp > 0f) || float.IsInfinity(bar.MaxHp))
                    { error = "血管需要唯一非空 ID 和有限正数血量。"; break; }
            }
            if (error == null && (Phases == null || Phases.Length == 0 || Array.Exists(Phases, phase => phase == null)))
                error = "至少配置一个阶段，且阶段不能留空。";
            return error == null;
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
