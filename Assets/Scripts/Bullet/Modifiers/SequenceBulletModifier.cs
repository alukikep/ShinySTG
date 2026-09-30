using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable, SRName("Modifier/Sequence")]
public sealed class SequenceBulletModifier : BulletModifier
{
    [Tooltip("按顺序执行的阶段；子 Modifier 的 Timing 字段不参与调度。")]
    public BulletModifierEntry[] Entries;
    [Tooltip("所有阶段执行完后是否从第一个阶段重新开始。")]
    public bool Loop = false;

    int _index;
    float _elapsed;
    bool _attached;

    protected override void OnResetWindow()
    {
        _index = 0; _elapsed = 0f; _attached = false;
    }

    protected override void OnWindowEnter(Bullet bullet) => AttachCurrent(bullet);

    public override void ModifyCore(Bullet bullet, float dt)
    {
        if (Entries == null || Entries.Length == 0 || !(dt > 0f)) return;
        float remaining = dt; int guard = 0;
        while (remaining > 0f && guard++ < Entries.Length * 2 + 4)
        {
            if (_index >= Entries.Length)
            {
                if (!Loop) return;
                ResetChildren(bullet); _index = 0; _elapsed = 0f;
                AttachCurrent(bullet);
            }
            var entry = Entries[_index];
            var child = entry?.Modifier;
            float duration = Mathf.Max(0.0001f, entry != null ? entry.Duration : 0.0001f);
            float slice = Mathf.Min(remaining, Mathf.Max(0.0001f, duration) - _elapsed);
            if (child != null && slice > 0f) child.ModifyAsChild(bullet, slice);
            _elapsed += slice; remaining -= slice;
            if (_elapsed < duration - 0.000001f) break;
            DetachCurrent(bullet); _index++; _elapsed = 0f; AttachCurrent(bullet);
        }
    }

    protected override void OnDetach(Bullet bullet) { DetachCurrent(bullet); }
    protected override void OnWindowExitCleanup(Bullet bullet) { DetachCurrent(bullet); }

    void AttachCurrent(Bullet bullet)
    {
        if (_attached || Entries == null || _index < 0 || _index >= Entries.Length) return;
        var child = Entries[_index]?.Modifier; if (child == null) return;
        child.BeginAsChild(bullet); _attached = true;
    }
    void DetachCurrent(Bullet bullet)
    {
        if (!_attached || Entries == null || _index < 0 || _index >= Entries.Length) return;
        Entries[_index]?.Modifier?.EndAsChild(bullet);
        Entries[_index]?.Modifier?.Detach(bullet); _attached = false;
    }
    void ResetChildren(Bullet bullet)
    {
        DetachCurrent(bullet);
        for (int i = 0; i < Entries.Length; i++) Entries[i]?.Modifier?.ResetWindow();
    }
    public override BulletModifier Clone()
    {
        var copy = (SequenceBulletModifier)MemberwiseClone();
        copy.StartTrigger = StartTrigger?.Clone();
        copy.Entries = Entries == null ? null : new BulletModifierEntry[Entries.Length];
        if (copy.Entries != null) for (int i = 0; i < copy.Entries.Length; i++) copy.Entries[i] = Entries[i]?.Clone();
        copy.ResetWindow(); return copy;
    }
}
