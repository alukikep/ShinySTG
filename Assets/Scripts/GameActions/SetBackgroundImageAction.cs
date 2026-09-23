using System;
using SerializeReferenceEditor;
using ShinySTG.Background;
using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Set Background Image")]
    public sealed class SetBackgroundImageAction : GameAction
    {
        [Tooltip("Lower 位于 3D 下方，Upper 位于 3D 上方。")]
        public BackgroundImageLayer Layer = BackgroundImageLayer.Upper;
        [Tooltip("目标贴图配置；留空表示隐藏此层。")]
        public BackgroundImageDefinition Image;
        [Min(0f), Tooltip("旧贴图淡出时间；隐藏时只使用此时间。")]
        public float FadeOut;
        [Min(0f), Tooltip("新贴图淡入时间。正常完成后贴图保持显示。")]
        public float FadeIn = 0.5f;

        public override GameActionRuntime CreateRuntime(GameActionContext context)
            => new Runtime(context.LevelRuntime, Layer, Image, FadeOut, FadeIn);

        sealed class Runtime : GameActionRuntime
        {
            readonly LevelRuntime _runtime;
            readonly BackgroundImageLayer _layer;
            readonly BackgroundImageDefinition _image;
            readonly float _fadeOut, _fadeIn;
            BackgroundPlaybackHandle _handle;
            bool _preview;

            public Runtime(LevelRuntime runtime, BackgroundImageLayer layer, BackgroundImageDefinition image, float fadeOut, float fadeIn)
            {
                _runtime = runtime; _layer = layer; _image = image; _fadeOut = fadeOut; _fadeIn = fadeIn;
            }

            public override void Start()
            {
                if (!Application.isPlaying) { _preview = true; return; }
                var level = LevelController.Instance;
                if (level == null || _runtime == null || level.Runtime != _runtime)
                    throw new InvalidOperationException("[Background] 2D 背景动作缺少真实关卡上下文。");
                var binding = level.GetComponent<LevelBackgroundBinding>();
                if (binding == null) throw new InvalidOperationException("[Background] 缺少 LevelBackgroundBinding。");
                _handle = binding.SetImageForRuntime(_runtime, _layer, _image, _fadeOut, _fadeIn);
            }

            public override bool IsComplete
            {
                get
                {
                    if (_preview) return true;
                    if (_handle == null) return false;
                    if (_handle.Status == BackgroundPlaybackStatus.Failed)
                        throw new InvalidOperationException($"[Background] 2D 背景失败：{_handle.Failure}");
                    if (_handle.Status == BackgroundPlaybackStatus.Cancelled)
                        throw new OperationCanceledException("[Background] 2D 背景被取消或接管。");
                    return _handle.IsComplete;
                }
            }

            public override void Dispose() { _handle?.Cancel(); _handle = null; }
        }
    }
}
