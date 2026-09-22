using ShinySTG.Level.SpawnEntries;
using UnityEngine;

namespace ShinySTG.Level.Editor.Drawers
{
    public sealed class PlayPresentationEntryDrawer : ISpawnEntryDrawer
    {
        public override bool Handles(SpawnEntry entry) => entry is PlayPresentationEntry;
        public override string GetLabel(SpawnEntry entry) => $"🎴 {((PlayPresentationEntry)entry).Presentation?.name ?? "<no presentation>"}";
        public override string GetSubLabel(SpawnEntry entry) => GetLabel(entry);
        public override Color GetColor(SpawnEntry entry) => new Color(.85f, .35f, .75f);
    }
}
