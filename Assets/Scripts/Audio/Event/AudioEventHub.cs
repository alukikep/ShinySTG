using System.Collections;
using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 音频事件 Hub —— 提供「外部系统触发 → 音频系统响应」的桥接接口。
    ///
    /// ★ 当前状态:按 LevelDefinition 启用 ★
    ///   启用时机:LevelController.BeginLevel() 内,若 Definition.AutoSwitchBgm == true 且场景有 AudioSystem,
    ///   会调 TryBind(definition):启用 EventHub(若尚未启用) + 订阅当前 LevelController 事件 + 按 definition.AudioBinding 切歌。
    ///   每个关卡自带决定「要不要自动切歌」「用什么 AudioBinding」,关卡资产一站式管理。
    ///
    /// 公开 API:
    ///   - TryBind(definition)            关卡启用入口(LevelController 调)。幂等 + 自动延迟订阅(若 LevelController 尚未实例化)
    ///   - EnableAutoSwitch() / DisableAutoSwitch()  全局开关(高级用法,大多数关卡不需要直接调)
    ///   - SwitchToLevel(playlist) / SwitchToBoss(track) / SwitchToDefeat(track) / SwitchToSilence()
    ///                                    手动触发(永远可用,与 enable 无关)
    ///
    /// 上述「Switch*」方法任何时候都可以手动调用,与 enable 状态无关。
    /// </summary>
    public class AudioEventHub
    {
        readonly AudioSystem _owner;

        // 当前已订阅的状态
        ShinySTG.Level.LevelController _subscribedLevel;
        LevelAudioBinding _activeBinding;
        bool _autoSwitchEnabled;

        // LevelController 比 AudioSystem 后创建的兜底:启动协程轮询直到拿到
        Coroutine _waitForLevelCo;

        public AudioEventHub(AudioSystem owner) { _owner = owner; }

        // ─── 公开 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 关卡启用入口(LevelController.BeginLevel 调)。
        ///   - 若 Definition.AutoSwitchBgm == false:不启用,直接 return(可多次调,幂等)
        ///   - 若 Definition.AudioBinding == null:不启用,直接 return(配「允许自动切但本关不切」)
        ///   - 否则:启用 EventHub(若尚未启用) + 订阅当前 LevelController + 缓存 activeBinding
        ///
        /// 幂等:同一关卡多次调 TryBind 不会重复订阅(内部判 Instance 是否一致)。
        /// 关卡切换时:下一关调 TryBind 会自动解订旧订阅 + 订阅新 LevelController(同实例)。
        /// </summary>
        public void TryBind(ShinySTG.Level.LevelDefinition def)
        {
            if (def == null) return;
            if (!def.AutoSwitchBgm) return;             // 关卡明确不参与自动切歌
            if (def.AudioBinding == null) return;       // 允许切但本关没配 binding

            // 1. 缓存 activeBinding(用于后续 OnBossSpawned / OnBossDefeated 切歌)
            _activeBinding = def.AudioBinding;

            // 2. 启用(若已启用则内部守卫直接 return)
            EnableAutoSwitch();
        }

        /// <summary>启用「按 LevelController 事件自动切 BGM」。默认关。多次调幂等。</summary>
        public void EnableAutoSwitch()
        {
            if (_autoSwitchEnabled) return;
            _autoSwitchEnabled = true;

            // 当前已有 LevelController → 立即订阅;否则启动协程轮询兜底
            TrySubscribeCurrentLevel();
        }

        /// <summary>禁用自动切歌(已订阅的事件会自动解订)。</summary>
        public void DisableAutoSwitch()
        {
            if (!_autoSwitchEnabled) return;
            _autoSwitchEnabled = false;
            UnsubscribeCurrentLevel();
            StopWaitForLevelCoroutine();
        }

        // ─── 手动触发(永远可用,与 enable 无关)─────────────────────────────

        public void SwitchToLevel(LevelAudioBinding binding)
        {
            if (binding == null || binding.Playlist == null) { SwitchToSilence(1.5f); return; }
            _owner.Music.PlayPlaylist(binding.Playlist);
        }

        public void SwitchToBoss(LevelAudioBinding binding)
        {
            if (binding == null || binding.BossMusic == null) return;
            _owner.Music.PlayTrack(binding.BossMusic, binding.ToBossCrossfade);
        }

        public void SwitchToDefeat(LevelAudioBinding binding)
        {
            if (binding == null || binding.DefeatMusic == null) { SwitchToSilence(binding != null ? binding.ToDefeatCrossfade : 1.5f); return; }
            _owner.Music.PlayTrack(binding.DefeatMusic, binding.ToDefeatCrossfade);
        }

        public void SwitchToSilence(float fadeOut = 1.5f)
        {
            _owner.Music.Stop(fadeOut);
        }

        // ─── 自动订阅实现(仅当 EnableAutoSwitch() 后)────────────────────────

        void TrySubscribeCurrentLevel()
        {
            if (!_autoSwitchEnabled) return;
            var lc = ShinySTG.Level.LevelController.Instance;
            if (lc != null)
            {
                SubscribeTo(lc);
                return;
            }
            // LevelController 可能晚于 AudioSystem 创建 → 启动协程轮询兜底
            // (调方在 LevelController 创建后调 EnableAutoSwitch;若忘了,这里兜底)
            StartWaitForLevelCoroutine();
        }

        void StartWaitForLevelCoroutine()
        {
            if (_waitForLevelCo != null) return;
            _waitForLevelCo = _owner.StartCoroutine(WaitForLevelRoutine());
        }

        void StopWaitForLevelCoroutine()
        {
            if (_waitForLevelCo != null)
            {
                _owner.StopCoroutine(_waitForLevelCo);
                _waitForLevelCo = null;
            }
        }

        IEnumerator WaitForLevelRoutine()
        {
            // 最多等 5 秒;之后放弃(场景可能根本没有 LevelController)
            const float timeout = 5f;
            float t = 0f;
            while (t < timeout && _autoSwitchEnabled)
            {
                var lc = ShinySTG.Level.LevelController.Instance;
                if (lc != null)
                {
                    SubscribeTo(lc);
                    _waitForLevelCo = null;
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            _waitForLevelCo = null;
        }

        void SubscribeTo(ShinySTG.Level.LevelController lc)
        {
            if (lc == null) return;

            // 若已订阅同一个实例,跳过(关卡切换时调 TryBind 是幂等的)
            if (_subscribedLevel == lc) return;

            UnsubscribeCurrentLevel();
            _subscribedLevel = lc;
            lc.OnLevelStart    += HandleLevelStart;
            lc.OnBossSpawned   += HandleBossSpawned;
            lc.OnBossDefeated  += HandleBossDefeated;
        }

        void UnsubscribeCurrentLevel()
        {
            if (_subscribedLevel == null) return;
            _subscribedLevel.OnLevelStart    -= HandleLevelStart;
            _subscribedLevel.OnBossSpawned   -= HandleBossSpawned;
            _subscribedLevel.OnBossDefeated  -= HandleBossDefeated;
            _subscribedLevel = null;
            // 注意:不解 _activeBinding —— 关卡切换 TryBind 会覆盖;
            // DisableAutoSwitch 才彻底清场。
        }

        void HandleLevelStart(ShinySTG.Level.LevelDefinition def)
        {
            if (!_autoSwitchEnabled || def == null) return;
            // 优先按 def.AudioBinding(关卡内嵌引用);fallback 到 _owner.LevelBindings 全局查表(向后兼容老用法)
            var binding = def.AudioBinding != null ? def.AudioBinding : FindBinding(def);
            _activeBinding = binding;
            if (_activeBinding != null) SwitchToLevel(_activeBinding);
        }

        void HandleBossSpawned(GameObject go)
        {
            if (!_autoSwitchEnabled || _activeBinding == null) return;
            SwitchToBoss(_activeBinding);
        }

        void HandleBossDefeated(GameObject go)
        {
            if (!_autoSwitchEnabled || _activeBinding == null) return;
            SwitchToDefeat(_activeBinding);
        }

        LevelAudioBinding FindBinding(ShinySTG.Level.LevelDefinition def)
        {
            if (_owner.LevelBindings == null) return null;
            for (int i = 0; i < _owner.LevelBindings.Length; i++)
            {
                var b = _owner.LevelBindings[i];
                if (b != null && b.Level == def) return b;
            }
            return null;
        }

        // ─── 场景切换时清理订阅 ─────────────────────────────────────────────

        /// <summary>AudioSystem.OnDestroy 调用 —— 清理订阅,避免泄漏。</summary>
        public void OnDestroy()
        {
            UnsubscribeCurrentLevel();
            StopWaitForLevelCoroutine();
            DisableAutoSwitch();
        }
    }
}