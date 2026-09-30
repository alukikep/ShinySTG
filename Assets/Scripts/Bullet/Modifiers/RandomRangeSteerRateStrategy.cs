using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable, SRName("Steer Rate/Random Range")]
public sealed class RandomRangeSteerRateStrategy : SteerRateStrategy
{
    [Tooltip("角速度下限（度/秒）。每颗子弹每次窗口首次执行时抽样一次。")]
    public float Min = 45f;
    [Tooltip("角速度上限（度/秒）。窗口内保持抽样值；Min >= Max 时使用 Min。")]
    public float Max = 135f;

    public override float Sample() => Min >= Max ? Min : UnityEngine.Random.Range(Min, Max);
}
