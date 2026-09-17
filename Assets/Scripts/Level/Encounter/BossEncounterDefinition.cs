using System;
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

        [Tooltip("按 BossController 阶段索引配置进入阶段时的最小演出。")]
        public PhasePresentation[] PhasePresentations;

        [Tooltip("开场动作；等待完成时暂不启动首阶段。")]
        public ActionSequence StartActions = new();
        [Tooltip("击破动作；等待完成时保留 Boss 对象供演出使用。")]
        public ActionSequence DefeatActions = new();
        [Tooltip("收尾延迟结束后的动作；等待完成后释放关卡时间轴。")]
        public ActionSequence CompleteActions = new();
    }

    [Serializable]
    public class PhasePresentation
    {
        [Min(0)] public int PhaseIndex;
        public string DisplayName;
        public SfxCue EnterSfx;
        public SfxCue ExitSfx;
        [Tooltip("在本阶段战斗启动前执行。")]
        public ActionSequence EnterActions = new();
        [Tooltip("在本阶段战斗退出后执行。")]
        public ActionSequence ExitActions = new();
    }
}
