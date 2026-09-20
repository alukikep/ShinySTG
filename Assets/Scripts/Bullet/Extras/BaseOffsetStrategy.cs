using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 「本批次第一次 FireOnce 的恒定偏移值」采样策略 —— 抽象基类。
///
/// 设计动机:
///   <see cref="AngleOffsetFirePatternBulletExtra.BaseOffset"/> 之前是固定 float。
///   用户希望支持两种语义:精确值(Fixed,旧行为)与区间随机(Random Range)。
///   走 [SerializeReference] 多态下拉,与项目内所有策略类(SpawnFogConfig / FireExtension / SfxRule)
///   保持同一套路 —— Inspector 只显示当前模式字段,不显示无关字段。
///
/// 接口:
///   - <see cref="Sample"/> → 返回本次要施加的偏移值(度)。AngleOffsetFirePatternBulletExtra
///     在第一次 FireOnce 时调一次 Sample,之后缓存结果在窗口期内复用。
///
/// 字段约定(与 BulletModifier 一致):
///   - 纯值类型字段默认 MemberwiseClone 即可
///   - 持有引用类型字段的子类必须 override Clone 深拷
/// </summary>
[Serializable]
public abstract class BaseOffsetStrategy
{
    /// <summary>
    /// 返回本次要施加的 BaseOffset(度,正负皆可)。
    /// 宿主(AngleOffsetFirePatternBulletExtra)会在第一次 FireOnce 时调一次,之后缓存结果复用。
    /// </summary>
    public abstract float Sample();

    /// <summary>深拷。子类持有引用类型字段时 override;默认 MemberwiseClone 已覆盖纯值类型场景。</summary>
    public virtual BaseOffsetStrategy Clone() => (BaseOffsetStrategy)MemberwiseClone();
}
