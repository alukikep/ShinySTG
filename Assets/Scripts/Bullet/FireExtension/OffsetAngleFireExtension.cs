using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 角度偏移扩展:累加型模块 —— 在 currentAngleRad(上一步产出的角度)上叠加一个固定偏移角,产出新角度。
///
/// ★ 设计要点 ★
///   与 \"覆盖型\" (BaseAngleFireExtension / PlayerAimFireExtension) 对仗:
///     - BaseAngleFireExtension 是 \"锚点\",忽略上一步,直接覆盖;
///     - PlayerAimFireExtension 是 \"瞄准\",瞄得到则覆盖,瞄不到则透传;
///     - OffsetAngleFireExtension 是 \"累加\",在已有方向上叠加 N°,**不覆盖**。
///
///   pipeline 示例:
///     [PlayerAim, Offset Angle(+180°)]        → 玩家方向的反方向(常见 \"绕后弹\")
///     [PlayerAim, Offset Angle(+30°)]         → 玩家方向 + 30°(从玩家右侧掠过)
///     [PlayerAim, Offset Angle(-30°)]         → 玩家方向 - 30°(从玩家左侧掠过)
///     [Base(270°), Offset Angle(+15°)]        → 默认向下,每发整体右偏 15°
///     [Base(270°), PlayerAim, Offset Angle(+180°)]  → 瞄得到玩家:玩家反方向;瞄不到:向下
///
///   注意:本模块**不** override ProcessAngleForBullet。
///     默认实现 = 调 ProcessAngle(...) 拿 \"中线\",再叠加 OffsetAngle。
///     Ring / Arc / Line 子类按 \"中线 + 等分\" 语义工作;若想 \"每发独立累加\",新建
///     PerBulletOffsetAngleFireExtension 单独 override ProcessAngleForBullet 即可
///     (不破坏当前接口,与 ARCHITECTURE §3.1 预留的 \"累加型\" 扩展点对齐)。
///
/// ★ 命名空间说明 ★
///   本文件放在**全局命名空间**(与 FireExtension.cs / FireSound.cs / Bullet.cs 同款),
///   不要放 namespace ShinySTG.Bullet 里 —— 否则 FirePattern.cs 看不到(踩坑记录见 CONTRIBUTING §4.7)。
/// </summary>
[Serializable, SRName("FireExtension/Offset Angle")]
public class OffsetAngleFireExtension : FireExtension
{
    [Tooltip("叠加角度偏移(度)。\n" +
             "正数 = 在 currentAngleRad 上**逆时针**叠加 N°;\n" +
             "负数 = **顺时针**叠加 N°。\n" +
             "0 = 不偏移(本模块等价 \"透传\",通常不会放在数组里)。\n" +
             "常见用途:\n" +
             "  - +180° = 反方向(配合 PlayerAim 实现 \"绕后弹\":瞄向玩家但飞向玩家背后)\n" +
             "  - +30° / -30° = 偏离玩家 N°(从玩家左/右侧掠过)\n" +
             "  - +15° = 在 Base 默认方向上整体偏一点(几何微调)\n" +
             "STG 角度约定:0=右, 90=上, 180=左, 270=下(详见 ARCHITECTURE §2.1)。")]
    public float OffsetAngle = 0f;

    public override float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad)
        => currentAngleRad + OffsetAngle * Mathf.Deg2Rad;

    // ProcessAngleForBullet 沿用基类默认实现:走 ProcessAngle 拿中线,
    // Ring/Arc/Line 等子类按 \"中线 + 等分\" 语义分摊,本模块作为整体累加。
    // 若未来需要 \"每发独立累加\",override 此方法即可,不破坏现有接口。
}

