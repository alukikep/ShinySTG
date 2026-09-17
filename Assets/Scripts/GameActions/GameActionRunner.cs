using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.GameActions
{
    /// <summary>宿主负责 Tick/Dispose；每次 Play 返回可独立取消的句柄。</summary>
    public sealed class GameActionRunner : IDisposable
    {
        readonly List<GameActionHandle> _handles = new();
        public GameActionHandle Play(ActionSequence sequence, GameActionContext context)
        {
            var handle = new GameActionHandle(sequence?.Actions, context);
            _handles.Add(handle);
            handle.Advance();
            return handle;
        }
        public void Tick(float dt)
        {
            // 回调中可能添加或取消动作；本轮只推进开始时已存在的句柄。
            foreach (var handle in _handles.ToArray()) handle.Tick(Mathf.Max(0f, dt));
            _handles.RemoveAll(h => h.IsComplete);
        }
        public void Dispose()
        {
            foreach (var handle in _handles.ToArray()) handle.Cancel();
            _handles.Clear();
        }
    }

    public sealed class GameActionHandle
    {
        readonly GameAction[] _actions;
        readonly GameActionContext _context;
        GameActionRuntime _current;
        int _index;
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public Exception Failure { get; private set; }
        internal GameActionHandle(GameAction[] actions, GameActionContext context)
        {
            _actions = actions;
            _context = context;
        }
        internal void Advance()
        {
            try
            {
                while (!IsComplete && _current == null)
                {
                    if (_actions == null || _index >= _actions.Length) { IsComplete = true; return; }
                    _current = _actions[_index++]?.CreateRuntime(_context);
                    if (_current == null) continue;
                    _current.Start();
                    if (IsComplete) return;
                    if (!_current.IsComplete) return;
                    Release();
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        internal void Tick(float dt)
        {
            if (IsComplete) return;
            try
            {
                _current?.Tick(dt);
                if (IsComplete) return;
                if (_current == null || _current.IsComplete) { Release(); Advance(); }
            }
            catch (Exception ex) { Fail(ex); }
        }
        void Release()
        {
            var current = _current;
            _current = null;
            current?.Dispose();
        }
        void Fail(Exception ex)
        {
            Failure = ex;
            Debug.LogException(ex);
            Cancel();
        }
        public void Cancel()
        {
            if (IsComplete) return;
            IsComplete = true;
            IsCancelled = true;
            try { Release(); } catch (Exception ex) { Failure = ex; Debug.LogException(ex); }
        }
    }
}
