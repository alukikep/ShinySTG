using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 关卡 → BGM 绑定资产 —— 把 LevelDefinition 资产与 BGM 资产配对,供后续扩展用。
    ///
    /// ★ 当前状态:接口预留,不在运行时自动启用 ★
    ///   用户后续打算在 LevelEditor 加「切换 BGM」方法。本类提供数据结构,
    ///   让 LevelEditor 能在自己的 Inspector 里编辑这些绑定,但 AudioEventHub 默认不订阅
    ///   LevelController 事件(用户决策:暂时不需要自动切歌)。
    ///
    /// 使用方式(后续 LevelEditor 扩展时):
    ///   1. 用户在 LevelEditor 选中 LevelDefinition.asset 时,可以在右栏看到 LevelBindings 字段,
    ///      让用户配 "此关卡播放哪首 BGM / Boss 战切到哪首 / 击败 Boss 切到哪首"。
    ///   2. LevelEditor 内部把用户的编辑写入 LevelAudioBinding.Playlist / BossMusic / DefeatMusic。
    ///   3. 运行时关卡开始 → 关卡结束,BGM 切换由调用方通过 AudioMix.PlayTrack / PlayPlaylist 触发
    ///      (LevelEditor 可以用 LevelController.OnLevelStart / OnBossSpawned 事件,但本类本身不订阅)。
    ///
    /// 与既有层关系:
    ///   - 完全正交:不修改 LevelController,不修改 AudioSystem,仅作为「数据容器」存在。
    ///   - AudioSystem.LevelBindings 字段持有这些资产的引用,但 AudioSystem 启动时不会枚举它们
    ///     (除非用户后续在 AudioEventHub 里启用事件订阅)。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Audio/Level Audio Binding", fileName = "NewLevelAudioBinding", order = 105)]
    public class LevelAudioBinding : ScriptableObject
    {
        [Tooltip("此绑定对应的关卡。LevelEditor 后续扩展时按 LevelDefinition 匹配。")]
        public ShinySTG.Level.LevelDefinition Level;

        [Header("BGM")]
        [Tooltip("关卡默认 BGM 列表(可顺序/随机循环)。")]
        public BgmPlaylist Playlist;

        [Tooltip("Boss 出现时切换到此单首 BGM(可空 = 不切换)。\n" +
                 "LevelEditor 后续扩展时可在 SpawnEntry 选中 Boss 时指定。")]
        public BgmTrack BossMusic;

        [Tooltip("Boss 被击败时切换到此单首 BGM(可空 = 不切换)。\n" +
                 "通常用来播胜利音乐。")]
        public BgmTrack DefeatMusic;

        [Header("Transition")]
        [Tooltip("关卡 → BossMusic 的交叉淡化时长(秒)。")]
        [Range(0f, 5f)] public float ToBossCrossfade = 2f;

        [Tooltip("Boss → DefeatMusic 的交叉淡化时长(秒)。")]
        [Range(0f, 5f)] public float ToDefeatCrossfade = 1.5f;
    }
}