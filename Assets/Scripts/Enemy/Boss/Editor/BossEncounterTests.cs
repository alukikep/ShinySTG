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
            _definition.Signals = new BossSignal[] { new PhaseTimeSignal() };
            _definition.Phases = new BossPhase[] { new ShooterPhase { DisplayName = "first" } };
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
            _definition.Phases[0].EnterActions = Wait(2f);
            _a = new BossEncounterRuntime(_definition, _first, true);
            _b = new BossEncounterRuntime(_definition, _second, true);
            var a = _first.GetComponent<BossController>();
            var b = _second.GetComponent<BossController>();
            Assert.That(a.Phases[0], Is.Not.SameAs(b.Phases[0]));
            Assert.That(a.Phases[0].EnterActions.Actions[0], Is.Not.SameAs(_definition.Phases[0].EnterActions.Actions[0]));
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
            _definition.Phases[0].EnterActions = Wait(2f);
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
            _definition.Phases = null;
            Assert.That(_definition.TryValidate(out _), Is.False);
        }

        [Test]
        public void ReorderingSerializedStagesMovesTheirPresentationAndSupportsUndo()
        {
            _definition.Phases[0].EnterActions = Wait(3f);
            _definition.Phases = new[] { _definition.Phases[0], new ShooterPhase { DisplayName = "second", EnterActions = Wait(7f) } };
            Undo.IncrementCurrentGroup();
            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("Phases").MoveArrayElement(0, 1);
            serialized.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            Assert.That(_definition.Phases[0].DisplayName, Is.EqualTo("second"));
            Assert.That(((WaitGameAction)_definition.Phases[0].EnterActions.Actions[0]).Seconds, Is.EqualTo(7f));
            Undo.PerformUndo();
            Assert.That(_definition.Phases[0].DisplayName, Is.EqualTo("first"));
            Assert.That(((WaitGameAction)_definition.Phases[0].EnterActions.Actions[0]).Seconds, Is.EqualTo(3f));
        }

        static ActionSequence Wait(float seconds) => new ActionSequence { Actions = new GameAction[] { new WaitGameAction { Seconds = seconds } } };
        static void Update(BossController controller) => typeof(BossController)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
