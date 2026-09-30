using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable, SRName("Steer Rate/Fixed")]
public sealed class FixedSteerRateStrategy : SteerRateStrategy
{
    [Tooltip("角速度（度/秒）。正数逆时针，负数顺时针。")]
    public float Value = 90f;
    public override float Sample() => Value;
}
