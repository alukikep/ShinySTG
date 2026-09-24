using System.Collections.Generic;
using NUnit.Framework;
using ShinySTG.EnemyAI.Boss;
using UnityEngine;

namespace ShinySTG.UI.Editor
{
    public sealed class BossHudTests
    {
        GameObject _go;
        BossHealth _health;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Boss HUD test");
            _health = _go.AddComponent<BossHealth>();
            _health.CurrentBarIndex = 0;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        static BossHealth.HealthBar Bar(float hp, bool trigger = true) =>
            new BossHealth.HealthBar { MaxHp = hp, CurrentHp = hp, TriggerOnEmpty = trigger };

        [TestCase(120f, 99)]
        [TestCase(99f, 99)]
        [TestCase(98.2f, 99)]
        [TestCase(12.3f, 13)]
        [TestCase(.2f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        public void TimerCapsAndRoundsUpWithoutChangingGameTime(float seconds, int expected)
        {
            Assert.That(BossHudView.GetDisplayedSeconds(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void OverflowNotifiesOnceWithFinalStateEvenWhenBarEventsAreDisabled()
        {
            _health.Bars = new[] { Bar(100f, false), Bar(50f, false), Bar(200f) };
            int changes = 0, depleted = 0;
            _health.OnBarDepleted += _ => depleted++;
            _health.OnHealthChanged += () =>
            {
                changes++;
                Assert.That(_health.CurrentBarIndex, Is.EqualTo(2));
                Assert.That(_health.CurrentHpNormalized, Is.EqualTo(.75f));
                Assert.That(_health.RemainingBarCount, Is.EqualTo(1));
            };
            _health.TakeDamage(200f);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(depleted, Is.Zero);
            Assert.That(_health.TotalBarCount, Is.EqualTo(3));
        }

        [Test]
        public void DeathReadsAreSafeAndExistingBarEventStillObservesEmptyOldBar()
        {
            _health.Bars = new[] { Bar(100f) };
            var events = new List<string>();
            _health.OnBarDepleted += index =>
            {
                Assert.That(_health.CurrentBarIndex, Is.EqualTo(index));
                Assert.That(_health.CurrentBarPercent, Is.Zero);
                events.Add("bar");
            };
            _health.OnDeath += () =>
            {
                Assert.That(_health.CurrentHp, Is.Zero);
                Assert.That(_health.MaxHp, Is.Zero);
                Assert.That(_health.CurrentBarPercent, Is.Zero);
                Assert.That(_health.RemainingBarCount, Is.Zero);
                events.Add("death");
            };
            _health.OnHealthChanged += () => events.Add("changed");
            _health.TakeDamage(100f);
            _health.TakeDamage(100f);
            CollectionAssert.AreEqual(new[] { "bar", "death", "changed" }, events);
        }

        [Test]
        public void LegacySingleBarAndInvincibilityUseSameHudContract()
        {
            _health.Bars = null;
            _health.LegacyMaxHp = _health.LegacyCurrentHp = 100f;
            int changes = 0;
            _health.OnHealthChanged += () => changes++;
            _health.AddInvincibility("test");
            _health.TakeDamage(25f);
            Assert.That(changes, Is.Zero);
            _health.RemoveInvincibility("test");
            _health.TakeDamage(25f);
            Assert.That(_health.CurrentBarPercent, Is.EqualTo(75f));
            Assert.That(_health.TotalBarCount, Is.EqualTo(1));
            Assert.That(_health.RemainingBarCount, Is.EqualTo(1));
            _health.TakeDamage(75f);
            Assert.That(_health.RemainingBarCount, Is.Zero);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void ExactDepletionSkipsInvalidBarsWithoutAddingDamage()
        {
            _health.Bars = new[] { Bar(100f), null, Bar(-20f), Bar(50f) };
            _health.TakeDamage(100f);
            Assert.That(_health.CurrentBarIndex, Is.EqualTo(3));
            Assert.That(_health.CurrentHp, Is.EqualTo(50f));
            Assert.That(_health.TotalBarCount, Is.EqualTo(2));
            Assert.That(_health.RemainingBarCount, Is.EqualTo(1));
        }
    }
}
