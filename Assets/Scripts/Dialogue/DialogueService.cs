using System;
using UnityEngine;

namespace ShinySTG.Dialogue
{
    /// <summary>场景内单会话播放器；可锁定玩家控制，不暂停世界或提供无敌。</summary>
    public sealed class DialogueService : MonoBehaviour
    {
        [SerializeField, Tooltip("本场景的对话 UI。")]
        DialogueView _view;
        [SerializeField, Min(0f), Tooltip("每秒显示的字符数；0 表示立即显示全文。")]
        float _charactersPerSecond = 35f;
        [SerializeField, Min(0.05f), Tooltip("按住快进时，每句台词至少保留的秒数。")]
        float _fastForwardInterval = 0.15f;

        DialogueHandle _active;
        IDisposable _controlLock;
        DialogueLine[] _lines;
        int _lineIndex;
        float _visibleCharacters;
        float _fastForwardElapsed;
        bool _fastForwardHeld;
        int _lastConfirmFrame = -1;

        public DialogueHandle ActiveHandle => _active;
        public bool IsPlaying => _active != null;

        public DialogueHandle Play(DialogueDefinition definition, bool lockPlayerControls = true)
        {
            var handle = new DialogueHandle();
            if (IsPlaying) return Reject(handle, "已有对话正在播放，不能覆盖当前会话。");
            if (!isActiveAndEnabled) return Reject(handle, "DialogueService 未启用。");
            if (definition == null) return Reject(handle, "未指定 DialogueDefinition。");
            // 空对话不依赖 UI，立即完成。
            if (definition.Lines == null || !Array.Exists(definition.Lines, line => line != null))
            {
                handle.Finish();
                return handle;
            }
            if (_view == null || !_view.IsReady) return Reject(handle, "对话 UI 不可用，请检查 View、CanvasGroup 和正文引用。");

            _active = handle;
            handle.Bind(() => { if (_active == handle) End(true); });
            // 复制会话数据，不向共享 SO 写入播放状态。
            _lines = Array.ConvertAll(definition.Lines, line => line == null ? null : new DialogueLine
            {
                Character = line.Character, Side = line.Side, Text = line.Text, PortraitOverride = line.PortraitOverride
            });
            _lineIndex = -1;
            _fastForwardHeld = false;
            _fastForwardElapsed = 0f;
            _lastConfirmFrame = Time.frameCount;
            try
            {
                if (lockPlayerControls) _controlLock = ShinySTG.Player.PlayerControlLock.Acquire();
                _view.Show();
                Advance();
            }
            catch (Exception exception) { End(false, exception); }
            return handle;
        }

        void Update()
        {
            if (ShinySTG.GameFlow.GameplayPause.IsPaused) return;
            if (!IsPlaying) return;
            if (_view == null || !_view.IsReady)
            {
                End(false, new InvalidOperationException("播放中的对话 UI 已被禁用或销毁。"));
                return;
            }
            try
            {
                float dt = Time.unscaledDeltaTime;
                _visibleCharacters = _charactersPerSecond <= 0f ? _view.CharacterCount
                    : Mathf.Min(_view.CharacterCount, _visibleCharacters + _charactersPerSecond * dt);
                _view.SetVisibleCharacters(Mathf.FloorToInt(_visibleCharacters));
                if (!_fastForwardHeld) return;
                Reveal();
                _fastForwardElapsed += dt;
                if (_fastForwardElapsed >= Mathf.Max(0.05f, _fastForwardInterval)) Advance();
            }
            catch (Exception exception) { End(false, exception); }
        }

        public void Confirm()
        {
            if (ShinySTG.GameFlow.GameplayPause.BlocksDialogueInput) return;
            if (!IsPlaying || _lastConfirmFrame == Time.frameCount) return;
            _lastConfirmFrame = Time.frameCount;
            try
            {
                if (_view == null || !_view.IsReady) throw new InvalidOperationException("对话 UI 不可用。");
                if (_visibleCharacters < _view.CharacterCount) Reveal();
                else Advance();
            }
            catch (Exception exception) { End(false, exception); }
        }

        public void SetFastForward(bool held)
        {
            _fastForwardHeld = IsPlaying && held && !ShinySTG.GameFlow.GameplayPause.BlocksDialogueInput;
            if (!_fastForwardHeld) _fastForwardElapsed = 0f;
        }

        void Reveal()
        {
            _visibleCharacters = _view.CharacterCount;
            _view.SetVisibleCharacters(_view.CharacterCount);
        }

        void Advance()
        {
            do { _lineIndex++; } while (_lineIndex < _lines.Length && _lines[_lineIndex] == null);
            if (_lineIndex >= _lines.Length) { End(); return; }
            _fastForwardElapsed = 0f;
            _visibleCharacters = 0f;
            _view.ShowLine(_lines[_lineIndex]);
            if (_charactersPerSecond <= 0f) Reveal();
        }

        DialogueHandle Reject(DialogueHandle handle, string message)
        {
            var exception = new InvalidOperationException(message);
            handle.Finish(failure: exception);
            Debug.LogException(exception, this);
            return handle;
        }

        void End(bool cancelled = false, Exception failure = null)
        {
            var handle = _active;
            _active = null;
            _lines = null;
            _lineIndex = -1;
            _visibleCharacters = 0f;
            _fastForwardElapsed = 0f;
            _fastForwardHeld = false;
            _controlLock?.Dispose();
            _controlLock = null;
            handle?.Finish(cancelled, failure);
            if (_view != null) _view.Hide();
            if (failure != null) Debug.LogException(failure, this);
        }

        void OnDisable() => End(true);
        void OnDestroy() => End(true);
        void OnApplicationFocus(bool focused) { if (!focused) SetFastForward(false); }
    }
}
