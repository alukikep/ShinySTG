using System;
using ShinySTG.Audio;
using UnityEngine;

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
    }

    [Serializable]
    public class PhasePresentation
    {
        [Min(0)] public int PhaseIndex;
        public string DisplayName;
        public SfxCue EnterSfx;
        public SfxCue ExitSfx;
    }
}
