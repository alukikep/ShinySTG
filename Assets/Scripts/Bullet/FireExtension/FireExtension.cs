using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// FirePattern 子类的"基础发射逻辑扩展点"多态抽象 —— 对基础发射逻辑(中线方向)的可插拔扩展:
/// 不只是"瞄准"语义,任何"决定本轮发射方向"的策略都属于 FireExtension。
///
/// 对齐 SpawnEntry / BossSignal / OptionPositionForm 的扩展套路:
///   加新扩展(瞄准 Boss / 瞄准最近敌人 / 每发旋转 N° / 振荡 / ...) = 新建 FireExtension 子类 + 加 [SRName("FireExtension/<名字>")],
///   所有 FirePattern 子类的 Inspector FireExtension 下拉自动出现新选项,无需改任何现有 FirePattern 子类。
///
/// 接口设计预留扩展性(当前内置子类只用到"中心方向"语义,但接口已支持"每发独立方向"):
///   - ComputeAngleRad(from, rotationRad) → 中心方向(用于 "中线 + 等分"形态: Ring / Arc / 普通 Line)
///   - ComputeAngleRadForBullet(from, bulletIndex, totalCount, rotationRad) → 每发独立方向(用于 "每发旋转 N°" / "延迟扇形" 等)
///     内置子类 default 实现 = 调 ComputeAngleRad(...)拿到中心方向,等价于"中线 + 等分"。
///
/// 默认行为(FireExtension == null):
///   FirePattern 子类在 extension == null 时 fallback 到 "BaseAngle=270° + rotationRad"(完全等价旧版)。
/// </summary>
[Serializable]
public abstract class FireExtension
{
    /// <summary>
    /// 计算本轮发射的"中心方向"(弧度)。
    /// 由"中线 + 等分"形态(Ring / Arc / 普通 Line)调用,作为子弹分布的中心。
    /// </summary>
    /// <param name="from">发射点位置(世界坐标)</param>
    /// <param name="rotationRad">外部传入的整体方向增量(由 FireAction 等累积传进来)</param>
    public abstract float ComputeAngleRad(Vector2 from, float rotationRad);

    /// <summary>
    /// 计算本轮发射中第 bulletIndex 颗(0-based,共 totalCount 颗)子弹的方向(弧度)。
    /// 用于"每发独立方向"的形态(如每发旋转 N° / 延迟扇形 / 振荡弹幕)。
    ///
    /// 默认实现 = 调 ComputeAngleRad(from, rotationRad) 拿中心方向,即"中线 + 等分"语义。
    /// 想要"每发独立方向"的子类 override 此方法。
    /// </summary>
    public virtual float ComputeAngleRadForBullet(Vector2 from, int bulletIndex, int totalCount, float rotationRad)
        => ComputeAngleRad(from, rotationRad);
}

/// <summary>
/// 基础角度扩展:中心方向 = BaseAngle + rotationRad(等价旧版"normal"模式)。
/// 用途:不想瞄准、不想旋转、就是按固定角度开火。
/// </summary>
[Serializable, SRName("FireExtension/Base")]
public class BaseAngleFireExtension : FireExtension
{
    [Tooltip("整体基准朝向(度)。0=右,90=上,180=左,270=下。\n" +
             "STG 敌人最常用 270(向下射击)。")]
    public float BaseAngle = 270f;

    public override float ComputeAngleRad(Vector2 from, float rotationRad)
        => BaseAngle * Mathf.Deg2Rad + rotationRad;
}

/// <summary>
/// 瞄准玩家单例:中心方向 = atan2(player - from)。
/// 无玩家时退回 BaseAngle + rotationRad(优雅降级,与旧版行为一致)。
///
/// 注意:本类显式用全限定名 ShinySTG.Player.Player.Instance ——
/// 因为 namespace ShinySTG.Player 与类 Player 同名,using 会把 Player 解析为 namespace 导致
/// Player.Instance 找不到类成员。踩坑见 Assets/Scripts/Hitbox/CollisionService.cs 第 4-6 行注释。
/// </summary>
[Serializable, SRName("FireExtension/Player Aim")]
public class PlayerAimFireExtension : FireExtension
{
    [Tooltip("无玩家时的兜底方向(度)。指向玩家时此字段无效。")]
    public float BaseAngle = 270f;

    public override float ComputeAngleRad(Vector2 from, float rotationRad)
    {
        var p = ShinySTG.Player.Player.Instance;
        if (p == null) return BaseAngle * Mathf.Deg2Rad + rotationRad;
        Vector2 to = (Vector2)p.transform.position - from;
        return Mathf.Atan2(to.y, to.x);
    }
}
