using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// BGM 播放列表 —— 多首 BgmTrack 的有序集合。
    ///
    /// 用法:
    ///   - AudioMix.PlayPlaylist(playlist)  → 按 Shuffle 设置顺序/随机播放整个列表,播完停止
    ///   - AudioMix.PlayPlaylist(playlist, loop: true) → 列表播完后从第一首再开始
    ///
    /// 设计要点:
    ///   - 每首 BGM 播完自动切下一首,过渡用 CrossfadeTransition(项目目前只内置这一种)。
    ///   - Shuffle=true 用 Fisher-Yates 洗牌 —— 但只洗一次(每次调 PlayPlaylist 重新洗),
    ///     不做"播完再洗"避免连续两轮同序。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Audio/BGM Playlist", fileName = "NewBgmPlaylist", order = 103)]
    public class BgmPlaylist : ScriptableObject
    {
        [Tooltip("BGM 轨道列表。空数组 = 不播放任何东西(但不报错)。")]
        public BgmTrack[] Tracks;

        [Tooltip("是否随机播放。true = Fisher-Yates 洗牌后顺序播;false = 按数组顺序。\n" +
                 "每次调 PlayPlaylist 重新洗,不记忆跨调用顺序。")]
        public bool Shuffle = false;

        [Tooltip("播完整个列表后是否从头再开始。")]
        public bool Loop = true;

        [Tooltip("两首 BGM 之间的交叉淡化时长(秒)。0 = 硬切(但仍用同一个 CrossfadeTransition 容器,无淡入淡出)。")]
        [Range(0f, 5f)]
        public float CrossfadeDuration = 1.5f;
    }
}
