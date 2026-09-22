using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.EnemyAI.Boss;

namespace ShinySTG.EnemyAI
{
    public enum InvincibilityTarget
    {
        Owner,
        Player,
        OwnerAndPlayer
    }

    /// <summary>
    /// Behaviour Flow 时间轴中的临时无敌作用域。窗口时长与 Children 取较晚结束点。
    /// </summary>
    [Serializable, SRName("Action/Invincibility")]
    public sealed class InvincibilityAction : EnemyAction
    {
        [Tooltip("Owner = 行为流宿主上的 EnemyHealth 或 BossHealth；Player = 当前玩家；OwnerAndPlayer = 两者。")]
        public InvincibilityTarget Target = InvincibilityTarget.Owner;

        [SerializeReference, SR]
        [Tooltip("无敌期间执行的行为。窗口会持续到 Duration 与所有子行为都完成。")]
        public EnemyAction[] Children;

        [NonSerialized] string _runtimeKey;
        [NonSerialized] ShinySTG.Player.PlayerHealth _player;
        [NonSerialized] EnemyHealth _enemy;
        [NonSerialized] BossHealth _boss;
        [NonSerialized] bool _active;
        [NonSerialized] int _index;
        [NonSerialized] float _elapsed;
        [NonSerialized] bool _childrenComplete;
        [NonSerialized] bool _complete;

        public override bool IsComplete => _complete;
        public override bool UsesDuration => false;

        public override void OnEnter(Transform owner)
        {
            _runtimeKey ??= "behavior_flow_invincibility_" + Guid.NewGuid().ToString("N");
            CacheTargets(owner);

            if (Target == InvincibilityTarget.Player || Target == InvincibilityTarget.OwnerAndPlayer)
                _player?.AddInvincibility(_runtimeKey);
            if (Target == InvincibilityTarget.Owner || Target == InvincibilityTarget.OwnerAndPlayer)
            {
                _enemy?.AddInvincibility(_runtimeKey);
                _boss?.AddInvincibility(_runtimeKey);
            }
            _active = true;
            _complete = false;
            _index = -1;
            _elapsed = 0f;
            _childrenComplete = Children == null || Children.Length == 0;
            Advance(0, owner);
            EvaluateCompletion();
        }

        public override void OnTick(Transform owner, float dt)
        {
            _elapsed += dt;
            if (!_childrenComplete && Children != null && Children.Length > 0)
            {
                if (_index < 0) Advance(0, owner);
                if (_index >= 0)
                {
                    var child = Children[_index];
                    if (child == null) Advance(_index + 1, owner);
                    else
                    {
                        child.OnTick(owner, dt);
                        if (owner == null || !owner.gameObject.activeInHierarchy) return;
                        _childElapsed += dt;
                        if (child.IsComplete || _childElapsed >= child.CurrentDuration)
                        {
                            int next = _index + 1;
                            _index = -1;
                            child.OnExit(owner);
                            Advance(next, owner);
                        }
                    }
                }
            }
            EvaluateCompletion();
        }

        public override void OnExit(Transform owner)
        {
            if (_index >= 0 && Children != null && _index < Children.Length)
            {
                var child = Children[_index];
                _index = -1;
                child?.OnExit(owner);
            }
            if (!_active) return;
            _active = false;
            _player?.RemoveInvincibility(_runtimeKey);
            _enemy?.RemoveInvincibility(_runtimeKey);
            _boss?.RemoveInvincibility(_runtimeKey);
            _player = null;
            _enemy = null;
            _boss = null;
            _index = -1;
            _childrenComplete = true;
            _complete = true;
        }

        void Advance(int next, Transform owner)
        {
            if (Children == null || next >= Children.Length)
            {
                _index = -1;
                _childrenComplete = true;
                return;
            }
            _index = next;
            _childElapsed = 0f;
            var child = Children[_index];
            if (child == null) { Advance(next + 1, owner); return; }
            child.SetCurrentDuration(child.ResolveDuration());
            child.OnEnter(owner);
            if (child.IsComplete) { child.OnExit(owner); Advance(next + 1, owner); }
        }

        [NonSerialized] float _childElapsed;

        void EvaluateCompletion()
        {
            bool durationComplete = CurrentDuration <= 0f || _elapsed >= CurrentDuration;
            _complete = durationComplete && _childrenComplete;
        }

        void CacheTargets(Transform owner)
        {
            _player = null;
            _enemy = null;
            _boss = null;
            if (Target == InvincibilityTarget.Player) { _player = ShinySTG.Player.Player.Instance?.Health; return; }
            if (Target == InvincibilityTarget.OwnerAndPlayer) _player = ShinySTG.Player.Player.Instance?.Health;
            if (owner == null) return;
            _enemy = owner.GetComponent<EnemyHealth>();
            _boss = owner.GetComponent<BossHealth>();
        }
    }
}
