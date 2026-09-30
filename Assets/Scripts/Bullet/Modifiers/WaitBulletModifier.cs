using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>显式等待节点。停顿时长由外层 Entry.Duration 管理。</summary>
[Serializable, SRName("Modifier/Wait")]
public sealed class WaitBulletModifier : BulletModifier
{
    public override void ModifyCore(Bullet bullet, float deltaTime) { }
}
