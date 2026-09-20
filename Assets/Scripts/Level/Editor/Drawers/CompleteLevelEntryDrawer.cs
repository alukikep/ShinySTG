using ShinySTG.Level.SpawnEntries;
using UnityEngine;

namespace ShinySTG.Level.Editor.Drawers
{
    public sealed class CompleteLevelEntryDrawer : ISpawnEntryDrawer
    {
        public override bool Handles(SpawnEntry entry) => entry is CompleteLevelEntry;
        public override string GetLabel(SpawnEntry entry) => $"关卡通关 @ {entry.TriggerTime:F1}s";
        public override Color GetColor(SpawnEntry entry) => new Color(0.3f, 0.8f, 0.45f);
        public override float GetDuration(SpawnEntry entry) => 0f;
    }
}
