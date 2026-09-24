using NUnit.Framework;
using ShinySTG.GameActions;
using ShinySTG.Level.Encounter;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    public sealed class BossSegmentControllerTests
    {
        BossEncounterDefinition _definition;
        GameObject _go;
        BossEncounterRuntime _runtime;
        BossController _controller;
        BossHealth Health => _controller.Health;

        static ActionSequence Wait(float seconds) => new ActionSequence {
            Actions = new GameAction[] { new WaitGameAction { Seconds = seconds } } };

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            _definition.StartBarId = "a";
            _definition.DefeatActions = Wait(100f); // Keep the entity alive during EditMode death assertions.
            _definition.Bars = new[] { new BossHealth.HealthBar { Id = "a", Segments = new[] {
                new BossHealthSegment { Id = "nonspell", MaxHp = 40f, HasTimeLimit = false,
                    States = new BossPhase[] { new ShooterPhase() } },
                new BossHealthSegment { Id = "spell", MaxHp = 60f, TimeLimit = 3f, DamageTakenMultiplier = 0.5f,
                    States = new BossPhase[] { new ShooterPhase() } } } } };
            _go = new GameObject("Segment encounter test");
            _go.AddComponent<Boss>();
            _controller = _go.GetComponent<BossController>();
        }

        void Start() => _runtime = new BossEncounterRuntime(_definition, _go, true);

        [TearDown]
        public void TearDown()
        {
            _runtime?.Dispose();
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void SegmentTransitionWaitsForExitAndEntryAndPausesTimer()
        {
            var segments = _definition.Bars[0].Segments;
            segments[0].States[0].ExitActions = Wait(1f);
            segments[1].States[0].EnterActions = Wait(2f);
            Start();
            Assert.That(_controller.TryGetBarRemainingSeconds(out _), Is.False);
            int exits = 0, entries = 0;
            _controller.OnPhaseExited += (_, _) => exits++;
            _controller.OnPhaseEntered += (_, _) => entries++;
            Health.TakeDamage(1000f);
            Assert.That(Health.IsInvincible, Is.True);
            _controller.Tick(0f);
            Assert.That(Health.CurrentSegmentIndex, Is.EqualTo(1));
            Assert.That(_controller.LastSegmentEndReason, Is.EqualTo(BossController.SegmentEndReason.Defeated));
            _controller.Tick(100f);
            Health.TakeDamage(1000f);
            Assert.That(Health.CurrentHp, Is.EqualTo(60f));
            Assert.That(_controller.SegmentElapsedSeconds, Is.Zero);
            _runtime.Tick(1f);
            _controller.Tick(0f);
            Assert.That(entries, Is.Zero);
            _runtime.Tick(2f);
            _controller.Tick(0f);
            Assert.That(entries, Is.EqualTo(1));
            Assert.That(exits, Is.EqualTo(1));
            Assert.That(Health.IsInvincible, Is.False);
            Assert.That(_controller.TryGetBarRemainingSeconds(out float remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(3f));
            Health.TakeDamage(20f);
            Assert.That(Health.CurrentHp, Is.EqualTo(50f));
        }

        [Test]
        public void StateThresholdUsesSegmentHealthAndDoesNotResetSegmentTimer()
        {
            var segment = _definition.Bars[0].Segments[0];
            segment.States = new BossPhase[] {
                new ShooterPhase { AdvanceMode = BossPhase.StateAdvanceMode.HealthPercent, AdvanceAtPercent = 50f },
                new ShooterPhase() };
            Start();
            _controller.Tick(2f);
            Health.TakeDamage(20f);
            _controller.Tick(0f);
            Assert.That(_controller.CurrentPhaseIndex, Is.EqualTo(1));
            Assert.That(Health.CurrentHp, Is.EqualTo(80f));
            Assert.That(_controller.SegmentElapsedSeconds, Is.EqualTo(2f));
            Assert.That(_controller.PhaseElapsedSeconds, Is.Zero);
        }

        [Test]
        public void TimeoutClearsOnlyCurrentSegmentAndFinalDeathOccursOnce()
        {
            var first = _definition.Bars[0].Segments[0];
            first.HasTimeLimit = true;
            first.TimeLimit = 1f;
            Start();
            int depleted = 0, deaths = 0;
            Health.OnSegmentDepleted += (_, _) => depleted++;
            Health.OnDeath += () => deaths++;
            _controller.Tick(1f);
            Assert.That(_controller.LastSegmentEndReason, Is.EqualTo(BossController.SegmentEndReason.TimedOut));
            Assert.That(_controller.LastEndedSegmentIndex, Is.Zero);
            Assert.That(Health.CurrentHp, Is.EqualTo(60f));
            Assert.That(Health.CurrentBarIndex, Is.Zero);
            Assert.That(depleted, Is.Zero);
            _controller.Tick(3f);
            _controller.Tick(10f);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(_controller.LastBarTimedOut, Is.True);
            Assert.That(_controller.LastEndedSegmentIndex, Is.EqualTo(1));
            Assert.That(_controller.IsStopped, Is.True);
            Assert.That(Health.IsInvincible, Is.False);
        }

        [Test]
        public void FinalSegmentAdvancesToLegacyBarAndRestoresItsTimer()
        {
            var first = _definition.Bars[0];
            first.NextBarId = "b";
            _definition.Bars = new[] { first, new BossHealth.HealthBar { Id = "b", TimeLimit = 20f,
                States = new BossPhase[] { new ShooterPhase() } } };
            Start();
            Health.TakeDamage(40f);
            _controller.Tick(0f);
            Health.TakeDamage(120f);
            _controller.Tick(0f);
            Assert.That(Health.CurrentBarIndex, Is.EqualTo(1));
            Assert.That(_controller.CurrentPhaseIndex, Is.EqualTo(2));
            Assert.That(_controller.TryGetBarRemainingSeconds(out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(20f));
            Assert.That(_controller.LastBarTimedOut, Is.False);
        }

        [Test]
        public void CancelledEntryStopsAndReleasesProtection()
        {
            _definition.Bars[0].Segments[1].States[0].EnterActions = Wait(5f);
            Start();
            Health.TakeDamage(40f);
            _controller.Tick(0f);
            Assert.That(Health.IsInvincible, Is.True);
            var gate = (GameActionHandle)typeof(BossController).GetField("_phaseGate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_controller);
            gate.Cancel();
            _controller.Tick(0f);
            Assert.That(_controller.IsStopped, Is.True);
            Assert.That(Health.IsInvincible, Is.False);
            Assert.That(_controller.LastSegmentEndReason, Is.EqualTo(BossController.SegmentEndReason.Cancelled));
            Assert.That(_controller.LastEndedSegmentIndex, Is.EqualTo(1));
        }

        [Test]
        public void InspectorConversionClonesStatesAndSupportsUndo()
        {
            var bar = _definition.Bars[0];
            bar.Segments = null;
            bar.MaxHp = 321f;
            bar.States = new BossPhase[] { new ShooterPhase { EnterActions = Wait(2f) } };
            Undo.IncrementCurrentGroup();
            var serialized = new SerializedObject(_definition);
            BossEncounterDefinitionEditor.ConvertToSegment(serialized.FindProperty("Bars").GetArrayElementAtIndex(0));
            serialized.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            Assert.That(bar.Segments[0].MaxHp, Is.EqualTo(321f));
            Assert.That(bar.Segments[0].States[0], Is.Not.SameAs(bar.States[0]));
            Assert.That(bar.Segments[0].States[0].EnterActions.Actions[0], Is.Not.SameAs(bar.States[0].EnterActions.Actions[0]));
            Assert.That(EditorUtility.IsDirty(_definition), Is.True);
            Undo.PerformUndo();
            Assert.That(_definition.Bars[0].HasSegments, Is.False);
        }

        [Test]
        public void InspectorAddAndReorderPreserveUniqueIdsAndDefaults()
        {
            var serialized = new SerializedObject(_definition);
            var segments = serialized.FindProperty("Bars").GetArrayElementAtIndex(0).FindPropertyRelative("Segments");
            BossEncounterDefinitionEditor.AddSegment(segments, true);
            string id = segments.GetArrayElementAtIndex(2).FindPropertyRelative("Id").stringValue;
            segments.MoveArrayElement(2, 0);
            serialized.ApplyModifiedProperties();
            var added = _definition.Bars[0].Segments[0];
            Assert.That(added.Id, Is.EqualTo(id));
            Assert.That(added.HasTimeLimit, Is.True);
            Assert.That(added.Kind, Is.EqualTo(BossHealthSegment.SegmentKind.Spell));
            Assert.That(added.DamageTakenMultiplier, Is.EqualTo(1f));
            Assert.That(_definition.TryValidate(out _), Is.True);
            var clone = _definition.CreateRuntimeCopy();
            try { Assert.That(clone.Bars[0].Segments[0].Id, Is.EqualTo(id)); }
            finally { Object.DestroyImmediate(clone); }
        }
    }
}
