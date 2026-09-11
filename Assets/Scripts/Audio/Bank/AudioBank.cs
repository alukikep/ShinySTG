using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// 一组 SfxCue 的命名集合 —— 方便批量加载 + 在 Inspector 里分类查看。
    ///
    /// 使用场景:
    ///   - "玩家音效.bank" 装 Hit/Damage/Death/Shoot/Graze 等 cue
    ///   - "敌人音效.bank" 装 Explode/Hit/Roar 等 cue
    ///   - "UI 音效.bank"   装 Click/Hover/Confirm/Cancel 等 cue
    ///
    /// 本身只是个分组容器,不强制使用 —— 调用方可以直接引用 SfxCue 资产。
    /// 仅当你希望"一次加载一组 cue"或"在 Inspector 里按类别组织"时才用本类。
    /// 走 Resources 子目录加载(AudioSystem 自动枚举),不进 Addressables(项目暂未用)。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Audio/SFX Bank", fileName = "NewAudioBank", order = 101)]
    public class AudioBank : ScriptableObject
    {
        [Tooltip("本 Bank 的所有 SfxCue。仅作为 Inspector 分组,不强制调用方走 Bank 引用 cue —— " +
                 "直接拖 SfxCue 到 PlayerShooting.HitSfx 等字段也可以。")]
        public SfxCue[] Cues;
    }
}
