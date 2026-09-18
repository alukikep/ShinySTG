using System;
using ShinySTG.Background;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SerializeReferenceEditor.SRName("背景/Play Background Loop")]
    public sealed class PlayBackgroundLoopEntry : SpawnEntry
    {
        [Tooltip("一次启动整个镜头序列，持续循环至下一条镜头或换景指令，不阻塞关卡。")]
        public BackgroundLoopCue Cue;

        public override bool ShouldTrigger(bool alreadyFired) => !alreadyFired;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (!Application.isPlaying) return;
            var level = LevelController.Instance;
            if (level == null || runtime == null || level.Runtime != runtime) return;
            level.RequestBackgroundLoop(runtime, Cue);
        }
    }
}
