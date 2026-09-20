using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ShinySTG.GameActions;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    public sealed class BossTransitionTests
    {
        GameObject _go;
        BossController _controller;
        BossHealth _health;
        TestPhase _first;
        TestPhase _second;

        [Serializable]
        sealed class TestPhase : BossPhase
        {
            public int Entries;
            public int Exits;
            public override void OnEnter(Transform boss) => Entries++;
            public override void OnTick(Transform boss, float dt) { }
            public override void OnExit(Transform boss) => Exits++;
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Boss transition test");
            _controller = _go.AddComponent<BossController>();
            _health = _go.GetComponent<BossHealth>();
            _controller.Health = _health;
            _health.Bars = new[] { Bar("a"), Bar("b"), Bar("c") };
            _health.CurrentBarIndex = 0;
            _first = new TestPhase
            {
                ExitMode = PhaseExitMode.Conditions,
                ExitConditions = new[] { new PhaseExitCondition { Kind = PhaseExitConditionKind.BarDepleted, BarId = "a" } },
                ProtectBarTransition = true,
                ProtectedBarId = "a"
            };
            _second = new TestPhase();
            _controller.Phases = new BossPhase[] { _first, _second };
            // Normalize subscriptions when EditMode does not run Awake/OnEnable.
            Invoke("OnDisable");
            Invoke("OnEnable");
            Invoke("EnterPhase", 0);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_go);

        static BossHealth.HealthBar Bar(string id) => new BossHealth.HealthBar
            { Id = id, MaxHp = 100f, CurrentHp = 100f, TriggerOnEmpty = false };

        [Test]
        public void DefaultOverflowAndOptInDiscardAreIndependentOfLaterHits()
        {
            _first.ProtectBarTransition = false;
            _health.TakeDamage(150f);
            Assert.That(_health.CurrentHp, Is.EqualTo(50f));
            _health.Bars[1].DiscardOverflow = true;
            _health.TakeDamage(100f);
            Assert.That(_health.CurrentHp, Is.EqualTo(100f));
            _health.TakeDamage(10f);
            Assert.That(_health.CurrentHp, Is.EqualTo(90f));
        }

        [TestCase(100f)]
        [TestCase(250f)]
        public void ProtectionStopsOverflowAndSameFrameHitsUntilEntry(float damage)
        {
            _health.TakeDamage(damage);
            _health.TakeDamage(50f);
            Assert.That(_health.CurrentBarIndex, Is.EqualTo(1));
            Assert.That(_health.CurrentHp, Is.EqualTo(100f));
            Assert.That(_health.IsInvincible, Is.True);
            Invoke("Update");
            Assert.That(_first.Exits, Is.EqualTo(1));
            Assert.That(_second.Entries, Is.EqualTo(1));
            Assert.That(_health.IsInvincible, Is.False);
        }

        [Test]
        public void ProtectionSpansExitAndEntryGatesAndCancellationClearsIt()
        {
            var exitGate = PendingGate();
            var entryGate = PendingGate();
            _controller.PhaseActions = (_, entering) => entering ? entryGate : exitGate;
            _health.TakeDamage(100f);
            Invoke("Update");
            Assert.That(_controller.IsTransitioning, Is.True);
            Assert.That(_second.Entries, Is.Zero);
            Assert.That(_health.IsInvincible, Is.True);
            exitGate.Cancel();
            Invoke("Update");
            Assert.That(_controller.IsTransitioning, Is.False);
            Assert.That(_health.IsInvincible, Is.False);
            Assert.That(_second.Entries, Is.Zero);
            Assert.That(_first.Exits, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulGatesKeepProtectionUntilEntryCompletes()
        {
            var exitGate = PendingGate();
            var entryGate = PendingGate();
            _controller.PhaseActions = (_, entering) => entering ? entryGate : exitGate;
            _health.TakeDamage(100f);
            Invoke("Update");
            Complete(exitGate);
            Invoke("Update");
            Assert.That(_health.IsInvincible, Is.True);
            Assert.That(_second.Entries, Is.Zero);
            Complete(entryGate);
            Invoke("Update");
            Assert.That(_second.Entries, Is.EqualTo(1));
            Assert.That(_health.IsInvincible, Is.False);
        }

        [Test]
        public void ReentrantDamageCannotDepleteTheSameBarTwice()
        {
            _first.ProtectBarTransition = false;
            _health.Bars[0].TriggerOnEmpty = true;
            int depleted = 0;
            _health.OnBarDepleted += _ => { depleted++; _health.TakeDamage(100f); };
            _health.TakeDamage(100f);
            Assert.That(depleted, Is.EqualTo(1));
            Assert.That(_health.CurrentBarIndex, Is.EqualTo(1));
            Assert.That(_health.CurrentHp, Is.EqualTo(100f));
        }

        [Test]
        public void StopAndDisableReleaseOnlyOwnedProtection()
        {
            _health.TakeDamage(100f);
            Invoke("OnDisable");
            Assert.That(_health.IsInvincible, Is.False);
            Invoke("OnEnable");
            Assert.That(_health.IsInvincible, Is.True);
            _health.AddInvincibility("other");
            _controller.Stop();
            _controller.Stop();
            Assert.That(_first.Exits, Is.EqualTo(1));
            Assert.That(_health.IsInvincible, Is.True);
            _health.RemoveInvincibility("other");
            Assert.That(_health.IsInvincible, Is.False);
        }

        [Test]
        public void DeathBeforeTransitionDoesNotEnterNextPhase()
        {
            _first.ProtectedBarId = "c";
            _health.TakeDamage(300f);
            Invoke("Update");
            Assert.That(_health.IsDead, Is.True);
            Assert.That(_health.IsInvincible, Is.False);
            Assert.That(_first.Exits, Is.EqualTo(1));
            Assert.That(_second.Entries, Is.Zero);
        }

        [Test]
        public void InvalidProtectionReferenceDoesNotBlockDamage()
        {
            _health.Bars[1].Id = "a";
            _health.TakeDamage(150f);
            Assert.That(_health.IsInvincible, Is.False);
            Assert.That(_health.CurrentHp, Is.EqualTo(50f));
        }

        static GameActionHandle PendingGate() => (GameActionHandle)Activator.CreateInstance(
            typeof(GameActionHandle), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { null, default(GameActionContext) }, null);

        static void Complete(GameActionHandle handle) => typeof(GameActionHandle)
            .GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(handle, null);

        void Invoke(string method, params object[] args) => typeof(BossController)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, args);
    }
}
