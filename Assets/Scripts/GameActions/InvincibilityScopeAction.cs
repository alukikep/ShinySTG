using System;
using SerializeReferenceEditor;
using ShinySTG.GameplayCommands;
using ShinySTG.Player;
using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Invincibility Scope")]
    public sealed class InvincibilityScopeAction : GameAction
    {
        [Tooltip("保护当前玩家。")]
        public bool ProtectPlayer = true;
        [Tooltip("保护指令发起者。")]
        public bool ProtectOwner;
        [Tooltip("保护期间执行的动作；结束或取消自动解除本次保护。")]
        public ActionSequence Sequence = new();
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(this, context);
        sealed class Runtime : GameActionRuntime
        {
            readonly InvincibilityScopeAction _config;
            readonly GameActionContext _context;
            readonly string _key = "action_scope_" + Guid.NewGuid().ToString("N");
            readonly GameActionRunner _runner = new();
            PlayerHealth _player, _ownerPlayer;
            EnemyHealth _enemy;
            BossHealth _boss;
            GameActionHandle _handle;
            public Runtime(InvincibilityScopeAction config, GameActionContext context) { _config = config; _context = context; }
            public override void Start()
            {
                if (_config.ProtectPlayer && ShinySTG.Player.Player.Instance != null) _player = ShinySTG.Player.Player.Instance.Health;
                if (_config.ProtectOwner && _context.Owner != null)
                {
                    _ownerPlayer = _context.Owner.GetComponent<PlayerHealth>();
                    _enemy = _context.Owner.GetComponent<EnemyHealth>();
                    _boss = _context.Owner.GetComponent<BossHealth>();
                }
                _player?.AddInvincibility(_key); _ownerPlayer?.AddInvincibility(_key);
                _enemy?.AddInvincibility(_key); _boss?.AddInvincibility(_key);
                _handle = _runner.Play(_config.Sequence, _context);
            }
            public override bool IsComplete
            {
                get { if (_handle?.Failure != null) throw _handle.Failure; return _handle != null && _handle.IsComplete; }
            }
            public override void Tick(float dt) => _runner.Tick(dt);
            public override void Dispose()
            {
                _runner.Dispose();
                if (_player != null) _player.RemoveInvincibility(_key);
                if (_ownerPlayer != null) _ownerPlayer.RemoveInvincibility(_key);
                if (_enemy != null) _enemy.RemoveInvincibility(_key);
                if (_boss != null) _boss.RemoveInvincibility(_key);
            }
        }
    }
}
