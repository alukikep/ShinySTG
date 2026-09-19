using NUnit.Framework;
using UnityEngine;

public class FirePatternRuntimeStateTests
{
    [Test]
    public void DefaultSpriteMaterialsShareFallbackAndReleaseLastOwner()
    {
        var first = new GameObject("fallback-first");
        var second = new GameObject("fallback-second");
        try
        {
            first.AddComponent<SpriteRenderer>();
            second.AddComponent<SpriteRenderer>();
            var a = first.AddComponent<Bullet>();
            var b = second.AddComponent<Bullet>();
            var material = a.Renderer.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("STG/BulletTint"));
            Assert.That(b.Renderer.sharedMaterial, Is.SameAs(material));
            Object.DestroyImmediate(first);
            Assert.That(material != null, Is.True);
            Object.DestroyImmediate(second);
            Assert.That(material == null, Is.True);
        }
        finally
        {
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void CachedPreparationAdvancesCountsAndResetReclones()
    {
        var source = new FireExtension[] { new BaseAngleFireExtension() };
        var state = new FirePatternRuntimeState();
        state.Advance(source);
        var extensions = state.GetRuntimeExtensions(source);
        var counts = state.GetRuntimeFireCounts(source);
        Assert.That(counts[extensions[0]], Is.EqualTo(1));
        state.Advance(source);
        Assert.That(state.GetRuntimeExtensions(source), Is.SameAs(extensions));
        Assert.That(state.GetRuntimeFireCounts(source), Is.SameAs(counts));
        Assert.That(counts[extensions[0]], Is.EqualTo(2));
        state.Reset();
        Assert.That(state.GetRuntimeExtensions(source)[0], Is.Not.SameAs(extensions[0]));
        Assert.That(state.GetRuntimeFireCounts(source).Count, Is.Zero);
    }

    sealed class SignalReceiver
    {
        public void Receive(Vector2 origin) { }
    }

    [Test]
    public void SignalUnsubscribeUsesDelegateEquality()
    {
        const string signal = "test/delegate-equality";
        var receiver = new SignalReceiver();
        var subscribed = new System.Action<Vector2>(receiver.Receive);
        var removed = new System.Action<Vector2>(receiver.Receive);
        Assert.That(subscribed, Is.Not.SameAs(removed));
        try
        {
            ShinySTG.BulletCore.BulletSignalBus.Subscribe(signal, subscribed);
            ShinySTG.BulletCore.BulletSignalBus.Unsubscribe(signal, removed);
            Assert.That(ShinySTG.BulletCore.BulletSignalBus.DebugSubscriberCount(signal), Is.Zero);
        }
        finally
        {
            ShinySTG.BulletCore.BulletSignalBus.Unsubscribe(signal, subscribed);
        }
    }

    [Test]
    public void IndependentStatesDoNotShareCounts()
    {
        var extension = new BaseAngleFireExtension();
        var extensions = new FireExtension[] { extension };
        var first = new FirePatternRuntimeState();
        var second = new FirePatternRuntimeState();

        first.Advance(extensions);
        first.Advance(extensions);
        second.Advance(extensions);

        Assert.That(first.FireCounts[extension], Is.EqualTo(2));
        Assert.That(second.FireCounts[extension], Is.EqualTo(1));
        first.Reset();
        Assert.That(first.FireCounts.ContainsKey(extension), Is.False);
        Assert.That(second.FireCounts[extension], Is.EqualTo(1));
    }

    [Test]
    public void RuntimeExtensionsAreIndependentClones()
    {
        var source = new AccumulatingOffsetAngleFireExtension
        {
            StepOffset = 15f,
            BaseOffset = new FixedBaseOffsetStrategy { Value = 3f }
        };
        var first = new FirePatternRuntimeState();
        var second = new FirePatternRuntimeState();
        var config = new[] { (FireExtension)source };

        var firstExtension = (AccumulatingOffsetAngleFireExtension)first.GetRuntimeExtensions(config)[0];
        var secondExtension = (AccumulatingOffsetAngleFireExtension)second.GetRuntimeExtensions(config)[0];
        Assert.That(firstExtension, Is.Not.SameAs(source));
        Assert.That(secondExtension, Is.Not.SameAs(firstExtension));
        Assert.That(firstExtension.BaseOffset, Is.Not.SameAs(source.BaseOffset));

        first.Advance(config);
        firstExtension.OnFireGroupTriggered(1);
        second.Advance(config);
        secondExtension.OnFireGroupTriggered(1);
        first.Advance(config);
        firstExtension.OnFireGroupTriggered(2);

        Assert.That(first.FireCounts[source], Is.EqualTo(2));
        Assert.That(second.FireCounts[source], Is.EqualTo(1));
        Assert.That(secondExtension.ProcessAngle(Vector2.zero, 0f, 0f), Is.EqualTo(3f * Mathf.Deg2Rad).Within(0.0001f));
    }

    [Test]
    public void CompositeCountsRepeatedChildrenAndStopsCycles()
    {
        var root = ScriptableObject.CreateInstance<CompositeFirePattern>();
        var nested = ScriptableObject.CreateInstance<CompositeFirePattern>();
        var ring = ScriptableObject.CreateInstance<RingFirePattern>();
        try
        {
            ring.Count = 3;
            root.Children = new FirePattern[] { nested, ring };
            nested.Children = new FirePattern[] { ring, root };
            Assert.That(root.GetFireCount(), Is.EqualTo(6));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(nested);
            Object.DestroyImmediate(ring);
        }
    }

    [Test]
    public void BatchSamplesOnceWhileBulletOffsetsIncrease()
    {
        var offset = new AccumulatingOffsetAngleFireExtension {
            BaseOffset = new FixedBaseOffsetStrategy { Value = 3f },
            StepOffset = 10f, OffsetPerBullet = 5f
        };
        var extensions = new FireExtension[] { offset };
        var counts = new System.Collections.Generic.Dictionary<FireExtension, int> { [offset] = 2 };
        var from = Vector2.zero;
        FireExtensionResolver.PrepareBatch(extensions, ref from, counts);
        Assert.That(FireExtensionResolver.ResolveBulletPipeline(extensions, from, 0, 3, 0f), Is.EqualTo(13f * Mathf.Deg2Rad).Within(0.0001f));
        Assert.That(FireExtensionResolver.ResolveBulletPipeline(extensions, from, 2, 3, 0f), Is.EqualTo(23f * Mathf.Deg2Rad).Within(0.0001f));
    }

    sealed class CountingOffset : BaseOffsetStrategy
    {
        public static int Calls;
        public override float Sample() { Calls++; return Calls; }
    }

    [Test]
    public void RootBatchSamplesAllModulesOnceAcrossRepeatedChildren()
    {
        var root = new GameObject("batch-test");
        var source = new GameObject("bullet-template");
        source.SetActive(false);
        var prefab = source.AddComponent<Bullet>();
        var pool = root.AddComponent<BulletPool>();
        var leaf = ScriptableObject.CreateInstance<RingFirePattern>();
        var composite = ScriptableObject.CreateInstance<CompositeFirePattern>();
        var first = new AngleOffsetFirePatternBulletExtra { BaseOffset = new CountingOffset(), BatchSample = AngleOffsetFirePatternBulletExtra.BatchSampleMode.Synchronized };
        var second = new AngleOffsetFirePatternBulletExtra { BaseOffset = new CountingOffset(), BatchSample = AngleOffsetFirePatternBulletExtra.BatchSampleMode.Synchronized };
        leaf.BulletPrefab = prefab;
        leaf.Count = 3;
        leaf.ModifierPrefabs = new BulletModifier[] { new FireOnEnterBulletModifier { Extra = first } };
        var extras = new BulletModifier[] { new FireOnEnterBulletModifier { Extra = second } };
        composite.Children = new FirePattern[] { leaf, leaf };
        CountingOffset.Calls = 0;
        try
        {
            pool.FireGroup(composite, Vector2.zero, 0f, null, extras, new FirePatternRuntimeState());
            Assert.That(pool.ActiveBullets.Count, Is.EqualTo(6));
            Assert.That(CountingOffset.Calls, Is.EqualTo(2));
            pool.ReturnAll();
            pool.FireGroup(composite, Vector2.zero, 0f, null, extras, new FirePatternRuntimeState());
            Assert.That(CountingOffset.Calls, Is.EqualTo(4));
            var field = typeof(AngleOffsetFirePatternBulletExtra).GetField("_baseOffsetSampled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field.GetValue(first), Is.False);
            Assert.That(field.GetValue(second), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(leaf);
            Object.DestroyImmediate(composite);
        }
    }

    [Test]
    public void IndependentMotherSamplesOncePerClone()
    {
        var template = new AngleOffsetFirePatternBulletExtra { BaseOffset = new CountingOffset() };
        var first = (AngleOffsetFirePatternBulletExtra)template.Clone();
        var second = (AngleOffsetFirePatternBulletExtra)template.Clone();
        CountingOffset.Calls = 0;
        first.OnFireTriggered(null);
        first.OnFireTriggered(null);
        second.OnFireTriggered(null);
        Assert.That(CountingOffset.Calls, Is.EqualTo(2));
        Assert.That(first.GetRotationOffset(), Is.EqualTo(Mathf.Deg2Rad).Within(0.0001f));
        Assert.That(second.GetRotationOffset(), Is.EqualTo(2f * Mathf.Deg2Rad).Within(0.0001f));
        var reused = (AngleOffsetFirePatternBulletExtra)first.Clone();
        reused.OnFireTriggered(null);
        Assert.That(CountingOffset.Calls, Is.EqualTo(3));
    }
}
