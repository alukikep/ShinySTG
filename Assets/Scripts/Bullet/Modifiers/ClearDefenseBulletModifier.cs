using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>挂载期间抵挡普通消弹；不消耗防御，不受自身 Timing 设置影响。</summary>
[Serializable, SRName("Modifier/Clear Defense")]
public sealed class ClearDefenseBulletModifier : BulletModifier
{
    [Tooltip("开启后，越界、命中和 Modifier 请求都不回收；仅二级消弹或关卡强制清场可回收。")]
    public bool OnlyStrongClear;

    internal override bool UsesTimeWindow => false;
    protected override void OnAttach(Bullet bullet) => bullet.AddClearDefense(this, OnlyStrongClear);

    // Sequence/Parallel 的子节点在阶段开始时挂载，在阶段结束时解除防御。
    protected override void OnWindowExitCleanup(Bullet bullet) => bullet.RemoveClearDefense(this);

    protected override void OnDetach(Bullet bullet) => bullet.RemoveClearDefense(this);

    public override void ModifyCore(Bullet bullet, float deltaTime) { }
}
