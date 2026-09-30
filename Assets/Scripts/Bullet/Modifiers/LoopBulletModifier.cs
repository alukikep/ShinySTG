using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 重复执行一个子 Modifier 的容器。子 Modifier 每轮都会重新 ResetWindow，
/// 因此可把 Orbit 的半径变化、Delay、OneShot 等行为组合成周期运动。
/// </summary>
[Serializable, SRName("Modifier/Loop")]
public sealed class LoopBulletModifier : BulletModifier
{
    [SerializeReference, SR]
    [Tooltip("每轮执行的子 Modifier。通常放 Orbit、Color、Accelerate 等单一行为。")]
    public BulletModifier Child;

    [Min(0.0001f)]
    [Tooltip("每轮持续时间。到期后重置 Child 并从头开始。")]
    public float CycleDuration = 1f;

    [Min(0)]
    [Tooltip("循环次数。0 表示无限循环；正数表示最多执行指定轮数。")]
    public int MaxCycles = 0;

    int _completedCycles;
    float _cycleElapsed;
    bool _childAttached;
    bool _finished;

    protected override void OnResetWindow()
    {
        _completedCycles = 0;
        _cycleElapsed = 0f;
        _childAttached = false;
        _finished = false;
        Child?.ResetWindow();
    }

    protected override void OnWindowEnter(Bullet bullet)
    {
        AttachChild(bullet);
    }

    public override void ModifyCore(Bullet bullet, float deltaTime)
    {
        if (Child == null || _finished || !(deltaTime > 0f)) return;

        float remaining = deltaTime;
        int safety = 0;
        while (remaining > 0f && safety++ < 32)
        {
            float cycleLength = Mathf.Max(0.0001f, CycleDuration);
            float slice = Mathf.Min(remaining, cycleLength - _cycleElapsed);
            if (slice > 0f)
            {
                Child.Modify(bullet, slice);
                if (bullet.ReturnRequested) return;
                _cycleElapsed += slice;
                remaining -= slice;
            }

            if (_cycleElapsed < cycleLength - 0.000001f) continue;
            _cycleElapsed = 0f;
            _completedCycles++;

            if (MaxCycles > 0 && _completedCycles >= MaxCycles)
            {
                Child.Detach(bullet);
                _childAttached = false;
                _finished = true;
                return;
            }

            Child.Detach(bullet);
            Child.ResetWindow();
            AttachChild(bullet);
        }
    }

    protected override void OnDetach(Bullet bullet)
    {
        if (_childAttached)
        {
            Child?.Detach(bullet);
            _childAttached = false;
        }
    }

    protected override void OnWindowExitCleanup(Bullet bullet)
    {
        if (_childAttached)
        {
            Child?.Detach(bullet);
            _childAttached = false;
        }
    }

    void AttachChild(Bullet bullet)
    {
        if (Child == null || _childAttached) return;
        Child.StartTrigger?.OnAttach(bullet);
        _childAttached = true;
    }

    public override BulletModifier Clone()
    {
        var copy = (LoopBulletModifier)MemberwiseClone();
        copy.StartTrigger = StartTrigger?.Clone();
        copy.Child = Child?.Clone();
        copy.ResetWindow();
        return copy;
    }
}
