using UnityEngine;

namespace ShinySTG.Background
{
    /// <summary>仅控制背景镜头与速度；不修改战斗相机，不推进循环组件的时间。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed partial class StageBackgroundController : MonoBehaviour
    {
        [SerializeField, Tooltip("本背景根节点之下的 CameraRig，不可指定本物体。")]
        Transform _cameraRig;
        [SerializeField, Tooltip("CameraRig 下的透视背景相机，不能使用 MainCamera。")]
        Camera _backgroundCamera;
        [SerializeField, Tooltip("本背景下的循环布景组件。")]
        LoopingBackgroundStrip _strip;

        Vector3 _startPosition;
        Quaternion _startRotation;
        float _startFov;
        float _startSpeed;
        Vector3 _targetPosition;
        Quaternion _targetRotation;
        float _targetFov;
        float _targetSpeed;
        float _duration;
        float _elapsed;
        AnimationCurve _curve;

        Vector3 _initialPosition;
        Quaternion _initialRotation;
        float _initialFov;
        float _initialSpeed;
        bool _initialPaused;
        bool _hasInitialState;

        public BackgroundPlaybackHandle CurrentPlayback { get; private set; }
        public bool IsTransitioning => CurrentPlayback != null && !CurrentPlayback.IsComplete;
        public bool IsPaused => _strip != null && _strip.IsPaused;

        void Awake() => CaptureInitialState();

        bool CaptureInitialState()
        {
            if (_hasInitialState) return true;
            if (!HasValidBindings()) return false;
            _initialPosition = _cameraRig.localPosition;
            _initialRotation = _cameraRig.localRotation;
            _initialFov = _backgroundCamera.fieldOfView;
            _initialSpeed = _strip.Speed;
            _initialPaused = _strip.IsPaused;
            _initialStrip = _strip;
            _initialContentActive = _strip.gameObject.activeSelf;
            _initialClearColor = _backgroundCamera.backgroundColor;
            _initialClearFlags = _backgroundCamera.clearFlags;
            _hasInitialState = true;
            return true;
        }

        public BackgroundPlaybackHandle Play(BackgroundCue cue)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || cue == null)
                return FailedPlayback("播放需要启用的运行时控制器和有效 Cue。");
            if (!HasValidBindings())
            {
                Debug.LogWarning("[Background] 请在背景根节点绑定其子 CameraRig、透视背景相机和循环布景。相机须排除战斗层，仅渲染 Background3D。", this);
                return FailedPlayback("背景引用无效。");
            }
            string error = CueValidationError(cue);
            if (error != null)
            {
                string reason = $"Cue '{cue.name}'：{error}";
                Debug.LogWarning($"[Background] {reason}", cue);
                return FailedPlayback(reason);
            }

            CaptureInitialState();
            CancelPlayback();
            CurrentPlayback = new BackgroundPlaybackHandle();
            _startPosition = _cameraRig.localPosition;
            _startRotation = _cameraRig.localRotation;
            _startFov = _backgroundCamera.fieldOfView;
            _startSpeed = _strip.Speed;
            _targetPosition = cue.LocalPosition;
            _targetRotation = Quaternion.Euler(cue.LocalEulerAngles);
            _targetFov = cue.FieldOfView;
            _targetSpeed = cue.ScrollSpeed;
            _duration = cue.Duration;
            // 快照避免播放中编辑共享资产改变已经启动的过渡。
            _curve = new AnimationCurve(cue.Easing.keys);
            _elapsed = 0f;
            if (_duration == 0f)
            {
                Apply(1f);
                CurrentPlayback.Complete();
            }
            return CurrentPlayback;
        }

        static BackgroundPlaybackHandle FailedPlayback(string reason)
        {
            var handle = new BackgroundPlaybackHandle();
            handle.Fail(reason);
            return handle;
        }

        void Update()
        {
            if (_switching) { TickSwitch(); return; }
            if (!IsTransitioning) return;
            if (!HasValidBindings())
            {
                CurrentPlayback.Fail("背景引用失效。");
                Debug.LogWarning("[Background] 背景引用失效，已停止当前过渡。", this);
                return;
            }
            if (!_strip.isActiveAndEnabled || _strip.IsPaused || Time.deltaTime <= 0f) return;
            if (_loopNodes != null) { TickLoop(Time.deltaTime); return; }
            _elapsed = Mathf.Min(_elapsed + Time.deltaTime, _duration);
            float progress = _elapsed >= _duration ? 1f : _curve.Evaluate(_elapsed / _duration);
            if (!Finite(progress))
            {
                CurrentPlayback.Fail("曲线产生无效进度。");
                Debug.LogWarning("[Background] 曲线计算产生无效值，已停止当前过渡。", this);
                return;
            }
            Apply(Mathf.Clamp01(progress));
            if (_elapsed >= _duration) CurrentPlayback.Complete();
        }

        void Apply(float progress)
        {
            _cameraRig.localPosition = Vector3.Lerp(_startPosition, _targetPosition, progress);
            _cameraRig.localRotation = Quaternion.Slerp(_startRotation, _targetRotation, progress);
            _backgroundCamera.fieldOfView = Mathf.Lerp(_startFov, _targetFov, progress);
            _strip.Speed = Mathf.Lerp(_startSpeed, _targetSpeed, progress);
        }

        public void Pause()
        {
            if (Application.isPlaying && HasValidBindings() && CaptureInitialState()) _strip.Pause();
        }

        public void Resume()
        {
            if (Application.isPlaying && HasValidBindings() && CaptureInitialState()) _strip.Resume();
        }

        public void CancelPlayback()
        {
            CurrentPlayback?.Cancel();
            _loopNodes = null;
            _loopTime = 0;
            _loopDuration = 0;
            _loopEntering = false;
            _switching = false;
            SetFade(0f);
        }

        public void ResetBackground()
        {
            if (!Application.isPlaying) return;
            CancelPlayback();
            RestoreInitialContent();
            if (!HasValidBindings() || !CaptureInitialState())
            {
                Debug.LogWarning("[Background] 引用无效，无法恢复初始状态。", this);
                return;
            }
            _cameraRig.localPosition = _initialPosition;
            _cameraRig.localRotation = _initialRotation;
            _backgroundCamera.fieldOfView = _initialFov;
            _strip.Speed = _initialSpeed;
            _strip.ResetBackground();
            if (_initialPaused) _strip.Pause(); else _strip.Resume();
            _elapsed = 0f;
        }

        void OnDisable() => CancelPlayback();
        void OnDestroy()
        {
            CancelPlayback();
            RestoreInitialContent();
            if (_fadeCanvas != null) Destroy(_fadeCanvas.gameObject);
        }

        bool HasValidBindings()
        {
            int layer = LayerMask.NameToLayer("Background3D");
            return layer >= 0 && _cameraRig != null && _cameraRig != transform
                && _cameraRig.IsChildOf(transform) && _backgroundCamera != null
                && _backgroundCamera.transform.IsChildOf(_cameraRig)
                && !_backgroundCamera.orthographic && !_backgroundCamera.CompareTag("MainCamera")
                && _backgroundCamera.cullingMask == (1 << layer)
                && _strip != null && _strip.transform.IsChildOf(transform);
        }

        static string CueValidationError(BackgroundCue cue)
        {
            if (!Finite(cue.LocalPosition)) return "LocalPosition 含非有限数值。";
            if (!Finite(cue.LocalEulerAngles)) return "LocalEulerAngles 含非有限数值。";
            if (!Finite(cue.FieldOfView) || cue.FieldOfView < 1f || cue.FieldOfView > 179f)
                return $"FieldOfView 必须在 1~179 之间，当前为 {cue.FieldOfView}。";
            if (!Finite(cue.ScrollSpeed) || cue.ScrollSpeed < 0f)
                return $"ScrollSpeed 必须是非负有限数值，当前为 {cue.ScrollSpeed}。";
            if (!Finite(cue.Duration) || cue.Duration < 0f)
                return $"Duration 必须是非负有限数值，当前为 {cue.Duration}。";
            if (ValidCurve(cue.Easing)) return null;
            if (cue.Easing == null || cue.Easing.length < 2)
                return "Easing 至少需要两个关键帧，起点 (0,0)，终点 (1,1)。";
            var keys = cue.Easing.keys;
            var first = keys[0];
            var last = keys[keys.Length - 1];
            return $"Easing 关键帧数值无效或端点不符合要求：起点应为 (0,0)，实际 ({first.time},{first.value})；"
                + $"终点应为 (1,1)，实际 ({last.time},{last.value})。";
        }

        static bool ValidCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length < 2) return false;
            var keys = curve.keys;
            foreach (var key in keys)
                if (!Finite(key.time) || !Finite(key.value) || float.IsNaN(key.inTangent)
                    || float.IsNaN(key.outTangent) || !Finite(key.inWeight) || !Finite(key.outWeight)) return false;
            return Mathf.Approximately(keys[0].time, 0f) && Mathf.Approximately(keys[0].value, 0f)
                && Mathf.Approximately(keys[keys.Length - 1].time, 1f)
                && Mathf.Approximately(keys[keys.Length - 1].value, 1f);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
