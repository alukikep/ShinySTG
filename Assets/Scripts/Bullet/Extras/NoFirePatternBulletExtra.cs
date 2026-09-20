using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 「显式不传递」占位选项 —— 行为等价于 <see cref="FirePatternBulletExtra"/> 字段为 null。
///
/// 用途:用户已下拉选了类型但希望关掉时不必把字段再设为 null(避免拖回去找不到原选项)。
/// </summary>
[Serializable, SRName("Extra/None")]
public class NoFirePatternBulletExtra : FirePatternBulletExtra
{
}
