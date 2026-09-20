using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 「母弹 → 分裂弹」信息传递的多态模块 —— 抽象基类。
///
/// 设计动机:
///   <see cref="FirePatternBulletModifier"/> 本身在 <see cref="FirePatternBulletModifier.FireOnce"/>
///   那一刻向 BulletPool.FireGroup 传入 rotationRad(基线方向)。用户希望「父弹可以传递部分信息
///   改变子弹的基础逻辑」 —— 比如每次开火后累加一个旋转角偏移,实现「旋转喷射」「扇形铺开」「节拍扩散」等。
///
///   与 FireExtension / FireSound 对仗:
///     - FireExtension   → FirePattern 上的「角度管道」模块(每次发射算一次)
///     - FireSound       → FirePattern 上的「开火音并行触发器」(每次发射算一次)
///     - FirePatternBulletExtra → FirePatternBulletModifier 上的「母弹 → 分裂弹 信息传递」
///                              (每次 FireOnce 算一次)
///
///   命名空间感:
///     - 宿主 = <see cref="FirePatternBulletModifier"/> (modifier,挂在母弹上)
///     - 触发时机 = 宿主 FireOnce 内 BulletPool.FireGroup 之前(改 rotationRad + 可改 host 状态)
///     - 接收方 = 分裂弹(由 FireGroup → pattern.Fire → SpawnBullet → pool.Get 链路创建)
///
/// 接口设计(预留扩展):
///   - <see cref="GetRotationOffset"/>  → 本次 FireOnce 实际传给分裂弹的 rotationRad 增量(弧度)
///   - <see cref="OnFireTriggered"/>   → 钩子:每次 FireOnce 触发一次,子类用来更新自己的累加状态
///   - 未来若要传递更多信息(modifier覆盖 / 位置 override / 阵营 override),加新 virtual 方法即可
///
/// 字段约定(与 BulletModifier 一致):
///   - 值类型字段默认 Clone 即可
///   - 引用类型字段必须 override Clone 深拷
///   - per-instance 累加状态字段标 [NonSerialized],Clone 后自然归零(每次子弹复用重置)
/// </summary>
[Serializable]
public abstract class FirePatternBulletExtra
{
    /// <summary>
    /// 返回本次 FireOnce 要加到 rotationRad 上的额外旋转角(弧度)。
    /// 默认 = 0(不传递)。
    /// </summary>
    public virtual float GetRotationOffset() => 0f;

    /// <summary>
    /// 钩子:宿主 FireOnce 触发时调一次(在调 GetRotationOffset 之前)。
    /// 子类 override 此方法更新自己的累加状态(例:每次触发后 StepOffset 累加)。
    /// 参数 <paramref name="host"/> 是触发本次 FireOnce 的母弹 modifier 宿主(FirePatternBulletModifier 实例)。
    /// 默认空 —— 不传递任何信息的子类不需 override。
    /// </summary>
    public virtual void OnFireTriggered(FirePatternBulletModifier host) { }

    /// <summary>深拷。子类持有引用类型字段时 override。</summary>
    public virtual FirePatternBulletExtra Clone() => (FirePatternBulletExtra)MemberwiseClone();
}
