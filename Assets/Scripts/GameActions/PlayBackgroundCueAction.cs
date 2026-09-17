using System;
using SerializeReferenceEditor;
using ShinySTG.Background;
using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Play Background Cue")]
    public sealed class PlayBackgroundCueAction : GameAction
    {
        [Tooltip("通过当前关卡的背景绑定播放；动作按实际过渡结束。")]
        public BackgroundCue Cue;

        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Cue, context.LevelRuntime);

        sealed class Runtime : GameActionRuntime
        {
            readonly BackgroundCue _cue;
            readonly LevelRuntime _levelRuntime;
            BackgroundPlaybackHandle _handle;
            bool _preview;

            public Runtime(BackgroundCue cue, LevelRuntime levelRuntime)
            {
                _cue = cue;
                _levelRuntime = levelRuntime;
            }

            public override void Start()
            {
                // 编辑器预览可构造 Encounter，但不能改动真实背景。
                if (!Application.isPlaying) { _preview = true; return; }
                var level = LevelController.Instance;
                if (_levelRuntime == null || level == null || level.Runtime != _levelRuntime)
                    throw new InvalidOperationException("[Background] 背景动作缺少当前真实关卡上下文；预览或旧 Runtime 不可播放。");
                var binding = level.GetComponent<LevelBackgroundBinding>();
                if (binding == null)
                    throw new InvalidOperationException("[Background] 当前关卡缺少 LevelBackgroundBinding。");
                _handle = binding.PlayForRuntime(_levelRuntime, _cue);
            }

            public override bool IsComplete
            {
                get
                {
                    if (_preview) return true;
                    if (_handle == null) return false;
                    if (_handle.Status == BackgroundPlaybackStatus.Failed)
                        throw new InvalidOperationException($"[Background] Cue 播放失败：{_handle.Failure}");
                    if (_handle.Status == BackgroundPlaybackStatus.Cancelled)
                        throw new OperationCanceledException("[Background] Cue 被外部取消或接管，终止后续动作。");
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
