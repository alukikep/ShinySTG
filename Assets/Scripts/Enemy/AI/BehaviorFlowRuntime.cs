using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 行为流的运行时驱动器。每实例一份独立状态(_index / _elapsed 等),
    /// 即使多个敌人共享同一个 BehaviorFlow SO 资产也互不影响。
    ///
    /// 取代了旧 ShooterEnemy 内部的时间轴引擎。把那段逻辑提到独立类,
    /// 既能被 ShooterEnemy 用,也能被 BossPhase 用。
    /// </summary>
    public class BehaviorFlowRuntime
    {
        readonly BehaviorFlow _flow;

        int   _index = -1;
        float _elapsedInCurrent;
        float _delayLeft;
        bool  _started;

        public BehaviorFlowRuntime(BehaviorFlow flow)
        {
            _flow = flow;
            Reset();
        }

        /// <summary>重置为初始状态(从头开始)。</summary>
        public void Reset()
        {
            _index = -1;
            _elapsedInCurrent = 0f;
            _delayLeft = Mathf.Max(0f, _flow != null ? _flow.StartDelay : 0f);
            _started = false;
        }

        /// <summary>每帧由外部调用(owner 是行为作用的目标 transform)。</summary>
        public void Tick(Transform owner, float dt)
        {
            if (_flow == null) return;
            var actions = _flow.Actions;
            if (actions == null || actions.Length == 0) return;

            // 1. 启动延迟
            if (!_started)
            {
                _delayLeft -= dt;
                if (_delayLeft > 0f) return;
                _started = true;
                AdvanceTo(0, owner);
            }

            var current = actions[_index];
            if (current == null)
            {
                AdvanceTo(_index + 1, owner);
                return;
            }

            current.OnTick(owner, dt);
            _elapsedInCurrent += dt;

            // 2. 到时间,切下一条
            if (_elapsedInCurrent >= current.Duration)
            {
                current.OnExit(owner);
                AdvanceTo(_index + 1, owner);
            }
        }

        /// <summary>
        /// 强制退出当前 action(比如 ShooterPhase.OnExit 调用),
        /// 不切下一条,只清理状态。
        /// </summary>
        public void ForceExit(Transform owner)
        {
            if (_flow == null) return;
            var actions = _flow.Actions;
            if (actions == null || _index < 0 || _index >= actions.Length) return;
            actions[_index]?.OnExit(owner);
        }

        void AdvanceTo(int next, Transform owner)
        {
            var actions = _flow.Actions;
            if (actions == null) return;

            if (next >= actions.Length)
            {
                if (_flow.Loop && actions.Length > 0) next = 0;
                else { _index = -1; return; } // 序列结束
            }

            _index = next;
            _elapsedInCurrent = 0f;
            if (actions[_index] != null) actions[_index].OnEnter(owner);
        }
    }
}
