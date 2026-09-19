using NUnit.Framework;
using UnityEngine;

public class FirePatternRuntimeStateTests
{
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
}
