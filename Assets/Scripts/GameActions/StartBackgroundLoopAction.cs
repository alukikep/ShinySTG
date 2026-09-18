using System;
using SerializeReferenceEditor;
using ShinySTG.Background;
using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Start Background Loop")]
    public sealed class StartBackgroundLoopAction : GameAction
    {
        [Tooltip("成功启动即结束此动作，循环由背景控制器持有，直到新的镜头调用接管。")]
        public BackgroundLoopCue Cue;

        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Cue, context.LevelRuntime);

        sealed class Runtime : GameActionRuntime
        {
            readonly BackgroundLoopCue _cue;
            readonly LevelRuntime _levelRuntime;
            bool _started;

            public Runtime(BackgroundLoopCue cue, LevelRuntime levelRuntime)
            {
                _cue = cue;
                _levelRuntime = levelRuntime;
            }

            public override bool IsComplete => _started;

            public override void Start()
            {
                if (_started) return;
                if (!Application.isPlaying) { _started = true; return; }
                var level = LevelController.Instance;
                if (_levelRuntime == null || level == null || level.Runtime != _levelRuntime)
                    throw new InvalidOperationException("[Background] 循环镜头动作缺少当前真实关卡上下文。");
                var binding = level.GetComponent<LevelBackgroundBinding>();
                if (binding == null)
                    throw new InvalidOperationException("[Background] 当前关卡缺少 LevelBackgroundBinding。");
                var handle = binding.PlayLoopForRuntime(_levelRuntime, _cue);
                if (handle.Status == BackgroundPlaybackStatus.Failed)
                    throw new InvalidOperationException($"[Background] 循环镜头启动失败：{handle.Failure}");
                _started = true;
                // 控制权已交给背景；Runner 正常 Dispose 不应终止持续循环。
            }
        }
    }
}
