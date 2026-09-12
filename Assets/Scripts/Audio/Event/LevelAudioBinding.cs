using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 关卡 → BGM 绑定资产 —— 把 LevelDefinition 资产与 BGM 资产配对。
    ///
    /// ★ 推荐用法 ★
    ///   把 LevelAudioBinding 拖到 LevelDefinition.AudioBinding 字段(关卡资产一站式管理)。
    ///   LevelController.BeginLevel() 会调 AudioEventHub.TryBind(definition) 自动启用切歌:
    ///     - OnLevelStart    → Playlist(关卡默认 BGM)
    ///     - OnBossSpawned   → BossMusic(Boss 出场切到 Boss BGM)
    ///     - OnBossDefeated  → DefeatMusic(Boss 击败切到胜利 BGM)
    ///
    /// ★ 向后兼容用法 ★
    ///   AudioSystem.LevelBindings[] 是全局查表模式:多个 LevelDefinition 共享同一套 binding 模板时使用。
    ///   当 LevelDefinition.AudioBinding 为空时,AudioEventHub 才会去 LevelBindings 里按 Level 字段匹配。
    ///
    /// ★ 创建便利 ★
    ///   关卡编辑器菜单 STG → Level Editor → 选中关卡 → 工具栏「+ Create AudioBinding」
    ///   一键在关卡同目录创建同名 _AudioBinding.asset + 双向反引用。
    ///
    /// 与既有层关系:
    ///   - 完全正交:不修改 LevelController / AudioSystem 的接口,仅作为「数据容器」存在。
    ///   - AudioSystem.LevelBindings 字段保留作为向后兼容入口(老用法仍可工作)。
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