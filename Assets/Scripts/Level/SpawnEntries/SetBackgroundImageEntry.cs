using System;
using ShinySTG.Background;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    [Serializable, SerializeReferenceEditor.SRName("背景/Set Background Image")]
    public sealed class SetBackgroundImageEntry : SpawnEntry
    {
        [Tooltip("Lower 位于 3D 下方，Upper 位于 3D 上方。")]
        public BackgroundImageLayer Layer = BackgroundImageLayer.Upper;
        [Tooltip("目标贴图配置；留空表示隐藏此层。")]
        public BackgroundImageDefinition Image;
        [Min(0f), Tooltip("旧贴图淡出时间；隐藏时只使用此时间。")]
        public float FadeOut;
        [Min(0f), Tooltip("新贴图淡入时间。")]
        public float FadeIn = 0.5f;

        public override bool ShouldTrigger(bool alreadyFired) => !alreadyFired;
        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (!Application.isPlaying) return;
            var level = LevelController.Instance;
            if (level != null) level.RequestBackgroundImage(runtime, Layer, Image, FadeOut, FadeIn);
        }
    }
}
