using System;
using System.Collections.Generic;
using UnityEngine;
using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;
using ShinySTG.Hitbox;
using ShinySTG.GameActions;

namespace ShinySTG.Player
{
    public enum BombState { Ready, Starting, Active, Ending }

    [DisallowMultipleComponent]
    public sealed class PlayerBomb : MonoBehaviour
    {
        [SerializeField] BombDefinition _definition;
        [Header("Legacy fallback (used when Definition is empty)")]
        [SerializeField] bool _clearEnemyProjectiles = true;
        [SerializeField] bool _includeLasers = true;
        [SerializeField] bool _damageAllEnemies;
        [Min(0f), SerializeField] float _enemyDamage;
        [Min(0f), SerializeField] float _invincibilityDuration = 2f;
        [Min(0f), SerializeField] float _duration = 2f;
        [Min(0f), SerializeField] float _damageInterval;
        [SerializeField] ShinySTG.Audio.SfxCue _bombSfx;

        public BombState State { get; private set; } = BombState.Ready;
        public bool IsActive => State != BombState.Ready;
        public event Action OnBombUsed;
        public event Action<BombState> OnStateChanged;

        float _stateRemaining, _invincibilityRemaining, _damageTimer;
        GameActionRunner _actionRunner;
        GameActionHandle _activeHandle;
        readonly HashSet<Bullet> _spawnedBullets = new();
        readonly HashSet<Bullet> _beforeFireBullets = new();
        const string InvincibilityKey = "PlayerBomb";
        bool ClearProjectiles => _definition != null ? _definition.ClearEnemyProjectiles : _clearEnemyProjectiles;
        bool IncludeLasers => _definition != null ? _definition.IncludeLasers : _includeLasers;
        bool DamageAll => _definition != null ? _definition.DamageAllEnemies : _damageAllEnemies;
        float Damage => _definition != null ? _definition.EnemyDamage : _enemyDamage;
        float Duration => Mathf.Max(0f, _definition != null ? _definition.Duration : _duration);
        float InvincibilityDuration => Mathf.Max(0f, _definition != null ? _definition.InvincibilityDuration : _invincibilityDuration);
        float DamageInterval => Mathf.Max(0f, _definition != null ? _definition.DamageInterval : _damageInterval);
        ShinySTG.Audio.SfxCue BombSfx => _definition != null ? _definition.BombSfx : _bombSfx;
        bool ReturnSpawnedBullets => _definition == null || _definition.ReturnSpawnedBulletsOnEnd;
        internal float DeathbombWindow => _definition != null ? Mathf.Max(0f, _definition.DeathbombWindow) : 0.1f;

        internal void CaptureBulletsBeforeBombFire()
        {
            _beforeFireBullets.Clear();
            if (BulletPool.Instance == null) return;
            foreach (var bullet in BulletPool.Instance.ActiveBullets) if (bullet != null) _beforeFireBullets.Add(bullet);
        }

        internal void CaptureBulletsAfterBombFire()
        {
            if (BulletPool.Instance == null) return;
            foreach (var bullet in BulletPool.Instance.ActiveBullets)
                if (bullet != null && !_beforeFireBullets.Contains(bullet)) _spawnedBullets.Add(bullet);
        }

        void Update()
        {
            if (State == BombState.Ready || ShinySTG.GameFlow.GameplayPause.IsPaused) return;
            var dt = Time.deltaTime;
            _actionRunner?.Tick(dt);
            TickInvincibility(dt);
            if (State == BombState.Active && DamageAll && Damage > 0f && DamageInterval > 0f)
            {
                _damageTimer -= dt;
                if (_damageTimer <= 0f) { ApplyDamage(); _damageTimer += DamageInterval; }
            }
            _stateRemaining -= dt;
            if (_stateRemaining > 0f) return;
            if (State == BombState.Starting) EnterState(BombState.Active, Duration);
            else if (State == BombState.Active) EnterState(BombState.Ending, 0f);
            else ResetBomb();
        }

        public bool TryUse()
        {
            var player = Player.Instance;
            if (State != BombState.Ready || player == null || player.Health == null || (!player.Health.CanInteract && !player.Health.IsDeathbombWindowOpen)
                || ShinySTG.GameFlow.GameplayPause.IsPaused || ShinySTG.Level.BattleRestriction.IsActive
                || PlayerControlLock.IsLocked || player.Resources == null || !player.Resources.TryConsumeBomb()) return false;
            bool deathbomb = player.Health.IsDeathbombWindowOpen;
            if (deathbomb && !player.Health.TryResolveDeathbomb()) return false;
            if (ClearProjectiles)
            {
                BulletPool.Instance?.ReturnAll(team => team == CollisionTeam.Enemy);
                if (IncludeLasers) ShinySTG.Laser.LaserPool.Instance?.ReturnAll(team => team == CollisionTeam.Enemy);
            }
            EnterState(BombState.Starting, 0f);
            ApplyDamage();
            _damageTimer = DamageInterval;
            _invincibilityRemaining = InvincibilityDuration;
            if (_invincibilityRemaining > 0f) player.Health.AddInvincibility(InvincibilityKey);
            if (BombSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(BombSfx, position: (Vector2)transform.position);
            OnBombUsed?.Invoke();
            _actionRunner = new GameActionRunner();
            var context = new GameActionContext(player.transform);
            if (_definition?.StartActions?.Actions != null)
                _actionRunner.Play(_definition.StartActions, context);
            EnterState(BombState.Active, Duration);
            if (_definition?.ActiveActions?.Actions != null)
                _activeHandle = _actionRunner.Play(_definition.ActiveActions, context);
            return true;
        }

        void ApplyDamage()
        {
            if (!DamageAll || Damage <= 0f) return;
            var enemies = new List<EnemyHealth>(EnemyHealth.Alive);
            for (int i = 0; i < enemies.Count; i++) enemies[i]?.TakeDamage(Damage);
            var bosses = new List<BossHealth>(BossHealth.Alive);
            for (int i = 0; i < bosses.Count; i++) bosses[i]?.TakeDamage(Damage);
        }

        void TickInvincibility(float dt)
        {
            if (_invincibilityRemaining <= 0f) return;
            _invincibilityRemaining = Mathf.Max(0f, _invincibilityRemaining - dt);
            if (_invincibilityRemaining <= 0f) Player.Instance?.Health?.RemoveInvincibility(InvincibilityKey);
        }

        void EnterState(BombState next, float duration)
        {
            State = next;
            _stateRemaining = Mathf.Max(0f, duration);
            OnStateChanged?.Invoke(next);
        }

        void ResetBomb()
        {
            if (_invincibilityRemaining > 0f) Player.Instance?.Health?.RemoveInvincibility(InvincibilityKey);
            if (_definition?.EndActions?.Actions != null && _actionRunner != null)
                _actionRunner.Play(_definition.EndActions, new GameActionContext(Player.Instance != null ? Player.Instance.transform : null));
            _actionRunner?.Tick(0f);
            _actionRunner?.Dispose();
            _actionRunner = null;
            _activeHandle = null;
            if (ReturnSpawnedBullets && BulletPool.Instance != null)
            {
                foreach (var bullet in new List<Bullet>(_spawnedBullets)) BulletPool.Instance.Return(bullet);
            }
            _spawnedBullets.Clear();
            _beforeFireBullets.Clear();
            _invincibilityRemaining = 0f;
            _damageTimer = 0f;
            EnterState(BombState.Ready, 0f);
        }

        /// <summary>玩家死亡时清理当前 Bomb，不执行正常结束动作。</summary>
        internal void ResetForDeath()
        {
            if (_invincibilityRemaining > 0f)
                Player.Instance?.Health?.RemoveInvincibility(InvincibilityKey);
            _actionRunner?.Dispose();
            _actionRunner = null;
            _activeHandle = null;
            _spawnedBullets.Clear();
            _beforeFireBullets.Clear();
            _invincibilityRemaining = 0f;
            _stateRemaining = 0f;
            _damageTimer = 0f;
            EnterState(BombState.Ready, 0f);
        }

        void OnDisable()
        {
            if (_invincibilityRemaining > 0f) Player.Instance?.Health?.RemoveInvincibility(InvincibilityKey);
            _actionRunner?.Dispose();
            _actionRunner = null;
            _spawnedBullets.Clear();
            _beforeFireBullets.Clear();
            _invincibilityRemaining = _stateRemaining = _damageTimer = 0f;
            State = BombState.Ready;
        }
    }
}
