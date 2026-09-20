using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 精确值 BaseOffset 策略 —— 直接返回 <see cref="Value"/>(度)。
///
/// 对齐旧版 v1 行为:`BaseOffset = X` ≡ `BaseOffset = FixedBaseOffsetStrategy { Value = X }`。
/// </summary>
[Serializable, SRName("Base Offset/Fixed")]
public class FixedBaseOffsetStrategy : BaseOffsetStrategy
{
    [Tooltip("精确偏移值(度)。\n" +
             "  0  = 分裂与母弹原方向一致(默认);\n" +
             "  90 = 分裂朝母弹左 90°;\n" +
             "  -45 = 分裂朝母弹右 45°。")]
    public float Value = 0f;

    public override float Sample() => Value;
}
