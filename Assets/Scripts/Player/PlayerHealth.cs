using System;
using System.Collections.Generic;
using UnityEngine;
using ShinySTG.Hitbox;  // CollisionService / Bullet (擦弹事件订阅)

namespace ShinySTG.Player
{
    /// <summary>
    /// 玩家残机 + 复活无敌 + 火力级 + 擦弹计数。
    ///
    /// 火力级 (PowerLevel 0..MaxPower):
    ///   - 影响 PlayerShooting 喷射形态
    ///   - 影响 PlayerOptions 解锁多少子机(每 +1 火力解锁下一档)
    /// 残机 (Lives):
    ///   - 0 = 死透了,触发 OnAllLivesLost,禁止移动、射击和拾取
    ///   - >0 = 死亡时扣 1,触发 OnLifeLost;由死亡演出完成后触发 OnRevive 和复活无敌
    ///
    /// 无敌阶段 (Invincibility):
    ///   - 默认从出生 / 复活开始给一段无敌,撞弹不扣命
    ///   - 期间玩家闪白(可视化留给外部 Renderer,本类只暴露事件)
    ///
    /// 擦弹 (GrazeCount):
    ///   - 普通子弹擦弹事件与激光服务共用 RecordGraze 入口累加
    ///   - 不影响残机 / 火力 / 无敌,仅作为 STG 经典的高分元素与成就统计
    ///   - 提供 DebugTriggerGraze() 调试入口(跳过真实碰撞直接累加,供 UI 测试用)
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Audio (optional — 留空则不播放)")]
        [Tooltip("玩家受伤时播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _hitSfx;
        [Tooltip("玩家死亡时(残机归零)播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _deathSfx;
        [Tooltip("擦弹时播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _grazeSfx;
        [Tooltip("火力提升时播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _powerUpSfx;

        [Header("Lives")]
        [Tooltip("初始残机数(含本体,例如 3 = 玩家 + 2 续命)。")]
        public int InitialLives = 3;

        [Tooltip("当前生命总数，含本体；1 = 无备用残机，0 = 已死亡。")]
        public int Lives { get; private set; }
        /// <summary>生命总数改变后触发，参数含本体；HUD 备用残机应减去本体。</summary>
        public event Action<int> OnLivesChanged;

        [Header("Power")]
        [Range(0, 4)]
        [Tooltip("初始火力级(0..4)。")]
        public int InitialPower = 1;

        [Range(1, 4)]
        [Tooltip("最大火力级。")]
        public int MaxPower = 4;

        [Tooltip("运行时当前火力级(0..MaxPower)。由外部 PowerUp() 提升。")]
        public int PowerLevel { get; private set; }
        public int PowerUnits { get; private set; }
        public float Power => PowerUnits / 100f;
        public event Action<int> OnPowerChanged;

        [Header("Invincibility")]
        [Tooltip("出生时的无敌时长(秒)。")]
        public float SpawnInvincibleDuration = 3f;

        [Tooltip("复活后的无敌时长(秒)。")]
        public float ReviveInvincibleDuration = 3f;

        [Tooltip("当前无敌剩余时间(秒)。<=0 = 可受伤。")]
        public float InvincibleRemaining { get; private set; }

        [field: Header("Graze")]
        [field: Tooltip("累计擦弹数，包含敌方子弹与激光。用于计分、成就与 HUD 显示。")]
        [field: SerializeField] public int GrazeCount { get; private set; }

        readonly HashSet<string> _invincibilityLocks = new();
        bool _settlingHit;
        float _deathbombRemaining;
        float _deathbombDamage;
        public bool IsDeathbombWindowOpen => _deathbombRemaining > 0f;

        public bool IsInvincible => InvincibleRemaining > 0f || _invincibilityLocks.Count > 0;
        public bool IsDead       => Lives <= 0;
        public bool IsDying { get; private set; }
        public bool CanInteract => isActiveAndEnabled && !IsDead && !IsDying;

        // ---- 事件(供 UI / 动画 / 子机响应)----
        public event Action OnLifeLost;       // 死亡瞬间(扣命前)
        public event Action OnRevive;         // 复活瞬间
        public event Action OnAllLivesLost;   // 全部耗尽(没复活)
        public event Action<int> OnPowerUp;    // 火力提升(int = 新等级)
        public event Action OnInvincibleStart;
        public event Action OnInvincibleEnd;
        /// <summary>擦弹 +1 触发(int = 累加后的新值)。供 UI / 计分 / 音效订阅。</summary>
        public event Action<int> OnGraze;

        void Awake()
        {
            Lives = Mathf.Max(0, InitialLives);
            PowerLevel = Mathf.Clamp(InitialPower, 0, MaxPower);
            PowerUnits = PowerLevel * 100;
            InvincibleRemaining = SpawnInvincibleDuration;
            if (InvincibleRemaining > 0f) OnInvincibleStart?.Invoke();
        }

        void Start()
        {
            // 订阅全局碰撞服务的擦弹事件。CollisionService 在场景里手动挂;
            // 还没初始化时给出警告(LateUpdate 不会跑,Lives 也不会被命中)。
            // 与 CollisionService.Instance 共生命周期:场景切换时 OnDestroy 会自动置 Instance = null,
            // 本组件 OnDisable 会跟着解订,无需手动 null 守卫。
            if (CollisionService.Instance != null)
            {
                CollisionService.Instance.OnPlayerGrazeByEnemyBullet += HandleGraze;
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] CollisionService.Instance 为 null,擦弹事件无法订阅。请确认场景里挂了 CollisionService 组件。");
            }
        }

        void OnDisable()
        {
            // 与 Start 对称解订,避免组件被禁用 / 销毁时残留回调。
            if (CollisionService.Instance != null)
            {
                CollisionService.Instance.OnPlayerGrazeByEnemyBullet -= HandleGraze;
            }
            _deathbombRemaining = 0f;
            _deathbombDamage = 0f;
        }

        /// <summary>CollisionService 触发时回调:累加 GrazeCount + 广播事件。</summary>
        void HandleGraze(Bullet bullet, PlayerHealth player)
        {
            if (player != this) return;
            RecordGraze(bullet != null ? (Vector2)bullet.transform.position : (Vector2)transform.position);
        }

        /// <summary>子弹与激光共用的擦弹计数、HUD 通知及音效入口；调用方负责判定与冷却。</summary>
        public void RecordGraze(Vector2 position)
        {
            if (!CanInteract || ShinySTG.GameFlow.GameplayPause.IsPaused) return;
            if (ShinySTG.Level.BattleRestriction.IsActive) return;
            GrazeCount++;
            OnGraze?.Invoke(GrazeCount);
            if (_grazeSfx != null)
            {
                ShinySTG.Audio.AudioMix.PlaySfx(_grazeSfx, position: position);
            }
        }

        /// <summary>
        /// 调试入口:跳过真实碰撞,直接累加一次擦弹。供 UI 测试 / Play Mode 调试用,
        /// 生产代码不应调本方法(正常的擦弹走 CollisionService 事件)。
        /// </summary>
        public void DebugTriggerGraze()
        {
            HandleGraze(null, this);
        }

        void Update()
        {
            // 无敌倒计时
            if (InvincibleRemaining > 0f)
            {
                InvincibleRemaining -= Time.deltaTime;
                if (InvincibleRemaining <= 0f)
                {
                    InvincibleRemaining = 0f;
                    if (_invincibilityLocks.Count == 0) OnInvincibleEnd?.Invoke();
                }
            }
            if (_deathbombRemaining > 0f && !ShinySTG.GameFlow.GameplayPause.IsPaused)
            {
                _deathbombRemaining -= Time.deltaTime;
                if (_deathbombRemaining <= 0f)
                {
                    _deathbombRemaining = 0f;
                    CommitHit(_deathbombDamage);
                    _deathbombDamage = 0f;
                }
            }
        }

        public void AddInvincibility(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey)) return;
            bool wasInvincible = IsInvincible;
            _invincibilityLocks.Add(sourceKey);
            if (!wasInvincible && IsInvincible) OnInvincibleStart?.Invoke();
        }

        public void RemoveInvincibility(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey)) return;
            bool wasInvincible = IsInvincible;
            _invincibilityLocks.Remove(sourceKey);
            if (wasInvincible && !IsInvincible) OnInvincibleEnd?.Invoke();
        }

        /// <summary>被敌弹 / 敌人命中时调用。无敌时直接吞掉。</summary>
        public void TakeHit(float damage = 1f)
        {
            if (!CanInteract || IsInvincible || ShinySTG.Level.BattleRestriction.IsActive) return;
            if (damage <= 0f) return;
            if (Lives <= 0) return;
            if (IsDeathbombWindowOpen) return;

            var bomb = ShinySTG.Player.Player.Instance?.Bomb;
            float window = bomb != null ? bomb.DeathbombWindow : 0f;
            if (window > 0f && !IsDeathbombWindowOpen)
            {
                _deathbombDamage = damage;
                _deathbombRemaining = window;
                return;
            }
            CommitHit(damage);
        }

        internal bool TryResolveDeathbomb()
        {
            if (!IsDeathbombWindowOpen) return false;
            _deathbombRemaining = 0f;
            _deathbombDamage = 0f;
            return true;
        }

        void CommitHit(float damage)
        {
            if (!CanInteract || IsInvincible || ShinySTG.Level.BattleRestriction.IsActive) return;
            if (damage <= 0f || Lives <= 0) return;

            _settlingHit = true;
            IsDying = true; // 在任何外部回调前防止重入。
            try
            {
                OnLifeLost?.Invoke();
                if (_hitSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_hitSfx, position: (Vector2)transform.position);

                Lives -= 1;
                OnLivesChanged?.Invoke(Lives);
                if (Lives <= 0)
                {
                    Lives = 0;
                    if (_deathSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_deathSfx, position: (Vector2)transform.position);
                    OnAllLivesLost?.Invoke();
                    return;
                }
            }
            finally { _settlingHit = false; }
        }

        /// <summary>由死亡演出结束或续关流程调用；加命本身不恢复控制。</summary>
        public bool CompleteRevive()
        {
            if (_settlingHit || !IsDying || Lives <= 0 || !isActiveAndEnabled) return false;
            IsDying = false;
            // 玩家实际出现时才开始复活无敌。
            bool wasInvincible = IsInvincible;
            InvincibleRemaining = ReviveInvincibleDuration;
            if (!wasInvincible && IsInvincible) OnInvincibleStart?.Invoke();
            OnRevive?.Invoke();
            return true;
        }

        /// <summary>吃火力道具时调用。clamp 到 [0, MaxPower]。</summary>
        public void PowerUp(int delta = 1)
        {
            SetPowerUnits((long)PowerUnits + (long)delta * 100);
        }

        /// <summary>百分之一 Power 为一个单位；小 P +1，大 P +100。</summary>
        public void AddPowerUnits(int delta) => SetPowerUnits((long)PowerUnits + delta);

        void SetPowerUnits(long value)
        {
            int next = (int)Math.Max(0L, Math.Min(value, (long)Mathf.Max(0, MaxPower) * 100));
            if (next == PowerUnits) return;
            int oldLevel = PowerLevel;
            PowerUnits = next;
            PowerLevel = PowerUnits / 100;
            OnPowerChanged?.Invoke(PowerUnits);
            if (PowerLevel == oldLevel) return;
            OnPowerUp?.Invoke(PowerLevel);
            if (_powerUpSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_powerUpSfx, position: (Vector2)transform.position);
        }

        /// <summary>修改生命总数；终局续关还需调用 PlayerDeathController.Respawn。</summary>
        public void AddLife(int delta = 1)
        {
            int next = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)Lives + delta));
            if (next == Lives) return;
            Lives = next;
            OnLivesChanged?.Invoke(Lives);
        }
    }
}
