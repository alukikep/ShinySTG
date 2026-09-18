using System;
using ShinySTG.Background;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SerializeReferenceEditor.SRName("背景/Switch Background")]
    public sealed class SwitchBackgroundEntry : SpawnEntry
    {
        [Tooltip("目标背景配置。")]
        public BackgroundDefinition Background;
        [Min(0f), Tooltip("背景淡出秒数。")]
        public float FadeOut = 1f;
        [Min(0f), Tooltip("背景淡入秒数。")]
        public float FadeIn = 1f;

        public override bool ShouldTrigger(bool alreadyFired) => !alreadyFired;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (!Application.isPlaying) return;
            var level = LevelController.Instance;
            if (level == null || runtime == null || level.Runtime != runtime) return;
            level.RequestBackgroundSwitch(runtime, Background, FadeOut, FadeIn);
        }
    }
}
