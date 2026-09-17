using System;
using SerializeReferenceEditor;
using ShinySTG.Level.Encounter;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SRName("遭遇/Boss 战")]
    public class BossEncounterEntry : SpawnEntry
    {
        public BossEncounterDefinition Encounter;
        public Vector2 SpawnPosition = Vector2.zero;
        public float InitialRotation;

        [Tooltip("开启后，关卡时间轴等待整场遭遇（包括击破收尾延迟）完成。")]
        public bool BlockTimeline = true;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (Encounter == null || Encounter.BossPrefab == null)
            {
                Debug.LogWarning("[Level] BossEncounterEntry 缺少 Encounter 或 BossPrefab，已跳过。", def);
                return;
            }

            var go = UnityEngine.Object.Instantiate(
                Encounter.BossPrefab,
                SpawnPosition,
                Quaternion.Euler(0f, 0f, InitialRotation));

            runtime.Track(go);
            var encounterRuntime = new BossEncounterRuntime(Encounter, go, BlockTimeline);
            runtime.AddTimelineProcess(encounterRuntime);
            LevelController.Instance?.NotifyBossSpawned(go);
        }
    }
}
