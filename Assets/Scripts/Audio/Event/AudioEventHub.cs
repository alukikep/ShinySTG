using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 音频事件 Hub —— 提供「外部系统触发 → 音频系统响应」的桥接接口。
    ///
    /// ★ 当前状态:框架预留,默认不订阅任何事件 ★
    ///   用户决策:暂时不需要自动切歌(后续在 LevelEditor 加切换 BGM 的方法)。
    ///   本类保留 enable / disable 开关 + 显式订阅 API,让后续 LevelEditor 扩展时
    ///   可以直接调用 EnableAutoSwitch() 让 BGM 自动响应关卡事件,无需再写新代码。
    ///
    /// 公开 API:
    ///   - EnableAutoSwitch() / DisableAutoSwitch()
    ///   - SwitchToLevel(playlist) / SwitchToBoss(track) / SwitchToDefeat(track) / SwitchToSilence()
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

        public AudioEventHub(AudioSystem owner) { _owner = owner; }

        // ─── 公开 API ─────────────────────────────────────────────────────────

        /// <summary>启用「按 LevelController 事件自动切 BGM」。默认关。</summary>
        public void EnableAutoSwitch()
        {
            if (_autoSwitchEnabled) return;
            _autoSwitchEnabled = true;

            // 当前已有 LevelController → 立即订阅
            TrySubscribeCurrentLevel();
        }

        /// <summary>禁用自动切歌(已订阅的事件会自动解订)。</summary>
        public void DisableAutoSwitch()
        {
            if (!_autoSwitchEnabled) return;
            _autoSwitchEnabled = false;
            UnsubscribeCurrentLevel();
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
            if (lc != null) SubscribeTo(lc);
            // 注意:LevelController 可能晚于 AudioSystem 创建 → 调用方应自己处理时序
            // 或者在 AudioEventHub 里挂一个 Coroutine 轮询直到拿到实例
            // 这里为简化,要求调用方在 LevelController 创建后调 EnableAutoSwitch
        }

        void SubscribeTo(ShinySTG.Level.LevelController lc)
        {
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
            _activeBinding = null;
        }

        void HandleLevelStart(ShinySTG.Level.LevelDefinition def)
        {
            if (!_autoSwitchEnabled || def == null) return;
            _activeBinding = FindBinding(def);
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
            DisableAutoSwitch();
        }
    }
}