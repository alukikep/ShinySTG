using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.Background
{
    /// <summary>显式订阅指定关卡；禁用期间不接收事件，重新启用按当前关卡状态同步。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelController))]
    [DefaultExecutionOrder(-50)]
    public sealed class LevelBackgroundBinding : MonoBehaviour
    {
        [SerializeField, Tooltip("此关卡控制的背景。请在非播放模式配置。")]
        StageBackgroundController _background;

        LevelController _level;
        StageBackgroundController _boundBackground;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            _level = GetComponent<LevelController>();
            _boundBackground = _background;
            if (_boundBackground == null)
            {
                Debug.LogWarning("[Background] LevelBackgroundBinding 缺少背景引用。", this);
                return;
            }
            _level.OnLevelStart += OnLevelStart;
            _level.OnLevelComplete += OnLevelComplete;
            _level.OnBackgroundCueRequested += PlayCue;
            if (_level.IsCompleted) StopBackground();
            else if (_level.IsRunning) _boundBackground.ResetBackground();
        }

        void OnLevelStart(LevelDefinition definition)
        {
            if (_boundBackground != null) _boundBackground.ResetBackground();
        }

        void OnLevelComplete(LevelDefinition definition) => StopBackground();

        void PlayCue(BackgroundCue cue)
        {
            if (_boundBackground == null)
            {
                Debug.LogWarning("[Background] 绑定的背景已被销毁，跳过 Cue。", this);
                return;
            }
            var handle = _boundBackground.Play(cue);
            if (handle.Status == BackgroundPlaybackStatus.Failed)
                Debug.LogWarning($"[Background] 关卡 Cue 播放失败：{handle.Failure}", this);
        }

        public BackgroundPlaybackHandle PlayForRuntime(LevelRuntime runtime, BackgroundCue cue)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _level == null || !_level.IsRunning
                || runtime == null || runtime != _level.Runtime || _boundBackground == null)
                throw new System.InvalidOperationException("[Background] 背景动作需要启用的关卡绑定及当前真实关卡 Runtime。");
            return _boundBackground.Play(cue);
        }

        void StopBackground()
        {
            if (_boundBackground == null) return;
            _boundBackground.CancelPlayback();
            _boundBackground.Pause();
        }

        void OnDisable()
        {
            if (_level != null)
            {
                _level.OnLevelStart -= OnLevelStart;
                _level.OnLevelComplete -= OnLevelComplete;
                _level.OnBackgroundCueRequested -= PlayCue;
            }
            StopBackground();
            _level = null;
            _boundBackground = null;
        }

        [ContextMenu("Restart Bound Level (Play Mode)")]
        void RestartBoundLevel()
        {
            if (Application.isPlaying && isActiveAndEnabled && _level != null) _level.BeginLevel();
        }

        [ContextMenu("Complete Bound Level (Play Mode)")]
        void CompleteBoundLevel()
        {
            if (Application.isPlaying && isActiveAndEnabled && _level != null) _level.CompleteLevel();
        }
    }
}
