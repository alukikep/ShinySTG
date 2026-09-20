using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    public sealed class PhaseExitConditionTests
    {
        GameObject _owner;
        BossController _controller;
        BossHealth _health;

        [Serializable]
        sealed class TestPhase : BossPhase
        {
            public override void OnEnter(Transform boss) { }
            public override void OnTick(Transform boss, float dt) { }
            public override void OnExit(Transform boss) { }
        }

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Phase condition test");
            _controller = _owner.AddComponent<BossController>();
            _health = _owner.GetComponent<BossHealth>();
            _controller.Health = _health;
            _health.Bars = new[] { Bar("a", 100f), Bar("b", 100f), Bar("c", 200f) };
            _health.CurrentBarIndex = 0;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);

        static BossHealth.HealthBar Bar(string id, float hp) =>
            new BossHealth.HealthBar { Id = id, MaxHp = hp, CurrentHp = hp, TriggerOnEmpty = false };

        static PhaseExitCondition Condition(PhaseExitConditionKind kind, string id = "a", float threshold = 50f) =>
            new PhaseExitCondition { Kind = kind, BarId = id, Threshold = threshold };

        [Test]
        public void DepletionPersistsAfterOverflowWithoutBarEvents()
        {
            int events = 0;
            _health.OnBarDepleted += _ => events++;
            _health.TakeDamage(250f);
            Assert.That(events, Is.Zero);
            Assert.That(Condition(PhaseExitConditionKind.BarDepleted).IsSatisfied(_controller), Is.True);
            Assert.That(Condition(PhaseExitConditionKind.BarDepleted, "b").IsSatisfied(_controller), Is.True);
            Assert.That(Condition(PhaseExitConditionKind.BarDepleted, "c").IsSatisfied(_controller), Is.False);
            Assert.That(Condition(PhaseExitConditionKind.BarPercentAtMost, "a", 0f).IsSatisfied(_controller), Is.True);
            Assert.That(_health.CurrentBarPercent, Is.EqualTo(75f));
        }

        [Test]
        public void StableReferenceSurvivesReorderRenameAndSerialization()
        {
            var first = _health.Bars[0];
            first.Name = "Renamed";
            _health.Bars = new[] { _health.Bars[1], first, _health.Bars[2] };
            var condition = JsonUtility.FromJson<PhaseExitCondition>(
                JsonUtility.ToJson(Condition(PhaseExitConditionKind.BarPercentAtMost)));
            _health.TakeDamage(100f);
            Assert.That(condition.IsSatisfied(_controller), Is.False);
            _health.TakeDamage(50f);
            Assert.That(condition.IsSatisfied(_controller), Is.True);
            Assert.That(_health.Bars[1].Id, Is.EqualTo("a"));
        }

        [Test]
        public void MissingDuplicateInvalidAndDeletedReferencesFailClosed()
        {
            var condition = Condition(PhaseExitConditionKind.BarDepleted);
            _health.Bars[0].CurrentHp = 0f;
            _health.Bars[1].Id = "a";
            Assert.That(condition.IsSatisfied(_controller), Is.False);
            _health.Bars[1].Id = "b";
            _health.Bars[0].MaxHp = 0f;
            Assert.That(condition.IsSatisfied(_controller), Is.False);
            _health.Bars[0] = null;
            Assert.That(condition.IsSatisfied(_controller), Is.False);
            condition.BarId = "";
            Assert.That(condition.IsSatisfied(_controller), Is.False);
        }

        [Test]
        public void AnyAllAndEmptyListsHaveExplicitSemantics()
        {
            _health.TakeDamage(50f);
            var phase = new TestPhase
            {
                ExitMode = PhaseExitMode.Conditions,
                ExitConditions = new[] { Condition(PhaseExitConditionKind.BarPercentAtMost), Condition(PhaseExitConditionKind.BarDepleted) }
            };
            Assert.That(phase.ShouldExit(_controller), Is.True);
            phase.ConditionMatch = PhaseConditionMatch.All;
            Assert.That(phase.ShouldExit(_controller), Is.False);
            _health.TakeDamage(50f);
            Assert.That(phase.ShouldExit(_controller), Is.True);
            phase.ExitConditions[1] = null;
            Assert.That(phase.ShouldExit(_controller), Is.False);
            phase.ExitConditions = Array.Empty<PhaseExitCondition>();
            Assert.That(phase.ShouldExit(_controller), Is.False);
            phase.ExitConditions = null;
            Assert.That(phase.ShouldExit(_controller), Is.False);
        }

        [Test]
        public void LegacyDefaultAndNewModeNeverMixConditions()
        {
            var signal = new CurrentBarPercentSignal();
            signal.OnAttach(_controller);
            _controller.Signals = new BossSignal[] { signal };
            var phase = new TestPhase
            {
                ExitTriggers = new[] { new PhaseTrigger { Threshold = 100f } },
                ExitConditions = new[] { Condition(PhaseExitConditionKind.BarDepleted) }
            };
            Assert.That(phase.ExitMode, Is.EqualTo(PhaseExitMode.LegacyTriggers));
            Assert.That(phase.ShouldExit(_controller), Is.True);
            phase.ExitMode = PhaseExitMode.Conditions;
            Assert.That(phase.ShouldExit(_controller), Is.False);
            var advanced = Condition(PhaseExitConditionKind.Signal);
            advanced.SignalTrigger.Threshold = 100f;
            Assert.That(advanced.IsSatisfied(_controller), Is.True);
            advanced.SignalTrigger.SignalIndex = 99;
            Assert.That(advanced.IsSatisfied(_controller), Is.False);
        }

        [Test]
        public void LegacySerializedPhaseRetainsOldMode()
        {
            var phase = JsonUtility.FromJson<TestPhase>("{\"ExitTriggers\":[]}");
            Assert.That(phase.ExitMode, Is.EqualTo(PhaseExitMode.LegacyTriggers));
            Assert.That(phase.ShouldExit(_controller), Is.False);
        }

        [Test]
        public void TotalPercentSupportsWeightedBarsAndLegacyHealth()
        {
            var condition = Condition(PhaseExitConditionKind.TotalHpPercentAtMost, threshold: 75f);
            Assert.That(condition.IsSatisfied(_controller), Is.False);
            _health.TakeDamage(100f);
            Assert.That(condition.IsSatisfied(_controller), Is.True);
            _health.Bars = null;
            _health.LegacyMaxHp = 100f;
            _health.LegacyCurrentHp = 75f;
            Assert.That(condition.IsSatisfied(_controller), Is.True);
            Assert.That(Condition(PhaseExitConditionKind.BarDepleted).IsSatisfied(_controller), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(101f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidPercentThresholdDoesNotTrigger(float value)
        {
            Assert.That(Condition(PhaseExitConditionKind.BarPercentAtMost, threshold: value).IsSatisfied(_controller), Is.False);
            Assert.That(Condition(PhaseExitConditionKind.TotalHpPercentAtMost, threshold: value).IsSatisfied(_controller), Is.False);
        }

        [Test]
        public void PhaseClockResetsOnEntryAndDoesNotTickDuringTransition()
        {
            _controller.Phases = new BossPhase[] { new TestPhase(), new TestPhase() };
            Invoke("EnterPhase", 0);
            typeof(BossController).GetProperty(nameof(BossController.PhaseElapsedSeconds))
                .GetSetMethod(true).Invoke(_controller, new object[] { 12f });
            Assert.That(Condition(PhaseExitConditionKind.PhaseTimeAtLeast, threshold: 10f).IsSatisfied(_controller), Is.True);
            Invoke("EnterPhase", 1);
            Assert.That(_controller.PhaseElapsedSeconds, Is.Zero);
            Assert.That(Condition(PhaseExitConditionKind.PhaseTimeAtLeast, threshold: 10f).IsSatisfied(_controller), Is.False);
            typeof(BossController).GetField("_pendingPhase", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_controller, 0);
            Invoke("Update");
            Assert.That(_controller.PhaseElapsedSeconds, Is.Zero);
        }

        void Invoke(string method, params object[] args) => typeof(BossController)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, args);
    }
}
