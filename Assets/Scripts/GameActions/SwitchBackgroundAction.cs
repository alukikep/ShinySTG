using System;
using SerializeReferenceEditor;
using ShinySTG.Background;
using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Switch Background")]
    public sealed class SwitchBackgroundAction : GameAction
    {
        [Tooltip("目标背景配置；通过当前关卡绑定执行换景。")]
        public BackgroundDefinition Background;
        [Min(0f), Tooltip("背景淡出秒数。")]
        public float FadeOut = 1f;
        [Min(0f), Tooltip("背景淡入秒数。")]
        public float FadeIn = 1f;

        public override GameActionRuntime CreateRuntime(GameActionContext context) =>
            new Runtime(Background, FadeOut, FadeIn, context.LevelRuntime);

        sealed class Runtime : GameActionRuntime
        {
            readonly BackgroundDefinition _background;
            readonly float _fadeOut, _fadeIn;
            readonly LevelRuntime _runtime;
            BackgroundPlaybackHandle _handle;
            bool _preview;

            public Runtime(BackgroundDefinition background, float fadeOut, float fadeIn, LevelRuntime runtime)
            {
                _background = background;
                _fadeOut = fadeOut;
                _fadeIn = fadeIn;
                _runtime = runtime;
            }

            public override void Start()
            {
                if (!Application.isPlaying) { _preview = true; return; }
                var level = LevelController.Instance;
                if (level == null || _runtime == null || level.Runtime != _runtime)
                    throw new InvalidOperationException("[Background] 换景动作缺少真实关卡上下文，不能从预览或旧 Runtime 播放。");
                var binding = level.GetComponent<LevelBackgroundBinding>();
                if (binding == null) throw new InvalidOperationException("[Background] 缺少 LevelBackgroundBinding。");
                _handle = binding.SwitchForRuntime(_runtime, _background, _fadeOut, _fadeIn);
            }

            public override bool IsComplete
            {
                get
                {
                    if (_preview) return true;
                    if (_handle == null) return false;
                    if (_handle.Status == BackgroundPlaybackStatus.Failed)
                        throw new InvalidOperationException($"[Background] 换景失败：{_handle.Failure}");
                    if (_handle.Status == BackgroundPlaybackStatus.Cancelled)
                        throw new OperationCanceledException("[Background] 换景被取消或接管，终止后续动作。");
                    return _handle.Status == BackgroundPlaybackStatus.Completed;
                }
            }

            public override void Dispose()
            {
                _handle?.Cancel();
                _handle = null;
            }
        }
    }
}
