using UnityEngine;

namespace ShinySTG.Background
{
    public sealed partial class StageBackgroundController
    {
        BackgroundLoopCue.Node[] _loopNodes;
        double _loopTime;
        double _loopDuration;
        bool _loopEntering;

        public bool IsLooping => IsTransitioning && _loopNodes != null;
        public string PlaybackKind => !IsTransitioning ? "Idle" : _switching ? "Switch" : IsLooping ? "Loop" : "Cue";

        public BackgroundPlaybackHandle PlayLoop(BackgroundLoopCue cue)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !HasValidBindings() || cue == null)
                return FailedPlayback("循环播放需要启用的运行时控制器、有效背景引用和配置。");
            if (!Finite(cue.EntryDuration) || cue.EntryDuration < 0f || !ValidCurve(cue.EntryEasing)
                || !Finite(cue.ScrollSpeed) || cue.ScrollSpeed < 0f || cue.Nodes == null || cue.Nodes.Length == 0)
                return FailedPlayback("循环配置无效，至少需要一个镜头节点。");

            double cycle = 0;
            foreach (var node in cue.Nodes)
            {
                if (node == null || !Finite(node.LocalPosition) || !Finite(node.LocalEulerAngles)
                    || !Finite(node.FieldOfView) || node.FieldOfView < 1f || node.FieldOfView > 179f
                    || !Finite(node.Duration) || node.Duration < 0f
                    || !Finite(node.HoldDuration) || node.HoldDuration < 0f || !ValidCurve(node.Easing))
                    return FailedPlayback("循环节点参数或曲线无效。");
                cycle += (double)node.Duration + node.HoldDuration;
            }
            if (cycle <= 0) return FailedPlayback("循环整轮总时长必须大于 0。");

            // 深复制节点和曲线，运行进度不写入共享资产。
            var nodes = new BackgroundLoopCue.Node[cue.Nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var source = cue.Nodes[i];
                nodes[i] = new BackgroundLoopCue.Node
                {
                    LocalPosition = source.LocalPosition, LocalEulerAngles = source.LocalEulerAngles,
                    FieldOfView = source.FieldOfView, Duration = source.Duration,
                    HoldDuration = source.HoldDuration, Easing = new AnimationCurve(source.Easing.keys)
                };
            }
            CaptureInitialState();
            CancelPlayback();
            CurrentPlayback = new BackgroundPlaybackHandle();
            _loopNodes = nodes;
            _loopDuration = cycle;
            _loopTime = 0;
            _loopEntering = true;
            _startPosition = _cameraRig.localPosition;
            _startRotation = _cameraRig.localRotation;
            _startFov = _backgroundCamera.fieldOfView;
            _startSpeed = _strip.Speed;
            _targetSpeed = cue.ScrollSpeed;
            SetLoopTarget(nodes[0]);
            _duration = cue.EntryDuration;
            _curve = new AnimationCurve(cue.EntryEasing.keys);
            if (_duration == 0f) TickLoop(0f);
            return CurrentPlayback;
        }

        void SetLoopTarget(BackgroundLoopCue.Node node)
        {
            _targetPosition = node.LocalPosition;
            _targetRotation = Quaternion.Euler(node.LocalEulerAngles);
            _targetFov = node.FieldOfView;
        }

        void ApplyLoopProgress(float time)
        {
            float progress = time >= 1f ? 1f : _curve.Evaluate(time);
            if (!Finite(progress))
            {
                CurrentPlayback.Fail("循环曲线产生无效进度。");
                Debug.LogWarning("[Background] 循环曲线产生无效进度，已停止播放。", this);
                return;
            }
            Apply(Mathf.Clamp01(progress));
        }

        void TickLoop(float deltaTime)
        {
            _loopTime += deltaTime;
            if (_loopEntering)
            {
                if (_loopTime < _duration)
                {
                    ApplyLoopProgress((float)(_loopTime / _duration));
                    return;
                }
                Apply(1f);
                _loopTime -= _duration;
                _loopEntering = false;
                _startSpeed = _targetSpeed;
            }
            // 单节点进入后保持，句柄仍可被后续指令接管。
            if (_loopNodes.Length == 1) return;

            // 每帧最多扫描一轮，长帧或极短节点不会产生无界追帧循环。
            _loopTime %= _loopDuration;
            double remaining = _loopTime;
            for (int i = 0; i < _loopNodes.Length; i++)
            {
                var source = _loopNodes[i];
                if (remaining < source.HoldDuration)
                {
                    SetLoopTarget(source);
                    Apply(1f);
                    return;
                }
                remaining -= source.HoldDuration;
                var destination = _loopNodes[(i + 1) % _loopNodes.Length];
                if (remaining < destination.Duration)
                {
                    _startPosition = source.LocalPosition;
                    _startRotation = Quaternion.Euler(source.LocalEulerAngles);
                    _startFov = source.FieldOfView;
                    SetLoopTarget(destination);
                    _curve = destination.Easing;
                    ApplyLoopProgress((float)(remaining / destination.Duration));
                    return;
                }
                remaining -= destination.Duration;
            }
            SetLoopTarget(_loopNodes[0]);
            Apply(1f);
        }
    }
}
