using System;
using SerializeReferenceEditor;
using UnityEngine;

[Serializable, SRName("Modifier/Parallel")]
public sealed class ParallelBulletModifier : BulletModifier
{
    [Tooltip("同时执行的行为；每个阶段持续时间由 Entry.Duration 决定。")]
    public BulletModifierEntry[] Entries;
    [Min(0.0001f)] public float CycleDuration = 1f;
    [Tooltip("达到 CycleDuration 后是否重置所有子行为并重新开始。")]
    public bool Loop = false;
    bool _attached;
    float _cycleElapsed;
    float[] _entryElapsed;
    bool[] _entryEnded;

    protected override void OnResetWindow()
    {
        _attached = false; _cycleElapsed = 0f;
        _entryElapsed = Entries == null ? null : new float[Entries.Length];
        _entryEnded = Entries == null ? null : new bool[Entries.Length];
        if (Entries == null) return;
    }
    protected override void OnWindowEnter(Bullet bullet) => AttachAll(bullet);
    public override void ModifyCore(Bullet bullet, float dt)
    {
        if (Entries == null || !(dt > 0f)) return;
        for (int i = 0; i < Entries.Length; i++)
        {
            var entry = Entries[i];
            if (entry == null || entry.Modifier == null || _entryEnded[i]) continue;
            float remaining = Mathf.Max(0f, entry.Duration - _entryElapsed[i]);
            float slice = Mathf.Min(dt, remaining);
            if (slice > 0f) { entry.Modifier.ModifyAsChild(bullet, slice); _entryElapsed[i] += slice; }
            if (_entryElapsed[i] >= Mathf.Max(0f, entry.Duration)) EndEntry(bullet, i);
        }
        _cycleElapsed += dt;
        if (_cycleElapsed < Mathf.Max(0.0001f, CycleDuration) || !Loop) return;
        _cycleElapsed = 0f;
        ResetChildren(bullet); AttachAll(bullet);
    }
    protected override void OnDetach(Bullet bullet) { DetachAll(bullet); }
    protected override void OnWindowExitCleanup(Bullet bullet) { DetachAll(bullet); }
    void AttachAll(Bullet bullet) { if (_attached || Entries == null) return; for (int i=0;i<Entries.Length;i++) Entries[i]?.Modifier?.BeginAsChild(bullet); _attached=true; }
    void EndEntry(Bullet bullet, int index)
    {
        if (_entryEnded[index]) return;
        _entryEnded[index] = true;
        Entries[index]?.Modifier?.EndAsChild(bullet);
        Entries[index]?.Modifier?.Detach(bullet);
    }
    void DetachAll(Bullet bullet) { if (!_attached || Entries == null) return; for (int i=0;i<Entries.Length;i++) EndEntry(bullet, i); _attached=false; }
    void ResetChildren(Bullet bullet) { DetachAll(bullet); for (int i=0;i<Entries.Length;i++) { Entries[i]?.Modifier?.ResetWindow(); if (_entryElapsed != null) _entryElapsed[i]=0f; if (_entryEnded != null) _entryEnded[i]=false; } }
    public override BulletModifier Clone() { var copy=(ParallelBulletModifier)MemberwiseClone(); copy.StartTrigger=StartTrigger?.Clone(); copy.Entries=Entries==null?null:new BulletModifierEntry[Entries.Length]; if(copy.Entries!=null)for(int i=0;i<copy.Entries.Length;i++)copy.Entries[i]=Entries[i]?.Clone(); copy.ResetWindow(); return copy; }
}
