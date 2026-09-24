using System.Reflection;
using NUnit.Framework;
using ShinySTG.GameActions;
using ShinySTG.Level.Encounter;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    public sealed class BossEncounterTests
    {
        BossEncounterDefinition _definition;
        GameObject _first, _second;
        BossEncounterRuntime _a, _b;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            _definition.Bars = new[] { new BossHealth.HealthBar { Id = "a", MaxHp = 100f } };
            _definition.StartBarId = "a";
            _definition.Signals = new BossSignal[] { new PhaseTimeSignal() };
            _definition.Bars[0].States = new BossPhase[] { new ShooterPhase { DisplayName = "first" } };
            _first = new GameObject("first");
            _first.AddComponent<Boss>();
            _second = new GameObject("second");
            _second.AddComponent<Boss>();
        }

        [TearDown]
        public void TearDown()
        {
            _a?.Dispose();
            _b?.Dispose();
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void EncountersOwnIndependentHealthPhasesSignalsAndNestedActions()
        {
            _definition.Bars[0].States[0].EnterActions = Wait(2f);
            _a = new BossEncounterRuntime(_definition, _first, true);
            _b = new BossEncounterRuntime(_definition, _second, true);
            var a = _first.GetComponent<BossController>();
            var b = _second.GetComponent<BossController>();
            Assert.That(a.Phases[0], Is.Not.SameAs(b.Phases[0]));
            Assert.That(a.Phases[0].EnterActions.Actions[0], Is.Not.SameAs(_definition.Bars[0].States[0].EnterActions.Actions[0]));
            Assert.That(a.Signals[0], Is.Not.SameAs(b.Signals[0]));
            Assert.That(a.Health.Bars[0], Is.Not.SameAs(b.Health.Bars[0]));
            a.Health.TakeDamage(25f);
            a.Signals[0].Tick(a, 4f);
            Assert.That(a.Health.CurrentHp, Is.EqualTo(75f));
            Assert.That(b.Health.CurrentHp, Is.EqualTo(100f));
            Assert.That(_definition.Bars[0].CurrentHp, Is.Zero);
            Assert.That(b.Signals[0].CurrentValue, Is.Zero);
            Assert.That(_definition.Signals[0].CurrentValue, Is.Zero);
        }

        [Test]
        public void StartAndEntryActionsGateFirstPhaseAndDisposeCancelsIt()
        {
            _definition.StartActions = Wait(1f);
            _definition.Bars[0].States[0].EnterActions = Wait(2f);
            _a = new BossEncounterRuntime(_definition, _first, true);
            var controller = _first.GetComponent<BossController>();
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(-1));
            _a.Tick(1f);
            Update(controller);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(-1));
            _a.Tick(2f);
            Update(controller);
            Assert.That(controller.CurrentPhaseIndex, Is.Zero);
            _a.Dispose();
            Assert.That(controller.IsStopped, Is.True);
            Assert.That(_a.IsComplete, Is.True);
            Assert.That(_a.BlocksTimeline, Is.False);
        }

        [Test]
        public void CancellingOpeningDoesNotEnterPhase()
        {
            _definition.StartActions = Wait(10f);
            _a = new BossEncounterRuntime(_definition, _first, true);
            var controller = _first.GetComponent<BossController>();
            controller.StartGate.Cancel();
            Update(controller);
            _a.Tick(0f);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(-1));
            Assert.That(_a.IsComplete, Is.True);
        }

        [Test]
        public void ControllerDoesNotStopBeforeSceneBossStartInitializesIt()
        {
            var controller = _first.GetComponent<BossController>();
            Update(controller);
            Assert.That(controller.IsStopped, Is.False);
            _a = new BossEncounterRuntime(_definition, _first, true);
            Assert.That(controller.CurrentPhaseIndex, Is.Zero);
        }

        [Test]
        public void InvalidOrDuplicateBarIdsAndMissingStagesAreRejected()
        {
            Assert.That(_definition.TryValidate(out _), Is.True);
            _definition.Bars = new[] { _definition.Bars[0], new BossHealth.HealthBar { Id = "a" } };
            Assert.That(_definition.TryValidate(out _), Is.False);
            _definition.Bars[1].Id = "b";
            _definition.Bars[0].States = null;
            Assert.That(_definition.TryValidate(out _), Is.False);
        }

        [Test]
        public void ReorderingSerializedStagesMovesTheirPresentationAndSupportsUndo()
        {
            _definition.Bars[0].States[0].EnterActions = Wait(3f);
            _definition.Bars[0].States = new[] { _definition.Bars[0].States[0], new ShooterPhase { DisplayName = "second", EnterActions = Wait(7f) } };
            Undo.IncrementCurrentGroup();
            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("Bars").GetArrayElementAtIndex(0).FindPropertyRelative("States").MoveArrayElement(0, 1);
            serialized.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            Assert.That(_definition.Bars[0].States[0].DisplayName, Is.EqualTo("second"));
            Assert.That(((WaitGameAction)_definition.Bars[0].States[0].EnterActions.Actions[0]).Seconds, Is.EqualTo(7f));
            Undo.PerformUndo();
            Assert.That(_definition.Bars[0].States[0].DisplayName, Is.EqualTo("first"));
            Assert.That(((WaitGameAction)_definition.Bars[0].States[0].EnterActions.Actions[0]).Seconds, Is.EqualTo(3f));
        }

        [Test]
        public void ExplicitPathTimeAndPercentStatesDoNotLoopOrDamageFutureBars()
        {
            var first = _definition.Bars[0];
            first.NextBarId = "b";
            first.TimeLimit = 20f;
            first.States = new BossPhase[] {
                new ShooterPhase { AdvanceAfterSeconds = 2f },
                new ShooterPhase { AdvanceMode = BossPhase.StateAdvanceMode.HealthPercent, AdvanceAtPercent = 50f },
                new ShooterPhase() };
            var second = new BossHealth.HealthBar { Id = "b", MaxHp = 200f, TimeLimit = 3f,
                States = new BossPhase[] { new ShooterPhase() } };
            var unused = new BossHealth.HealthBar { Id = "unused", States = new BossPhase[] { new ShooterPhase() } };
            _definition.Bars = new[] { second, unused, first };
            _a = new BossEncounterRuntime(_definition, _first, true);
            var controller = _first.GetComponent<BossController>();
            var health = controller.Health;
            Assert.That(health.TotalBarCount, Is.EqualTo(2));
            Assert.That(health.Bars[0].Id, Is.EqualTo("a"));
            controller.Tick(2f);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(1));
            health.TakeDamage(60f);
            controller.Tick(0f);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(2));
            controller.Tick(1f);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(2));
            health.TakeDamage(10000f);
            health.TakeDamage(10000f);
            Assert.That(health.Bars[1].CurrentHp, Is.EqualTo(200f));
            controller.Tick(0f);
            Assert.That(health.CurrentBarIndex, Is.EqualTo(1));
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(3));
            controller.Tick(3f);
            Assert.That(controller.LastBarTimedOut, Is.True);
            Assert.That(health.IsDead, Is.True);
            Assert.That(controller.IsStopped, Is.True);
        }

        [Test]
        public void TimeoutPreemptsStateAndProtectsEntryUntilComplete()
        {
            _definition.Bars[0].TimeLimit = 1f;
            _definition.Bars[0].NextBarId = "b";
            _definition.Bars[0].States = new BossPhase[] { new ShooterPhase { AdvanceAfterSeconds = 1f }, new ShooterPhase() };
            _definition.Bars = new[] { _definition.Bars[0], new BossHealth.HealthBar { Id = "b", MaxHp = 80f,
                States = new BossPhase[] { new ShooterPhase { EnterActions = Wait(2f) } } } };
            _a = new BossEncounterRuntime(_definition, _first, true);
            var controller = _first.GetComponent<BossController>();
            int entries = 0;
            controller.OnPhaseEntered += (_, _) => entries++;
            controller.Tick(1f);
            Assert.That(entries, Is.Zero);
            Assert.That(controller.Health.CurrentBarIndex, Is.EqualTo(1));
            controller.Health.TakeDamage(500f);
            controller.Tick(100f);
            Assert.That(controller.BarElapsedSeconds, Is.Zero);
            Assert.That(controller.Health.CurrentHp, Is.EqualTo(80f));
            _a.Tick(2f);
            controller.Tick(0f);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(2));
            Assert.That(controller.Health.IsInvincible, Is.False);
        }

        [Test]
        public void FinalDepletionFiresDeathOnceAndStopCancelsEntryProtection()
        {
            _a = new BossEncounterRuntime(_definition, _first, true);
            var controller = _first.GetComponent<BossController>();
            int deaths = 0;
            controller.Health.OnDeath += () => deaths++;
            controller.Health.TakeDamage(1000f);
            controller.Health.TakeDamage(1000f);
            controller.Tick(0f);
            controller.Tick(10f);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(controller.LastBarTimedOut, Is.False);
            Assert.That(controller.Health.IsInvincible, Is.False);

            _definition.Bars[0].States[0].EnterActions = Wait(5f);
            _b = new BossEncounterRuntime(_definition, _second, true);
            var waiting = _second.GetComponent<BossController>();
            Assert.That(waiting.Health.IsInvincible, Is.True);
            _b.Dispose();
            Assert.That(waiting.IsStopped, Is.True);
            Assert.That(waiting.Health.IsInvincible, Is.False);
        }

        [Test]
        public void UntimedBarStillAdvancesStatesAndDepletionRestoresNextBarTimer()
        {
            var bar = _definition.Bars[0];
            bar.HasTimeLimit = false;
            bar.TimeLimit = 0f;
            bar.NextBarId = "b";
            bar.States = new BossPhase[] { new ShooterPhase { AdvanceAfterSeconds = 1f }, new ShooterPhase() };
            _definition.Bars = new[] { bar, new BossHealth.HealthBar { Id = "b", TimeLimit = 30f,
                States = new BossPhase[] { new ShooterPhase() } } };
            Assert.That(_definition.TryValidate(out _), Is.True);
            _a = new BossEncounterRuntime(_definition, _first, true);
            var controller = _first.GetComponent<BossController>();
            Assert.That(controller.TryGetBarRemainingSeconds(out _), Is.False);
            controller.Tick(1f);
            Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(1));
            controller.Tick(1000f);
            Assert.That(controller.Health.CurrentBarIndex, Is.Zero);
            Assert.That(controller.Health.IsDead, Is.False);
            controller.Health.TakeDamage(1000f);
            controller.Tick(0f);
            Assert.That(controller.TryGetBarRemainingSeconds(out float remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(30f));
            Assert.That(controller.LastBarTimedOut, Is.False);
        }

        [Test]
        public void MissingStartInvalidTimeAndCyclesAreRejected()
        {
            _definition.StartBarId = "";
            Assert.That(_definition.TryValidate(out _), Is.False);
            _definition.StartBarId = "a";
            _definition.Bars[0].TimeLimit = 0f;
            Assert.That(_definition.TryValidate(out _), Is.False);
            _definition.Bars[0].TimeLimit = 1f;
            _definition.Bars[0].NextBarId = "a";
            Assert.That(_definition.TryValidate(out _), Is.False);
        }

        static ActionSequence Wait(float seconds) => new ActionSequence { Actions = new GameAction[] { new WaitGameAction { Seconds = seconds } } };
        static void Update(BossController controller) => typeof(BossController)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
