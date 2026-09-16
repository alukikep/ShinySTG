using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// LaserPattern 子类的"基础发射逻辑扩展点"多态模块 —— 对基础发射逻辑(激光中线方向)的可插拔扩展。
///
/// ★★★ 架构:模块数组 + Pipeline 模型(对齐 Bullet.FireExtension) ★★★
///
/// LaserPattern.FireExtensions 是一个 LaserFireExtension[] 数组,数组里每个模块按顺序串成一个角度管道:
///   angle = rotationRad                                                  ← 起点(外部累积角)
///   for ext in FireExtensions (按数组顺序遍历):
///       angle = ext.ProcessAngle(from, rotationRad, angle)                ← 上一步角度 → 下一步角度
///   return angle
///
/// 与子弹 FireExtension 的核心差异:
///   ★★★ 激光每次 Fire() 只生成 1 条 ★★★
///   - 没有 bulletIndex / totalCount 概念 → **不需要** ProcessAngleForBullet
///   - AccumulatingOffset 不带 OffsetPerBullet 字段(每发累加对激光无意义)
///   - 其他维度(Base / PlayerAim / 单次 Offset / 批次累加)与子弹版 1:1 对齐
///
/// Pipeline 模型与数组顺序语义(与子弹版完全一致):
///   [Base(270°), PlayerAim]          → 270° 起步 → 被 PlayerAim 拽向玩家(瞄不到时透传 270°)
///   [PlayerAim, Offset Angle(+180°)] → 玩家方向 + 180°(绕后激光)
///   [Base(0°), Offset Angle(+30°)]   → 0° 起步 → +30°(向右偏 30° 激光)
///   [] (空数组)                      → fallback 到 270° + rotationRad(等价旧版 default)
///
/// 对齐 SpawnEntry / BossSignal / OptionPositionForm / FireExtension 的扩展套路:
///   加新扩展 = 新建 LaserFireExtension 子类 + 加 [SRName("LaserFireExtension/<名字>")],
///   所有 LaserPattern 资产的 FireExtensions 数组下拉自动出现新选项,无需改任何现有 LaserPattern 子类。
///
/// 接口设计预留扩展性:
///   - ProcessAngle(from, baseRotationRad, currentAngleRad) → 单次角度处理(必备,所有子类都实现)
///   - OnFireGroupTriggered(int fireCount) → 每批开火钩子(累加型 override,其余默认空)
///   - ComputeAngleRad(旧接口):保留以兼容可能的外部调用者,默认转发到 ProcessAngle。
///
/// 默认行为(FireExtensions == null 或空数组):
///   Resolver 兜底到 "270° + rotationRad",保证旧资产零行为变更(旧资产反序列化后 FireExtensions 为 null/空)。
///
/// ★ 命名空间说明 ★
/// 本文件放在**全局命名空间**(与 FireExtension.cs / FireSound.cs / Bullet.cs 同款),
/// 不要放 namespace ShinySTG.Laser 里 —— 否则 LaserPattern.cs 看不到(踩坑记录见 CONTRIBUTING §4.7)。
/// 类名前缀 `Laser` 是为了与子弹 FireExtension 区分(避免两个全局类同名冲突)。
/// </summary>
[Serializable]
public abstract class LaserFireExtension
{
    /// <summary>
    /// Pipeline 核心:处理当前角度,产出下一步角度。
    /// </summary>
    /// <param name="from">发射点位置(世界坐标)。子类若需要瞄准,自己算 to-target。</param>
    /// <param name="baseRotationRad">
    /// 起点 rotationRad(由 FireLaserAction 等累积传进来,本次发射"原始"方向增量)。
    /// 子类若需要"覆盖"中心方向,通常以 baseRotationRad 为基础;若需要"参考原始方向",用此参数。
    /// </param>
    /// <param name="currentAngleRad">
    /// 数组里"上一步模块"产出的角度。第一步 = baseRotationRad;之后每步替换为上一步的产出。
    /// </param>
    /// <returns>本步产出的角度,作为下一步的 currentAngleRad。</returns>
    public abstract float ProcessAngle(Vector2 from, float baseRotationRad, float currentAngleRad);

    /// <summary>
    /// 本批 FireGroup 钩子:由 <see cref="LaserPool.FireGroup"/> 在每次"开火组"入口处对本数组里
    /// 每一个 LaserFireExtension 元素调一次(在 ProcessAngle 之前)。
    ///
    /// ★ 用途 ★
    /// 让累加型 LaserFireExtension(例:AccumulatingOffsetAngleLaserFireExtension)能在每次开火时更新自己的
    /// per-FireGroup 累加状态,实现"每次发射后累加一个偏移角"。
    ///
    /// ★ per-instance 隔离 ★
    /// <paramref name="fireCount"/> 由 LaserPool 按 (pattern 资产 ref + FireExtensions 数组里
    /// 的具体元素 ref) 维护,确保同一份 LaserPattern SO 资产被多敌人共用时累加计数互不污染。
    /// 第 1 次调用 fireCount = 1,第 2 次 = 2,以此类推。
    ///
    /// 默认空实现 —— 不关心"开火批次累加"的子类(覆盖型 / 透传型)无需 override。
    /// </summary>
    public virtual void OnFireGroupTriggered(int fireCount) { }

    /// <summary>
    /// [旧接口] 计算本轮发射的"中线方向"(弧度)。已弃用,新代码请走 ProcessAngle。
    /// 默认实现 = 把 currentAngleRad 设为 baseRotationRad 走一次 ProcessAngle(等价于"单模块 pipeline")。
    /// </summary>
    [Obsolete("走 ProcessAngle 参与 pipeline,而不是单点调用。LaserPattern 子类统一通过 LaserFireExtensionResolver.ResolvePipeline 调用。")]
    public virtual float ComputeAngleRad(Vector2 from, float rotationRad)
        => ProcessAngle(from, rotationRad, rotationRad);
}

/// <summary>
/// 基础角度扩展:覆盖型模块 —— 直接把角度设为 BaseAngle + baseRotationRad(忽略上一步的 currentAngleRad)。
///
/// 与子弹 BaseAngleFireExtension 1:1 对仗,字段语义完全相同。
///
/// 用途:作为 pipeline 的"锚点",提供固定方向的兜底。
/// 通常放在数组的第一位:
///   [Base(270°)]                  → 向下开火
///   [Base(270°), PlayerAim]       → 先按 270° 起步 → 再被 PlayerAim 拽向玩家
///
/// 如果放在数组中后段,它的"覆盖"语义会"砸掉"前面的模块(请谨慎)。
///
/// PositionOffset:实现"炮口偏移"(详见字段 Tooltip)。由 Resolver 在 pipeline 入口处应用,
/// 作用于所有后续模块(包括 PlayerAim 的瞄准参考点)。
/// </summary>
[Serializable, SRName("LaserFireExtension/Base")]
public class BaseAngleLaserFireExtension : LaserFireExtension
{
    [Tooltip("整体基准朝向(度)。0=右,90=上,180=左,270=下。\n" +
             "STG 激光常用:0/90/180/270 四向,或 +45/-45 斜向。\n" +
             "本模块是覆盖型:放在数组第一位提供锚点方向,放在中后段会覆盖前面模块的产出。")]
    public float BaseAngle = 270f;

    [Tooltip("实际发射位置相对标准位置的偏移(世界坐标)。\n" +
             "  - 标准位置 = LaserPattern 调用方传入的 position(敌人/Boss/玩家所在位置)\n" +
             "  - 实际位置 = 标准位置 + PositionOffset\n" +
             "  - (0, 0) = 不偏移(默认值,等价旧版行为)\n" +
             "  - (0, 0.5) = 在标准位置上方 0.5 单位开火(适合\"激光头在头顶\")\n" +
             "  - (0.3, 0) = 在标准位置右侧 0.3 单位开火(适合\"双管炮左右偏移\")\n" +
             "生效时机:Resolver 入口处(整个 pipeline 开始之前)。\n" +
             "意味着:偏移作用于所有后续模块(例如 [Base(偏移), PlayerAim] 会让 PlayerAim 用修正后的位置做瞄准参考)。")]
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
/// 与子弹 PlayerAimFireExtension 1:1 对仗,语义完全相同。
///
/// ★ 设计要点 ★
///   删掉 fallback 字段,瞄不到时直接透传 currentAngleRad。
///   兜底职责交给上游的 Base 模块(把 Base 放数组第一位即可)。
///
///   pipeline 示例:
///     [Base(270°), PlayerAim]     → 默认向下,瞄得到玩家时改指向玩家;瞄不到仍向下
///     [Base(0°), PlayerAim]       → 默认向右,瞄得到玩家时改指向玩家;瞄不到仍向右
///     [PlayerAim]                 → 瞄得到玩家 → 指向玩家;瞄不到 → fallback 到 Resolver 的 270°
///
/// 注意:本类显式用全限定名 ShinySTG.Player.Player.Instance ——
/// 因为 namespace ShinySTG.Player 与类 Player 同名,using 会把 Player 解析为 namespace 导致
/// Player.Instance 找不到类成员。踩坑见 Assets/Scripts/Hitbox/CollisionService.cs 第 4-6 行注释。
/// </summary>
[Serializable, SRName("LaserFireExtension/Player Aim")]
public class PlayerAimLaserFireExtension : LaserFireExtension
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