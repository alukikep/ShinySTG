using System;
using ShinySTG.Background;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SerializeReferenceEditor.SRName("背景/Play Background Cue")]
    public sealed class PlayBackgroundCueEntry : SpawnEntry
    {
        [Tooltip("要触发的背景过渡；过渡时长由 Cue 决定，不阻塞关卡。")]
        public BackgroundCue Cue;

        // 背景是一次性时间点命令，避免 OneShot=false 导致每帧重播。
        public override bool ShouldTrigger(bool alreadyFired) => !alreadyFired;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (!Application.isPlaying) return;
            var level = LevelController.Instance;
            if (level == null || runtime == null || level.Runtime != runtime) return;
            level.RequestBackgroundCue(runtime, Cue);
        }
    }
}
