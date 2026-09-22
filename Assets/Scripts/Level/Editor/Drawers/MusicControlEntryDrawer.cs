using ShinySTG.Level;
using ShinySTG.Level.SpawnEntries;

namespace ShinySTG.Level.Editor.Drawers
{
    public sealed class MusicControlEntryDrawer : ISpawnEntryDrawer
    {
        public override bool Handles(SpawnEntry entry) => entry is MusicControlEntry;

        public override string GetLabel(SpawnEntry entry)
        {
            var music = (MusicControlEntry)entry;
            return $"♫ {music.Action}";
        }

        public override string GetSubLabel(SpawnEntry entry)
        {
            var music = (MusicControlEntry)entry;
            switch (music.Action)
            {
                case MusicControlAction.PlayPlaylist: return music.Playlist != null ? music.Playlist.name : "<no playlist>";
                case MusicControlAction.PlayTrack: return music.Track != null ? music.Track.name : "<no track>";
                case MusicControlAction.Stop: return $"fade {music.Crossfade:F1}s";
                default: return "music only; timeline continues";
            }
        }
    }
}
