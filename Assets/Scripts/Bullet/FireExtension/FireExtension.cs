using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// FirePattern 子类的"基础发射逻辑扩展点"多态模块 —— 对基础发射逻辑(中线方向)的可插拔扩展。
///
/// ★★★ 架构:模块数组 + Pipeline 模型 ★★★
///
/// FirePattern.FireExtensions 是一个 FireExtension[] 数组,数组里每个模块按顺序串成一个角度管道:
///   center = rotationRad                                        ← 起点(外部累积角)
///   for ext in FireExtensions (按数组顺序遍历):
///       center = ext.ProcessAngle(from, rotationRad, center)    ← 上一步角度 → 下一步角度
///   return center
///
/// 每个模块都接收"上一步产出的角度"和"起点 rotationRad",产出"下一步要用的角度"。
/// 模块可以"覆盖"上一步结果(比如 PlayerAim 直接覆盖为指向玩家),也可以"修饰/累加"(未来加 RotationOffset 这种)。
///
/// 数组顺序就是执行顺序,改顺序 = 改语义。例:
///   [Base(0°), PlayerAim]                    → 0° 起步 → 被 PlayerAim 拽向玩家
///   [PlayerAim, BaseRotation(+30°)]          → 先瞄玩家 → 再叠 +30°(指向玩家偏 30°)
///   [] (空数组)                              → fallback 到 270° + rotationRad(等价旧版 default)
///
/// 对齐 SpawnEntry / BossSignal / OptionPositionForm / ModifierPrefabs 的扩展套路:
///   加新扩展(瞄准 Boss / 瞄准最近敌人 / 每发旋转 N° / 振荡 / ...) = 新建 FireExtension 子类 + 加 [SRName("FireExtension/<名字>")],
///   所有 FirePattern 资产的 FireExtensions 数组下拉自动出现新选项,无需改任何现有 FirePattern 子类。
///
/// 接口设计预留扩展性:
///   - ProcessAngle(from, baseRotationRad, currentAngleRad) → 单次角度处理(必备,所有子类都实现)
///   - ProcessAngleForBullet(from, bulletIndex, totalCount, baseRotationRad, currentAngleRad) → 每发独立方向
///     默认实现 = 调 ProcessAngle(...) 拿到"中线",等价于"中线 + 等分"语义。
///   - ComputeAngleRad / ComputeAngleRadForBullet(旧接口):保留以兼容可能的外部调用者,默认转发到 ProcessAngle。
///
/// 默认行为(FireExtensions == null 或空数组):
///   Resolver 兜底到 "270° + rotationRad",保证旧资产零行为变更(旧资产反序列化后 FireExtensions 为 null/空)。
/// </summary>
[Serializable]
public abstract class FireExtension
{
    /// <summary>
    /// Pipeline 核心:处理当前角度,产出下一步角度。
    /// </summary>
    /// <param name="from">发射点位置(世界坐标)。子类若需要瞄准,自己算 to-target。</param>
    /// <param name="baseRotationRad">
    /// 起点 rotationRad(由 FireAction 等累积传进来,本次发射"原始"方向增量)。
    /// 子类若需要"覆盖"中心方向,通常以 baseRotationRad 为基础;若需要"参考原始方向",用此参数。
    /// </param>
    /// <param name="currentAngleRad">
    /// 数组里"上一步模块"产出的角度。第一步 = baseRotationRad;之后每步替换为上一步的产出。
    /// </param>
    /// <returns>本步产出的角度,作为下一步的 currentAngleRad。</returns>
    public abstract float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad);

    /// <summary>
    /// Pipeline 每发独立方向版:处理"第 bulletIndex 颗"子弹的方向。
    /// 默认实现 = 调 ProcessAngle(...) 拿中心方向,即"中线 + 等分"语义。
    /// 想要"每发独立方向"的子类 override 此方法(比如"每发旋转 N°" / "延迟扇形")。
    /// </summary>
    public virtual float ProcessAngleForBullet(Vector2 from, int bulletIndex, int totalCount,
                                              float baseRotationRad, float currentAngleRad)
        => ProcessAngle(from, baseRotationRad, currentAngleRad);

    // ---- 旧接口兼容 ----
    // 旧版接口 (ComputeAngleRad / ComputeAngleRadForBullet) 保留以兼容可能的外部调用者,
    // 但不推荐新代码使用 —— 请走 ProcessAngle 走 pipeline。

    /// <summary>
    /// [旧接口] 计算本轮发射的"中心方向"(弧度)。已弃用,新代码请走 ProcessAngle。
    /// 默认实现 = 把 currentAngleRad 设为 baseRotationRad 走一次 ProcessAngle(等价于"单模块 pipeline")。
    /// </summary>
    [Obsolete("走 ProcessAngle 参与 pipeline,而不是单点调用。FirePattern 子类统一通过 FireExtensionResolver.ResolvePipeline 调用。")]
    public virtual float ComputeAngleRad(Vector2 from, float rotationRad)
        => ProcessAngle(from, rotationRad, rotationRad);

    /// <summary>
    /// [旧接口] 每发独立方向。已弃用,新代码请走 ProcessAngleForBullet。
    /// </summary>
    [Obsolete("走 ProcessAngleForBullet 参与 pipeline。")]
    public virtual float ComputeAngleRadForBullet(Vector2 from, int bulletIndex, int totalCount, float rotationRad)
        => ProcessAngleForBullet(from, bulletIndex, totalCount, rotationRad, rotationRad);
}

/// <summary>
/// 基础角度扩展:覆盖型模块,直接把角度设为 BaseAngle + baseRotationRad(忽略上一步的 currentAngleRad)。
///
/// 用途:作为 pipeline 的"锚点",提供固定方向的兜底。
/// 通常放在数组的第一位:
///   [Base(270°)]                  → 向下开火
///   [Base(270°), PlayerAim]       → 先按 270° 起步 → 再被 PlayerAim 拽向玩家
///
/// 如果放在数组中后段,它的"覆盖"语义会"砸掉"前面的模块(请谨慎)。
/// </summary>
[Serializable, SRName("FireExtension/Base")]
public class BaseAngleFireExtension : FireExtension
{
    [Tooltip("整体基准朝向(度)。0=右,90=上,180=左,270=下。\n" +
             "STG 敌人最常用 270(向下射击)。\n" +
             "本模块是覆盖型:放在数组第一位提供锚点方向,放在中后段会覆盖前面模块的产出。")]
    public float BaseAngle = 270f;

    [Tooltip("实际发射位置相对标准位置的偏移(世界坐标)。\n" +
             "  - 标准位置 = FirePattern 调用方传入的 position(敌人/Boss/玩家所在位置)\n" +
             "  - 实际位置 = 标准位置 + PositionOffset\n" +
             "  - (0, 0) = 不偏移(默认值,等价旧版行为)\n" +
             "  - (0, 0.5) = 在标准位置上方 0.5 单位开火(适合\"肩扛炮口在头顶\")\n" +
             "  - (0.3, 0) = 在标准位置右侧 0.3 单位开火(适合\"双管炮左右偏移\")\n" +
             "生效时机:Resolver 入口处(整个 pipeline 开始之前)。\n" +
             "意味着:偏移作用于所有后续模块(例如 [Base(偏移), PlayerAim] 会让 PlayerAim 用修正后的位置做瞄准参考)。\n" +
             "如果未来需要\"瞄准用标准位置,发射用偏移位置\"的语义,新建独立的 PositionOffsetFireExtension 模块即可(不破坏当前接口)。")]
    public Vector2 PositionOffset = Vector2.zero;

    public override float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad)
        => BaseAngle * Mathf.Deg2Rad + baseRotationRad;

    // 旧接口兼容:保持旧版语义(忽略 pipeline 的 currentAngleRad,直接 baseRotationRad 起算)
#pragma warning disable CS0809 // Obsolete member overrides Obsolete member
    [Obsolete("走 ProcessAngle 参与 pipeline。")]
    public override float ComputeAngleRad(Vector2 from, float rotationRad)
        => BaseAngle * Mathf.Deg2Rad + rotationRad;
#pragma warning restore CS0809
}

/// <summary>
/// 瞄准玩家单例:覆盖型模块 —— 瞄得到玩家,直接把角度覆盖为指向玩家;瞄不到玩家,**透传**上一步角度。
///
/// ★ 设计要点 ★
///   旧版(PlayerAim.BaseAngle):自己持有 fallback 字段,瞄不到时退回 BaseAngle —— 语义脏。
///   新版:删掉 BaseAngle 字段,瞄不到时直接透传 currentAngleRad。
///     兜底职责交给上游的 Base 模块(把 Base 放数组第一位即可)。
///
///   pipeline 示例:
///     [Base(270°), PlayerAim]     → 默认向下,瞄得到玩家时改指向玩家;瞄不到仍向下(等价旧版"瞄准 + 兜底")
///     [PlayerAim, Base(270°)]     → 玩家拽方向 → 再被 Base 砸回 270°(几乎不用)
///     [PlayerAim]                 → 瞄得到玩家 → 指向玩家;瞄不到 → fallback 到 Resolver 的 270°(管道起点的 baseRotationRad 是 rotationRad)
///     [Base(0°), PlayerAim]       → 默认向右,瞄得到玩家时改指向玩家;瞄不到仍向右
///
/// 注意:本类显式用全限定名 ShinySTG.Player.Player.Instance ——
/// 因为 namespace ShinySTG.Player 与类 Player 同名,using 会把 Player 解析为 namespace 导致
/// Player.Instance 找不到类成员。踩坑见 Assets/Scripts/Hitbox/CollisionService.cs 第 4-6 行注释。
/// </summary>
[Serializable, SRName("FireExtension/Player Aim")]
public class PlayerAimFireExtension : FireExtension
{
    public override float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad)
    {
        var p = ShinySTG.Player.Player.Instance;
        if (p == null) return currentAngleRad; // 瞄不到 → 透传,让上游 Base 兜底
        Vector2 to = (Vector2)p.transform.position - from;
        return Mathf.Atan2(to.y, to.x);        // 瞄得到 → 覆盖型
    }

    // 旧接口兼容:瞄不到时 fallback 到 270° + rotationRad(旧版语义;Pipeline 版本通过 currentAngleRad 透传给上游)
#pragma warning disable CS0809
    [Obsolete("走 ProcessAngle 参与 pipeline。")]
    public override float ComputeAngleRad(Vector2 from, float rotationRad)
    {
        var p = ShinySTG.Player.Player.Instance;
        if (p == null) return 270f * Mathf.Deg2Rad + rotationRad;
        Vector2 to = (Vector2)p.transform.position - from;
        return Mathf.Atan2(to.y, to.x);
    }
#pragma warning restore CS0809
}
