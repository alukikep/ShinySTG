using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable]
public sealed class BulletModifierEntry
{
    [Tooltip("子 Modifier 的行为配置。放入容器时，其 Duration、OneShot 不参与调度。")]
    [SerializeReference, SR]
    public BulletModifier Modifier;

    [Min(0.0001f)]
    [Tooltip("该阶段持续时间。容器只读取这里。")]
    public float Duration = 1f;

    public BulletModifierEntry Clone() => new BulletModifierEntry
    {
        Modifier = Modifier?.Clone(),
        Duration = Duration
    };
}
