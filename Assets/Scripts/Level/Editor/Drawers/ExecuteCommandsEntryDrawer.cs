using ShinySTG.Level.SpawnEntries;
using UnityEngine;

namespace ShinySTG.Level.Editor.Drawers
{
    public sealed class ExecuteCommandsEntryDrawer : ISpawnEntryDrawer
    {
        public override bool Handles(SpawnEntry entry) => entry is ExecuteCommandsEntry;
        public override string GetLabel(SpawnEntry entry) => $"执行指令 @ {entry.TriggerTime:F1}s";
        public override Color GetColor(SpawnEntry entry) => new Color(0.85f, 0.55f, 0.25f);
        public override float GetDuration(SpawnEntry entry) => 0f;
        public override string GetSubLabel(SpawnEntry entry)
        {
            var commandEntry = entry as ExecuteCommandsEntry;
            var count = commandEntry?.Commands?.Length ?? 0;
            return $"{count} command(s)";
        }
    }
}
