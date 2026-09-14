using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 子弹"出生雾化"的配置(纯数据,描述"什么时候、多长时间、什么视觉")。
/// 挂在 FirePattern.SpawnFog 上;Duration=0 或 null = 不雾化(默认,与历史行为 100% 等价)。
///
/// 雾化期内的子弹行为(由 Bullet.cs 统一执行):
///   - 位置固定(不按 Speed 移动)
///   - 不参与碰撞(HitboxComponent.IsFogged=true,CollisionService 各 Tick 阵营过滤后会 continue)
///   - modifier 时间窗口计时器不累计(雾化结束 → 时间窗口从 0 开始)
///   - 视觉走 STG/BulletTintFog shader,通过 MaterialPropertyBlock 写 _FogAmount=1→0
///
/// 复用安全:回池再发射时,Bullet.Init 会重置 FogElapsed / FogDuration / FogCfg / 视觉参数。
///
/// 与 FirePattern.ModifierPrefabs 的关系:
///   - ModifierPrefabs = 子弹生成后的持续行为(追踪 / 加速 / 分裂 / 颜色...)
///   - SpawnFog       = 仅"出生瞬间"的视觉/碰撞/运动压制
///   - 两者正交,互不冲突 —— SpawnFog 期间 modifier 不动,雾化结束 modifier 从头开始计时。
/// </summary>
[Serializable, SRName("Spawn Fog/Default")]
public class SpawnFogConfig
{
    [Tooltip("雾化持续时间(秒)。0 = 不雾化(默认,与历史行为 100% 等价)。\n" +
             "STG 经典值:0.10~0.25(短促);Boss 警示弹:0.3~0.5(给玩家反应时间)。\n" +
             "Duration=0 时,以下视觉字段全部忽略,子弹按普通方式发射。")]
    [Min(0f)] public float Duration = 0f;

    [Tooltip("雾化阶段显示的颜色(覆盖原 _TintColor,雾化结束恢复)。\n" +
             "Alpha=1 完全覆盖;经典 STG 用低饱和白/淡蓝/淡红做'能量凝聚'感。")]
    public Color FogColor = new Color(0.7f, 0.85f, 1f, 1f);

    [Tooltip("雾化期内 sprite 的视觉缩放倍率(从该值 → 1)。\n" +
             "1 = 不缩放,只染色。\n" +
             "1.4 = 出生瞬间大小是正常的 1.4 倍,雾化中收缩到正常(经典'凝聚')。\n" +
             "0.5 = 出生瞬间是正常的 0.5 倍,雾化中放大到正常(反向'展开')。\n" +
             "★ 实现方式:Bullet.cs 走 transform.localScale = _baseLocalScale × fogScale 乘法,\n" +
             "   prefab 的 0.28 美术基准始终保留(雾化期最终 localScale = 0.28 × fogScale,\n" +
             "   清晰后 = 0.28 × 1 = 0.28)。\n" +
             "★ Hitbox 影响:雾化期 IsFogged=true → 不参与碰撞/擦弹;雾化结束恢复 _baseLocalScale,判定盒大小正常。\n" +
             "★ 与 shader vertex 缩放的历史对比:vertex 写法在 FogStartScale>1 时出现'雾团快速移动'视觉异常,\n" +
             "   改用 C# 端 transform.localScale 乘法后 100% 不偏移,语义直观。")]
    [Min(0.01f)] public float FogStartScale = 1.4f;

    [Tooltip("雾化阶段视觉淡入的缓动曲线。None=线性(默认),其它可选 EaseOut / EaseIn / EaseInOut。\n" +
             "★ 雾化结束那一帧的 _FogAmount 一定是 0,与缓动无关(确保清晰'切断',不留余像)。")]
    public FogEasing Easing = FogEasing.None;
}

public enum FogEasing
{
    None,        // 线性:默认
    EaseOut,     // 前期快,后期慢(出生瞬间强,凝聚慢)
    EaseIn,      // 前期慢,后期快(酝酿感)
    EaseInOut,   // 两头慢,中间快
}