using System;
using ShinySTG.Audio;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    public enum MusicControlAction
    {
        PlayPlaylist,
        PlayTrack,
        Pause,
        Resume,
        Stop
    }

    /// <summary>在关卡时间轴上控制 BGM；Pause 只暂停音乐，不阻塞关卡时间轴。</summary>
    [Serializable, SerializeReferenceEditor.SRName("音乐/Music Control")]
    public sealed class MusicControlEntry : SpawnEntry
    {
        public MusicControlAction Action = MusicControlAction.PlayPlaylist;
        public BgmPlaylist Playlist;
        public BgmTrack Track;
        [Range(0f, 5f)] public float Crossfade = 1.5f;

        public override bool ShouldTrigger(bool alreadyFired) => !alreadyFired;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            switch (Action)
            {
                case MusicControlAction.PlayPlaylist:
                    AudioMix.PlayPlaylist(Playlist);
                    break;
                case MusicControlAction.PlayTrack:
                    AudioMix.PlayTrack(Track, Crossfade);
                    break;
                case MusicControlAction.Pause:
                    AudioMix.PauseMusic();
                    break;
                case MusicControlAction.Resume:
                    AudioMix.ResumeMusic();
                    break;
                case MusicControlAction.Stop:
                    AudioMix.StopMusic(Crossfade);
                    break;
            }
        }
    }
}
