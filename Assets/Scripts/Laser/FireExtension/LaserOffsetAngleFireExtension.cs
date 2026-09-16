using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 激光角度偏移扩展:单次累加型模块 —— 在 currentAngleRad(上一步产出的角度)上叠加一个固定偏移角,产出新角度。
///
/// ★ 设计要点 ★
/// 与"覆盖型" (BaseAngleLaserFireExtension / PlayerAimLaserFireExtension) 对仗:
///   - BaseAngleLaserFireExtension   = "锚点",忽略上一步,直接覆盖;
///   - PlayerAimLaserFireExtension   = "瞄准",瞄得到则覆盖,瞄不到则透传;
///   - OffsetAngleLaserFireExtension = "累加",在已有方向上叠加 N°,**不覆盖**。
///
/// pipeline 示例:
///   [PlayerAim, Offset Angle(+180°)]        → 玩家方向的反方向(常见 "绕后激光")
///   [PlayerAim, Offset Angle(+30°)]         → 玩家方向 + 30°(从玩家右侧掠过的激光)
///   [PlayerAim, Offset Angle(-30°)]         → 玩家方向 - 30°(从玩家左侧掠过的激光)
///   [Base(270°), Offset Angle(+15°)]        → 默认向下,整体右偏 15°
///   [Base(270°), PlayerAim, Offset Angle(+180°)]  → 瞄得到玩家:玩家反方向;瞄不到:向下
///
/// ★ 与子弹 OffsetAngleFireExtension 的差异 ★
/// 激光版本是"单条一个方向"—— 不存在每发累加(没有 bulletIndex 概念)。
/// 因此本模块没有 ProcessAngleForBullet override:ProcessAngle 已经直接定义了"中线"角度,
/// 整条激光就这一个方向。
///
/// 若想要"批次累加"(每次开火角度递进),直接用 AccumulatingOffsetAngleLaserFireExtension
/// (覆盖了 BaseOffset SR 多态 + StepOffset 批次累加),详见该类顶部注释。
///
/// ★ 命名空间说明 ★
/// 本文件放在**全局命名空间**(与 LaserFireExtension.cs / LaserOffsetAngleFireExtension.cs / FireSound.cs 同款),
/// 不要放 namespace ShinySTG.Laser 里 —— 否则 LaserPattern.cs 看不到(踩坑记录见 CONTRIBUTING §4.7)。
/// </summary>
[Serializable, SRName("LaserFireExtension/Offset Angle")]
public class OffsetAngleLaserFireExtension : LaserFireExtension
{
    [Tooltip("叠加角度偏移(度)。\n" +
             "正数 = 在 currentAngleRad 上**逆时针**叠加 N°;\n" +
             "负数 = **顺时针**叠加 N°。\n" +
             "0 = 不偏移(本模块等价 \"透传\",通常不会放在数组里)。\n" +
             "常见用途:\n" +
             "  - +180° = 反方向(配合 PlayerAim 实现 \"绕后激光\":瞄向玩家但激光从玩家背后射出)\n" +
             "  - +30° / -30° = 偏离玩家 N°(激光从玩家左/右侧掠过)\n" +
             "  - +15° = 在 Base 默认方向上整体偏一点(几何微调)\n" +
             "STG 角度约定:0=右, 90=上, 180=左, 270=下(详见 ARCHITECTURE §2.1)。")]
    public float OffsetAngle = 0f;

    public override float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad)
        => currentAngleRad + OffsetAngle * Mathf.Deg2Rad;
}