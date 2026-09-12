using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 音频系统总控 —— PersistentSingleton,场景里只挂一份,DontDestroyOnLoad 跨场景保留。
    ///
    /// 职责:
    ///   - 持有 SfxRouter / MusicPlayer / BusMixer 子模块
    ///   - 注册默认 Bus(Master/Sfx/Bgm/UI),用户可在 Inspector 拖 AudioBus 资产覆盖
    ///   - 暴露 RunInBackground / MuteOnApplicationPause 行为开关
    ///   - 处理 timeScale=0 时的暂停恢复(配合 MusicPlayer.OnApplicationPause)
    ///
    /// 与项目惯例对齐:
    ///   - 场景里只挂一份(PersistentSingleton),与 BulletPool / CollisionService / LevelController 一致。
    ///   - AudioSystem.cs 自带默认 fallback Bus(Master/Sfx/Bgm/UI),即使一个 AudioBus 资产都没建也能跑。
    ///   - 不修改任何现有脚本;调用方通过 AudioMix.PlaySfx(...) 静态门面访问,0 反向依赖。
    /// </summary>
    public class AudioSystem : PersistentSingleton<AudioSystem>
    {
        [Header("Default Buses (可选 — 没建 AudioBus 资产也能用)")]
        [Tooltip("Master 总线。空时用内置 fallback(Master,1.0)。")]
        public AudioBus MasterBus;
        [Tooltip("Bgm 总线。空时用内置 fallback(Bgm,1.0)。")]
        public AudioBus BgmBus;
        [Tooltip("Sfx 总线。空时用内置 fallback(Sfx,1.0)。")]
        public AudioBus SfxBus;
        [Tooltip("UI 总线。空时用内置 fallback(UI,1.0)。")]
        public AudioBus UIBus;

        [Header("Behavior")]
        [Tooltip("timeScale=0 时是否暂停音乐 + SFX。默认 true,STG 暂停菜单需要。")]
        public bool PauseOnTimeScaleZero = true;

        [Tooltip("Mute 当 Application.isFocused=false(失去焦点时静音)。默认 false。")]
        public bool MuteOnLoseFocus = false;

        [Header("Level Audio Binding (向后兼容 — 优先用 LevelDefinition.AudioBinding)")]
        [Tooltip("关卡 → BGM 绑定资产(全局查表,向后兼容)。AudioEventHub 在 LevelDefinition.AudioBinding 为空时,按 Level 字段匹配查表切 BGM。\n" +
                 "★ 推荐做法:在每个 LevelDefinition 上挂自己的 AudioBinding(关卡资产一站式管理),这里留空。\n" +
                 "若多个关卡共用同一套 BGM 模板(比如所有普通关卡共用 StageTheme),可以在这里集中配,关卡资产里 AudioBinding 留空。\n" +
                 "留空 + 关卡 AudioBinding 也空 = 不切 BGM。")]
        public LevelAudioBinding[] LevelBindings;

        // 子模块
        public SfxRouter Sfx { get; private set; }
        public MusicPlayer Music { get; private set; }
        public BusMixer BusMixer { get; private set; }
        public AudioEventHub EventHub { get; private set; }

        // 暂停状态
        bool _pausedByTimeScale;
        bool _mutedByFocus;
        bool _appPaused;

        protected override void Awake()
        {
            base.Awake();
            InitBuses();
            Sfx = new SfxRouter(this);
            Music = new MusicPlayer(this);
            EventHub = new AudioEventHub(this);
        }

        void InitBuses()
        {
            BusMixer = new BusMixer();
            // 注册用户配的;没配则建临时 fallback(不创建资产,纯内存)
            BusMixer.Register(MasterBus != null ? MasterBus : CreateTempBus("Master", "audio.master", 1f));
            BusMixer.Register(BgmBus    != null ? BgmBus    : CreateTempBus("Bgm",    "audio.bgm",    1f));
            BusMixer.Register(SfxBus    != null ? SfxBus    : CreateTempBus("Sfx",    "audio.sfx",    1f));
            BusMixer.Register(UIBus     != null ? UIBus     : CreateTempBus("UI",     "audio.ui",     1f));
            BusMixer.ApplyAllToMixer();
        }

        AudioBus CreateTempBus(string name, string prefsKey, float defaultVol)
        {
            // ScriptableObject.CreateInstance —— 运行时建临时实例,不写盘,跨场景存活
            var bus = ScriptableObject.CreateInstance<AudioBus>();
            bus.name = name;
            bus.BusName = name;
            bus.PlayerPrefsKey = prefsKey;
            bus.DefaultVolume = defaultVol;
            return bus;
        }

        void Update()
        {
            // 跟随 timeScale=0 暂停(对齐用户决策:暂停菜单静音)
            if (PauseOnTimeScaleZero)
            {
                bool shouldPause = Time.timeScale <= 0.0001f;
                if (shouldPause != _pausedByTimeScale)
                {
                    _pausedByTimeScale = shouldPause;
                    if (shouldPause) { Sfx.StopAll(); Music.Pause(); }
                    else Music.Resume();
                }
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!MuteOnLoseFocus) return;
            if (!hasFocus && !_mutedByFocus)
            {
                _mutedByFocus = true;
                AudioHelper.SetMute(true);
            }
            else if (hasFocus && _mutedByFocus)
            {
                _mutedByFocus = false;
                AudioHelper.SetMute(false);
            }
        }

        void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            Music.OnApplicationPause(paused);
        }
    }

    /// <summary>
    /// 静态 helper —— UnityEngine.AudioSettings 控制全局 mute(失去焦点时用)。
    /// 命名用 AudioHelper(而非 Audio)以避开 CONTRIBUTING §4.7 警告的「namespace 与类同名」陷阱:
    /// namespace ShinySTG.Audio 已占用 Audio 字段,类再叫 Audio 会让调用方写 ShinySTG.Audio.Audio 时编译器犹豫。
    /// </summary>
    public static class AudioHelper
    {
        public static void SetMute(bool muted) => AudioListener.pause = muted;
        public static bool IsMuted => AudioListener.pause;
    }
}
