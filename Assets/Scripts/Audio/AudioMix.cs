using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 音频系统静态门面 —— 整个项目所有代码播放音效/音乐的**唯一入口**。
    ///
    /// ★ 设计原则 ★
    ///   - 调用方(PlayerHealth / BossHealth / CollisionService / UI Button / LevelController ...)
    ///     不直接拿 AudioSystem.Instance,只通过本类的静态方法。
    ///   - 本类做 null-safe 处理:AudioSystem 还没初始化 / 被销毁时,调用不报错,直接跳过。
    ///   - 这样调用方写起来极简:AudioMix.PlaySfx(playerHitSfx); 一行搞定。
    ///
    /// 与项目惯例对齐:
    ///   - 与 BulletPool.Instance? 风格一致 —— 调用方不强制 null check。
    ///   - 不修改任何宿主脚本,只让宿主在 TakeHit / TakeDamage 处加一行 AudioMix.PlaySfx(...)。
    ///
    /// 典型用法:
    ///   // 在 PlayerHealth.TakeHit 里:
    ///   AudioMix.PlaySfx(_hitSfx);                                          // 默认规则播放
    ///   AudioMix.PlaySfx(_hitSfx, position: transform.position);           // 子弹命中的世界点
    ///   AudioMix.PlaySfx(_hitSfx, parent: enemyTransform);                  // 跟随敌人移动
    ///   AudioMix.PlaySfx(_explodeSfx, volumeMul: 0.5f);                     // 临时压低音量
    ///
    ///   // 在 Boss 总控 / 关卡事件订阅里:
    ///   AudioMix.PlayTrack(bossTheme, crossfade: 2f);
    ///   AudioMix.PlayPlaylist(stagePlaylist);
    ///   AudioMix.PauseMusic();
    ///
    ///   // 在 UI Button.OnClick:
    ///   AudioMix.PlaySfx(UIBank.Click);
    /// </summary>
    public static class AudioMix
    {
        // ─── SFX ───────────────────────────────────────────────────────────

        /// <summary>播放一个 SFX cue。无 position = 跟随 Listener(2D 监听);有 position = 在世界坐标发声。</summary>
        public static void PlaySfx(SfxCue cue,
                                   Vector2? position = null,
                                   Transform parent = null,
                                   float volumeMul = 1f,
                                   float pitch = 1f,
                                   MonoBehaviour caller = null)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Sfx == null) return;
            sys.Sfx.Play(cue, position, parent, volumeMul, pitch, caller);
        }

        /// <summary>停止某 cue 全部正在播放的 voice。</summary>
        public static void StopSfx(SfxCue cue)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Sfx == null) return;
            sys.Sfx.StopAll(cue);
        }

        /// <summary>停止所有 SFX(暂停菜单、关卡结束清场)。</summary>
        public static void StopAllSfx()
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Sfx == null) return;
            sys.Sfx.StopAll();
        }

        // ─── BGM ───────────────────────────────────────────────────────────

        /// <summary>播放单首 BGM,带交叉淡化。</summary>
        public static void PlayTrack(BgmTrack track, float crossfade = 1.5f)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Music == null) return;
            sys.Music.PlayTrack(track, crossfade);
        }

        /// <summary>播放整个 BGM 列表(顺序/随机/循环由 playlist 资产决定)。</summary>
        public static void PlayPlaylist(BgmPlaylist playlist)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Music == null) return;
            sys.Music.PlayPlaylist(playlist);
        }

        /// <summary>暂停音乐(timeScale=0 时 AudioSystem 自动调,这里给手动暂停菜单用)。</summary>
        public static void PauseMusic()
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Music == null) return;
            sys.Music.Pause();
        }

        public static void ResumeMusic()
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Music == null) return;
            sys.Music.Resume();
        }

        /// <summary>停止音乐,fadeOut 秒内淡出(默认 1.5s)。0 = 立即停。</summary>
        public static void StopMusic(float fadeOut = 1.5f)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.Music == null) return;
            sys.Music.Stop(fadeOut);
        }

        // ─── 总线音量 ──────────────────────────────────────────────────────

        /// <summary>设置某总线音量(0..1)。Master=0 时全部静音。</summary>
        public static void SetBusVolume(AudioBusKind kind, float volume01)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.BusMixer == null) return;
            sys.BusMixer.SetVolume(kind, Mathf.Clamp01(volume01));
        }

        /// <summary>读某总线当前音量。</summary>
        public static float GetBusVolume(AudioBusKind kind)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.BusMixer == null) return 1f;
            return sys.BusMixer.GetCurrentVolume(kind);
        }

        /// <summary>单独静音某总线(不影响其他)。</summary>
        public static void MuteBus(AudioBusKind kind, bool muted)
        {
            var sys = AudioSystem.Instance;
            if (sys == null || sys.BusMixer == null) return;
            sys.BusMixer.SetMuted(kind, muted);
        }

        /// <summary>全局静音(所有总线)。</summary>
        public static void MuteAll(bool muted)
        {
            AudioHelper.SetMute(muted);
        }
    }
}
