using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// FirePattern 开火音多态模块 —— 与 FireExtension 同套路,按 Inspector 数组顺序触发。
///
/// ★ 触发时机 ★
///   在 BulletPool.FireGroup(...) 入口触发,每次"开火组"播一次。
///   - 同一 pattern 在同一帧 LoopThroughPatterns=true 喷 N 次 → 调 N 次 FireGroup → 播 N 次音
///     (这是想要的行为,否则玩家按住开火键没声音反馈)
///   - CompositeFirePattern 内部递归 Fire() 不走 BulletPool.FireGroup → 子 pattern 的 FireSounds 不触发
///     (Composite 作为整体发声,不要"每个子弹幕各发一次")
///
/// ★ 与 FireExtension 的区别 ★
///   - FireExtension = 角度管道(上一步输出角度 → 下一步输入角度)
///   - FireSound     = 并行触发器(每个模块独立播一个音,可叠播多个 cue)
///
/// 留空数组 = 不播放(性能开销 ≈ 0,只走一次长度检查)。
///
/// 嵌入位置:FirePattern.cs 新增 `public FireSound[] FireSounds;` 字段,
/// 由 BulletPool.FireGroup 在调 pattern.Fire(...) 前调一次 PlayFireSounds()。
///
/// ★ 命名空间说明 ★
///   本文件放在**全局命名空间**(与 FireExtension.cs / FirePattern.cs / BulletModifier.cs 同款),
///   不要放 namespace ShinySTG 里 —— 否则 FirePattern.cs 看不到(踩坑记录见 CONTRIBUTING §4.7)。
/// </summary>
[Serializable]
public abstract class FireSound
{
    /// <summary>
    /// 触发一次开火音。每次 BulletPool.FireGroup 调用都会触发一次。
    /// </summary>
    /// <param name="position">发射点世界坐标(可用于 2D 定位发声)</param>
    /// <param name="ownerHitbox">发射者 Hitbox(可为 null)。子类可用 ownerHitbox.Team 区分敌我。</param>
    public abstract void OnFireTriggered(Vector2 position, ShinySTG.Hitbox.HitboxComponent ownerHitbox);
}

/// <summary>
/// 走 SfxCue 体系触发开火音 —— 限流 / Pipeline / Bus 路由 / PlayerPrefs 全部继承。
///
/// 推荐用法:
///   - 想让"激光"pattern 在玩家处发"滋——"音:把 Laser SfxCue 资产拖到 Cue 字段
///   - MaxVoices=2、Cooldown=0.02 在 SfxCue 资产上配即可,这里只放临时覆盖
///
/// 与 PlayerShooting._shootSfx 的区别(可并存):
///   - _shootSfx  = "玩家整体开火"  —— 不论哪个 pattern,只要玩家开火就播
///   - SfxCueFireSound = "特定 pattern 的特征音" —— 不同 pattern 可配不同 cue,
///                       敌人/Boss 用同一 pattern 时自动不带这个音(若 pattern 资产共用)
/// </summary>
[Serializable, SRName("FireSound/SFX Cue")]
public class SfxCueFireSound : FireSound
{
    [Tooltip("开火音 cue。留空 = 不播(本模块被禁用,继续数组后续模块)。")]
    public ShinySTG.Audio.SfxCue Cue;

    [Range(0f, 1f)]
    [Tooltip("临时音量倍率(乘到 cue.DefaultVolume 上)。\n" +
             "1 = 用 cue 默认音量;0.5 = 砍半。")]
    public float VolumeMul = 1f;

    [Tooltip("临时 pitch(乘到 cue.DefaultPitch 上)。\n" +
             "1 = 原始音高;1.2 = 升 20%。")]
    public float Pitch = 1f;

    public override void OnFireTriggered(Vector2 position, ShinySTG.Hitbox.HitboxComponent ownerHitbox)
    {
        if (Cue == null) return;
        ShinySTG.Audio.AudioMix.PlaySfx(Cue, position: position, volumeMul: VolumeMul, pitch: Pitch);
    }
}

/// <summary>
/// 显式"不播音"占位 —— 仅用于在数组里"看到"这里有个槽但要禁用,
/// 留空数组也能达到同样效果。实际很少用,放这里是为了 Inspector 下拉菜单完整。
/// </summary>
[Serializable, SRName("FireSound/None")]
public class NullFireSound : FireSound
{
    public override void OnFireTriggered(Vector2 position, ShinySTG.Hitbox.HitboxComponent ownerHitbox) { }
}
