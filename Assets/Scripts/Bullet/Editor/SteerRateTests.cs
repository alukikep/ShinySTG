using NUnit.Framework;
using UnityEngine;

public class SteerRateTests
{
    [Test]
    public void LegacyRateMigratesAndCloneOwnsStrategy()
    {
        var modifier = new SteerTowardModifier { TurnRate = -37f };
        modifier.OnAfterDeserialize();
        Assert.AreEqual(-37f, modifier.Rate.Sample());
        var clone = (SteerTowardModifier)modifier.Clone();
        Assert.AreNotSame(modifier.Rate, clone.Rate);
        Assert.AreEqual(-37f, clone.Rate.Sample());
    }

    [Test]
    public void SamplePersistsUntilResetAndCloneResamples()
    {
        var go = new GameObject("Steer sample test");
        try
        {
            var bullet = go.AddComponent<Bullet>();
            var rate = new RandomRangeSteerRateStrategy { Min = -40f, Max = -40f };
            var modifier = new SteerTowardModifier { Rate = rate };
            modifier.Modify(bullet, 0.1f);
            Assert.AreEqual(-40f * Mathf.Deg2Rad, bullet.AngularSpeed);
            rate.Min = rate.Max = 70f;
            modifier.Modify(bullet, 0.1f);
            Assert.AreEqual(-40f * Mathf.Deg2Rad, bullet.AngularSpeed);
            var clone = (SteerTowardModifier)modifier.Clone();
            clone.Modify(bullet, 0.1f);
            Assert.AreEqual(70f * Mathf.Deg2Rad, bullet.AngularSpeed);
            modifier.Detach(bullet);
            modifier.ResetWindow();
            modifier.Modify(bullet, 0.1f);
            Assert.AreEqual(70f * Mathf.Deg2Rad, bullet.AngularSpeed);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
