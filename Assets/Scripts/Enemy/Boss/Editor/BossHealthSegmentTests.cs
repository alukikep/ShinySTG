using System.Reflection;
using NUnit.Framework;
using ShinySTG.Level.Encounter;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    public sealed class BossHealthSegmentTests
    {
        GameObject _go;
        BossHealth _health;
        BossEncounterDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Segment health test");
            _health = _go.AddComponent<BossHealth>();
            _definition = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            _definition.StartBarId = "a";
            _definition.Bars = new[] { new BossHealth.HealthBar { Id = "a", MaxHp = -1f,
                Segments = new[] { Segment("nonspell", 40f), Segment("spell", 60f, 0.5f) } } };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_definition);
        }

        static BossHealthSegment Segment(string id, float hp, float multiplier = 1f) =>
            new BossHealthSegment { Id = id, MaxHp = hp, DamageTakenMultiplier = multiplier,
                States = new BossPhase[] { new ShooterPhase() } };

        static object Invoke(BossHealth health, string method) => typeof(BossHealth)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);

        [Test]
        public void DamageStopsAtBoundaryAndUsesNextSegmentMultiplier()
        {
            _health.Initialize(_definition.Bars, true);
            int segments = 0, bars = 0;
            _health.OnSegmentDepleted += (_, _) =>
            {
                segments++;
                Assert.That(Invoke(_health, "AdvanceToNextSegment"), Is.False);
                _health.TakeDamage(10000f);
            };
            _health.OnBarDepleted += _ => bars++;
            _health.TakeDamage(10000f);
            _health.TakeDamage(10000f);
            Assert.That(_health.CurrentHp, Is.EqualTo(60f));
            Assert.That(_health.CurrentSegmentHp, Is.Zero);
            Assert.That(_health.GetSegmentHp(0, 1), Is.EqualTo(60f));
            Assert.That(segments, Is.EqualTo(1));
            Assert.That(bars, Is.Zero);
            Assert.That(_health.TryGetBarPercent("a", out float percent), Is.True);
            Assert.That(percent, Is.EqualTo(60f).Within(0.001f));
            Assert.That(_health.TotalHpPercent, Is.EqualTo(60f).Within(0.001f));
            Invoke(_health, "CompleteCurrentBar");
            Assert.That(_health.CurrentBarIndex, Is.Zero);
            Assert.That(Invoke(_health, "AdvanceToNextSegment"), Is.True);
            _health.TakeDamage(20f);
            Assert.That(_health.CurrentSegmentHp, Is.EqualTo(50f));
            Assert.That(_health.MaxHp, Is.EqualTo(100f));
        }

        [Test]
        public void FinalSegmentNotifiesOnceAndDeathWaitsForCompletion()
        {
            _definition.Bars[0].TriggerOnEmpty = false;
            _health.Initialize(_definition.Bars, true);
            int segments = 0, bars = 0, deaths = 0;
            _health.OnSegmentDepleted += (_, _) => segments++;
            _health.OnBarDepleted += _ => bars++;
            _health.OnDeath += () => deaths++;
            _health.TakeDamage(40f);
            Invoke(_health, "AdvanceToNextSegment");
            _health.TakeDamage(float.MaxValue);
            _health.TakeDamage(100f);
            Assert.That(_health.IsCurrentBarEmpty, Is.True);
            Assert.That(_health.IsDead, Is.False);
            Assert.That(segments, Is.EqualTo(2));
            Assert.That(bars, Is.Zero);
            Invoke(_health, "CompleteCurrentBar");
            Invoke(_health, "CompleteCurrentBar");
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(_health.RemainingBarCount, Is.Zero);
            Assert.That(_health.CurrentHp, Is.Zero);
        }

        [Test]
        public void ZeroMultiplierAndInvalidDamageDoNotChangeHealth()
        {
            _definition.Bars[0].Segments[0].DamageTakenMultiplier = 0f;
            _health.Initialize(_definition.Bars, true);
            int notifications = 0;
            _health.OnHealthChanged += () => notifications++;
            foreach (float damage in new[] { float.MaxValue, float.NaN, float.PositiveInfinity, -1f, 0f })
                _health.TakeDamage(damage);
            Assert.That(_health.CurrentHp, Is.EqualTo(100f));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void IndependentInstancesAndReinitializationDoNotMutateSegmentConfig()
        {
            var otherGo = new GameObject("Other segment health");
            try
            {
                var other = otherGo.AddComponent<BossHealth>();
                other.Initialize(_definition.Bars, true);
                _health.Initialize(_definition.Bars, true);
                _health.TakeDamage(40f);
                Invoke(_health, "AdvanceToNextSegment");
                Assert.That(other.CurrentHp, Is.EqualTo(100f));
                Assert.That(_definition.Bars[0].Segments[0].MaxHp, Is.EqualTo(40f));
                _health.AddInvincibility("test");
                _health.Initialize(_definition.Bars, true);
                Assert.That(_health.CurrentHp, Is.EqualTo(100f));
                Assert.That(_health.CurrentSegmentIndex, Is.Zero);
                Assert.That(_health.IsInvincible, Is.False);
            }
            finally { Object.DestroyImmediate(otherGo); }
        }

        [Test]
        public void LegacyAndSegmentConfigurationsAreBothAcceptedAtRuntime()
        {
            Assert.That(_definition.TryValidate(out _), Is.True);
            Assert.That(_definition.TryValidateForRuntime(out _), Is.True);
            var bar = _definition.Bars[0];
            bar.Segments = null;
            bar.MaxHp = 100f;
            bar.States = new BossPhase[] { new ShooterPhase() };
            Assert.That(_definition.TryValidateForRuntime(out _), Is.True);
            _health.Initialize(_definition.Bars, true);
            _health.TakeDamage(25f);
            Assert.That(_health.CurrentHp, Is.EqualTo(75f));
            Assert.That(_health.UsesSegments, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidMultiplierIsRejected(float multiplier)
        {
            _definition.Bars[0].Segments[0].DamageTakenMultiplier = multiplier;
            Assert.That(_definition.TryValidate(out _), Is.False);
            Assert.Throws<System.ArgumentException>(() => _health.Initialize(_definition.Bars, true));
        }

        [Test]
        public void InvalidIdsHpTimeAndThresholdOrderAreRejected()
        {
            var segments = _definition.Bars[0].Segments;
            segments[1].Id = segments[0].Id;
            Assert.That(_definition.TryValidate(out _), Is.False);
            segments[1].Id = "spell";
            segments[0].MaxHp = 0f;
            Assert.That(_definition.TryValidate(out _), Is.False);
            segments[0].MaxHp = 40f;
            segments[0].TimeLimit = float.NaN;
            Assert.That(_definition.TryValidate(out _), Is.False);
            segments[0].HasTimeLimit = false;
            Assert.That(_definition.TryValidate(out _), Is.True);
            segments[0].States = new BossPhase[] {
                new ShooterPhase { AdvanceMode = BossPhase.StateAdvanceMode.HealthPercent, AdvanceAtPercent = 30f },
                new ShooterPhase { AdvanceMode = BossPhase.StateAdvanceMode.HealthPercent, AdvanceAtPercent = 50f },
                new ShooterPhase() };
            Assert.That(_definition.TryValidate(out _), Is.False);
            segments[0].States[1].AdvanceAtPercent = 20f;
            Assert.That(_definition.TryValidate(out _), Is.True);
        }

        [Test]
        public void SerializationClonePreservesSegmentsAndClonesNestedStates()
        {
            var copy = _definition.CreateRuntimeCopy();
            try
            {
                Assert.That(copy.TryValidate(out _), Is.True);
                Assert.That(copy.Bars[0].Segments[1].DamageTakenMultiplier, Is.EqualTo(0.5f));
                Assert.That(copy.Bars[0].Segments[0].States[0], Is.Not.SameAs(_definition.Bars[0].Segments[0].States[0]));
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
