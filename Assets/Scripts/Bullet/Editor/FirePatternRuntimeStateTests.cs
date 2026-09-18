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
        Assert.That(source.ProcessAngle(Vector2.zero, 0f, 0f), Is.EqualTo(0f).Within(0.0001f));
    }
}
