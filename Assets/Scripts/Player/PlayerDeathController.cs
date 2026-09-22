using System;
using System.Collections;
using SerializeReferenceEditor;
using ShinySTG.Effects;
using ShinySTG.GameplayCommands;
using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>死亡表现与重生编排；根对象保持启用，生命结算由 Health 负责。</summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerDeathController : MonoBehaviour
    {
        [SerializeReference, SR, Tooltip("每次失去一命执行一次，包含最后一命。")]
        GlobalCommand[] _deathCommands;
        [SerializeField, Tooltip("在死亡位置独立播放的一次性特效；留空跳过。")]
        GameObject _deathEffectPrefab;
        [SerializeField, Min(0f), Tooltip("特效结束后的额外等待秒数，使用游戏时间。")]
        float _respawnDelay = 0.3f;
        [SerializeField, Tooltip("重生位置；留空使用初始出生位置。")]
        Transform _respawnPoint;
        [SerializeField, Min(0), Tooltip("玩家复活时重置为该数量的 Bomb。")]
        int _respawnBombs;
        [SerializeField, Min(0.01f), Tooltip("重生无敌期间本体闪烁的间隔秒数。")]
        float _blinkInterval = 0.08f;

        public event Action OnDeathPresentationComplete;
        public bool IsDeathPresentationComplete { get; private set; }
        PlayerHealth _health;
        PlayerBomb _bomb;
        PlayerResources _resources;
        PlayerOptions _options;
        Renderer[] _renderers;
        bool[] _originalHidden;
        Vector3 _spawnPosition;
        Coroutine _routine;
        PooledEffect _effect;
        uint _effectVersion;
        bool _commandsExecuted;
        bool _blink;
        float _blinkElapsed;

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _bomb = GetComponent<PlayerBomb>();
            _resources = GetComponent<PlayerResources>();
            _options = GetComponent<PlayerOptions>();
            _spawnPosition = transform.position;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalHidden = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalHidden[i] = _renderers[i].forceRenderingOff;
        }

        void OnEnable()
        {
            _health.OnLifeLost += HandleDeath;
            if (_health.IsDead)
            {
                SetHidden(true);
                _options?.SetVisible(false);
            }
            if (_health.IsDying) StartDeath();
        }

        void Start()
        {
            // 各组件 Awake 顺序不保证 Health 已初始化，Start 再同步初始显示。
            if (!_health.IsDying)
            {
                SetHidden(_health.IsDead);
                _options?.SetVisible(!_health.IsDead);
            }
        }

        void HandleDeath()
        {
            _commandsExecuted = false;
            StartDeath();
        }

        void StartDeath()
        {
            if (_routine != null) return;
            IsDeathPresentationComplete = false;
            _blink = false;
            _bomb?.ResetForDeath();
            SetHidden(true);
            _options?.SetVisible(false);
            _routine = StartCoroutine(DeathSequence());
        }

        IEnumerator DeathSequence()
        {
            // TakeHit 的扣命通知尚未结束；下一帧也避开当前碰撞池的遍历。
            yield return null;
            while (Time.timeScale <= 0f) yield return null;
            if (!_commandsExecuted)
            {
                _commandsExecuted = true;
                try
                {
                    GlobalCommandExecutor.Execute(_deathCommands,
                        new GlobalCommandContext(transform, invocation: CommandInvocation.PlayerDeath));
                }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            if (!isActiveAndEnabled) yield break;
            _effect = EffectPool.Play(_deathEffectPrefab, transform.position, gameObject.scene);
            if (_effect != null)
            {
                _effectVersion = _effect.PlaybackVersion;
                while (_effect != null && _effect.IsPlaybackActive(_effectVersion)) yield return null;
            }
            _effect = null;
            float remaining = Mathf.Max(0f, _respawnDelay);
            while (remaining > 0f)
            {
                yield return null;
                remaining -= Time.deltaTime;
            }
            _routine = null;
            IsDeathPresentationComplete = true;
            OnDeathPresentationComplete?.Invoke();
            if (isActiveAndEnabled && _health.Lives > 0) Respawn();
        }

        /// <summary>终局续关时先加命，再调用本入口；演出中禁止提前重生。</summary>
        public bool Respawn()
        {
            if (!isActiveAndEnabled || _routine != null || !_health.isActiveAndEnabled || !_health.IsDying || _health.Lives <= 0) return false;
            transform.position = _respawnPoint != null ? _respawnPoint.position : _spawnPosition;
            SetHidden(false);
            _options?.SetVisible(true, true);
            if (!_health.CompleteRevive()) return false;
            _resources ??= GetComponent<PlayerResources>();
            _resources?.SetBombs(_respawnBombs);
            _blink = true;
            _blinkElapsed = 0f;
            return true;
        }

        void Update()
        {
            if (!_blink) return;
            if (_health.InvincibleRemaining <= 0f)
            {
                _blink = false;
                SetHidden(false);
                return;
            }
            _blinkElapsed += Time.deltaTime;
            SetHidden(Mathf.FloorToInt(_blinkElapsed / Mathf.Max(0.01f, _blinkInterval)) % 2 != 0);
        }

        void SetHidden(bool hidden)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;
                var indicator = renderer.GetComponentInParent<PlayerHitboxIndicator>();
                if (indicator != null && indicator.OwnsRenderer(renderer)) continue;
                renderer.forceRenderingOff = hidden || _originalHidden[i];
            }
        }

        void OnDisable()
        {
            _health.OnLifeLost -= HandleDeath;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_effect != null && _effect.IsPlaybackActive(_effectVersion)) _effect.Release();
            _effect = null;
            _blink = false;
            SetHidden(false);
            _options?.SetVisible(!_health.IsDying && !_health.IsDead, true);
        }
    }
}
