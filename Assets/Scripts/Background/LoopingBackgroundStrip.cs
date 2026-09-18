using System;
using UnityEngine;

namespace ShinySTG.Background
{
    /// <summary>沿局部 -Z 循环排列静态路段。直接子物体按层级顺序排列。</summary>
    [DisallowMultipleComponent]
    public sealed class LoopingBackgroundStrip : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("每段沿局部 Z 的长度；运行中不可改变布局参数。")]
        float _segmentLength = 32f;
        [SerializeField, Tooltip("循环区间后端的局部 Z；须位于背景相机视野后方。")]
        float _rearEdge = -48f;
        [SerializeField, Min(0f), Tooltip("向局部 -Z 滚动的速度，单位/秒。")]
        float _speed = 8f;
        [SerializeField, Tooltip("仅暂停背景；Time.timeScale 为 0 时也会暂停。")]
        bool _paused;

        Transform[] _segments;
        Vector3[] _positions;
        Quaternion[] _rotations;
        Vector3[] _scales;
        double _distance;
        float _runtimeLength;
        float _runtimeRear;
        double _visualElapsed;
        BackgroundUvPlayback _uvPlayback;

        public float Speed
        {
            get => _speed;
            set => _speed = IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        public bool IsPaused => _paused;
        public bool IsLayoutValid => transform.childCount >= 2 && IsFinite(_segmentLength)
            && _segmentLength > 0f && IsFinite(_rearEdge);

        void Awake() => Initialize();

        bool Initialize()
        {
            if (_segments != null) return true;
            int count = transform.childCount;
            if (count < 2 || !IsFinite(_segmentLength) || _segmentLength <= 0f || !IsFinite(_rearEdge))
            {
                Debug.LogError("[Background] 循环背景需要至少两个直接子路段、有效的正长度和后端位置。", this);
                enabled = false;
                return false;
            }

            _runtimeLength = _segmentLength;
            _runtimeRear = _rearEdge;
            _segments = new Transform[count];
            _positions = new Vector3[count];
            _rotations = new Quaternion[count];
            _scales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var segment = transform.GetChild(i);
                _segments[i] = segment;
                _positions[i] = segment.localPosition;
                _rotations[i] = segment.localRotation;
                _scales[i] = segment.localScale;
            }
            _uvPlayback = new BackgroundUvPlayback(transform);
            _uvPlayback.Apply(0d);
            ApplyPositions();
            return true;
        }

        void Update() => Advance(Time.deltaTime);

        // 使用总路程取模，单帧跨越多个路段甚至整个循环也不会产生间隙。
        public void Advance(float deltaTime)
        {
            if (!isActiveAndEnabled || _paused || !IsFinite(deltaTime) || deltaTime <= 0f || !Initialize()) return;
            _visualElapsed += deltaTime;
            _uvPlayback.Apply(_visualElapsed);
            if (!IsFinite(_speed) || _speed <= 0f) return;
            double cycle = (double)_runtimeLength * _segments.Length;
            _distance = (_distance + (double)_speed * deltaTime) % cycle;
            ApplyPositions();
        }

        void ApplyPositions()
        {
            double cycle = (double)_runtimeLength * _segments.Length;
            for (int i = 0; i < _segments.Length; i++)
            {
                var segment = _segments[i];
                if (segment == null)
                {
                    Debug.LogError("[Background] 循环路段已被删除，请重新加载场景恢复完整布景。", this);
                    enabled = false;
                    return;
                }
                double offset = ((double)i * _runtimeLength - _distance) % cycle;
                if (offset < 0d) offset += cycle;
                var position = _positions[i];
                // 路段中心可位于后端之后半段，确保回收时整段已离开后端。
                position.z = (float)(_runtimeRear - _runtimeLength * 0.5d + offset);
                segment.localPosition = position;
            }
        }

        [ContextMenu("Pause Background")]
        public void Pause() => _paused = true;

        [ContextMenu("Resume Background")]
        public void Resume() => _paused = false;

        /// <summary>恢复路段根变换与循环相位，保留当前速度和暂停状态。</summary>
        [ContextMenu("Reset Background")]
        public void ResetBackground()
        {
            if (!Application.isPlaying || !Initialize()) return;
            _distance = 0d;
            _visualElapsed = 0d;
            _uvPlayback.Apply(0d);
            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i] == null) continue;
                _segments[i].localRotation = _rotations[i];
                _segments[i].localScale = _scales[i];
            }
            ApplyPositions();
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void OnDestroy() => _uvPlayback?.Restore();
    }
}
