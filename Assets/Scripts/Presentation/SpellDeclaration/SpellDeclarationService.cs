using System;
using ShinySTG.Audio;
using UnityEngine;

namespace ShinySTG.Presentation.SpellDeclaration
{
    public sealed class SpellDeclarationService : MonoBehaviour
    {
        [SerializeField, Tooltip("本场景复用的符卡宣言视图。")]
        SpellDeclarationView _view;
        SpellDeclarationHandle _active;
        float _elapsed, _enter, _hold, _exit, _distance;

        public bool IsPlaying => _active != null;

        public SpellDeclarationHandle Play(SpellDeclarationDefinition definition)
        {
            var handle = new SpellDeclarationHandle();
            if (IsPlaying) return Reject(handle, "已有符卡宣言正在播放。");
            if (!isActiveAndEnabled) return Reject(handle, "符卡宣言服务未启用。");
            if (definition == null) return Reject(handle, "未指定符卡宣言配置。");
            if (definition.Sfx != null && definition.Sfx.Loop) return Reject(handle, "宣言音效不能使用循环 Cue。");
            if (_view == null) return Reject(handle, $"服务 '{name}' 的 View 未绑定 SpellDeclarationView 组件。");
            if (!_view.IsReady) return Reject(handle, $"视图 '{_view.name}' 不可用：{_view.UnavailableReason}");
            if (!Valid(definition.EnterDuration) || !Valid(definition.HoldDuration)
                || !Valid(definition.ExitDuration) || !Valid(definition.SlideDistance)
                || !Valid(definition.EnterDuration + definition.HoldDuration + definition.ExitDuration))
                return Reject(handle, "宣言时长和滑动距离必须为有限的非负数。");

            _active = handle;
            handle.Bind(() => { if (_active == handle) End(true); });
            _elapsed = 0f;
            // 保存本次播放快照，运行中修改配置不会改变当前播放。
            _enter = definition.EnterDuration;
            _hold = definition.HoldDuration;
            _exit = definition.ExitDuration;
            _distance = definition.SlideDistance;
            try
            {
                _view.Show(definition.DisplayName, definition.Portrait, definition.TitleImage);
                AudioMix.PlaySfx(definition.Sfx);
                Advance(0f);
            }
            catch (Exception exception) { End(failure: exception); }
            return handle;
        }

        static bool Valid(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        static SpellDeclarationHandle Reject(SpellDeclarationHandle handle, string message)
        {
            handle.Finish(failure: new InvalidOperationException("[Spell Declaration] " + message));
            return handle;
        }

        // 使用游戏时间，与 Boss 阶段和暂停保持一致；不由多个动作重复推进同一服务。
        void Update() => Advance(Time.deltaTime);

        internal void Advance(float dt)
        {
            if (_active == null) return;
            if (_view == null || !_view.IsReady)
            {
                End(failure: new InvalidOperationException("[Spell Declaration] "
                    + (_view == null ? "播放中的视图已销毁。" : _view.UnavailableReason)));
                return;
            }
            _elapsed += Mathf.Max(0f, dt);
            try
            {
                if (_elapsed >= _enter + _hold + _exit) { End(); return; }
                if (_elapsed < _enter)
                {
                    float t = Mathf.SmoothStep(0f, 1f, _elapsed / _enter);
                    _view.Render(t, _distance * (1f - t));
                }
                else if (_elapsed < _enter + _hold) _view.Render(1f, 0f);
                else
                {
                    float t = Mathf.SmoothStep(0f, 1f, (_elapsed - _enter - _hold) / _exit);
                    _view.Render(1f - t, -_distance * t);
                }
            }
            catch (Exception exception) { End(failure: exception); }
        }

        void End(bool cancelled = false, Exception failure = null)
        {
            var handle = _active;
            _active = null;
            _elapsed = _enter = _hold = _exit = _distance = 0f;
            handle?.Finish(cancelled, failure);
            if (_view != null) _view.Clear();
        }
        void OnDisable() => End(true);
        void OnDestroy() => End(true);
    }
}
