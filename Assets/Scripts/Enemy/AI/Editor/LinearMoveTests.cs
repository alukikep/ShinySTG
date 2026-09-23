using NUnit.Framework;
using UnityEngine;

namespace ShinySTG.EnemyAI.Editor
{
    public sealed class LinearMoveTests
    {
        GameObject _owner;

        [SetUp]
        public void SetUp() => _owner = new GameObject("Linear move test");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_owner);

        static LinearMove CreateMove(LinearMove.SpeedProfile profile) => new LinearMove
        {
            Direction = LinearMove.MoveDirection.Right,
            Speed = 4f,
            Profile = profile,
            AccelerationTime = 0.25f,
            BrakingTime = 0.125f
        };

        [TestCase(LinearMove.SpeedProfile.Constant, 8f)]
        [TestCase(LinearMove.SpeedProfile.Accelerate, 7.5f)]
        [TestCase(LinearMove.SpeedProfile.Brake, 7.75f)]
        [TestCase(LinearMove.SpeedProfile.AccelerateAndBrake, 7.25f)]
        public void ProfilesHaveExpectedTravelAcrossFrameSizes(LinearMove.SpeedProfile profile, float expected)
        {
            foreach (int frames in new[] { 1, 16, 256 })
            {
                _owner.transform.position = Vector3.zero;
                var move = CreateMove(profile);
                move.OnEnter(_owner.transform, 2f);
                for (int i = 0; i < frames; i++) move.OnTick(_owner.transform, 2f / frames);
                Assert.That(_owner.transform.position.x, Is.EqualTo(expected).Within(0.0001f));
            }
        }

        [Test]
        public void StartsSlowCruisesAndBrakesWithoutDriftingAfterEnd()
        {
            var move = CreateMove(LinearMove.SpeedProfile.AccelerateAndBrake);
            move.OnEnter(_owner.transform, 2f);
            move.OnTick(_owner.transform, 0.125f);
            Assert.That(_owner.transform.position.x, Is.InRange(0.001f, 0.25f));
            move.OnTick(_owner.transform, 0.125f);
            float before = _owner.transform.position.x;
            move.OnTick(_owner.transform, 1.625f);
            Assert.That(_owner.transform.position.x - before, Is.EqualTo(6.5f).Within(0.0001f));
            before = _owner.transform.position.x;
            move.OnTick(_owner.transform, 0.125f);
            Assert.That(_owner.transform.position.x - before, Is.EqualTo(0.25f).Within(0.0001f));
            move.OnTick(_owner.transform, 10f);
            Assert.That(_owner.transform.position.x, Is.EqualTo(7.25f).Within(0.0001f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.125f, 0.25f)]
        public void ShortDurationScalesTransitionsAndClampsOversizedTick(float duration, float expected)
        {
            var move = CreateMove(LinearMove.SpeedProfile.AccelerateAndBrake);
            move.OnEnter(_owner.transform, duration);
            move.OnTick(_owner.transform, 5f);
            Assert.That(_owner.transform.position.x, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void ReentryResetsTimeAndUsesNewDurationAndDirection()
        {
            var move = CreateMove(LinearMove.SpeedProfile.AccelerateAndBrake);
            move.OnEnter(_owner.transform, 2f);
            move.OnTick(_owner.transform, 2f);
            move.Direction = LinearMove.MoveDirection.Left;
            move.OnEnter(_owner.transform, 1f);
            move.OnTick(_owner.transform, 5f);
            Assert.That(_owner.transform.position.x, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void NonPositiveTransitionsBecomeInstantAndZeroTickDoesNotMove()
        {
            var move = CreateMove(LinearMove.SpeedProfile.AccelerateAndBrake);
            move.AccelerationTime = -1f;
            move.BrakingTime = 0f;
            move.OnEnter(_owner.transform, 2f);
            move.OnTick(_owner.transform, 0f);
            move.OnTick(_owner.transform, -1f);
            Assert.That(_owner.transform.position.x, Is.Zero);
            move.OnTick(_owner.transform, 3f);
            Assert.That(_owner.transform.position.x, Is.EqualTo(8f));
        }

        [Test]
        public void ParallelDeliversFinalPartialTickAndExitsOnlyOnce()
        {
            var probe = new ProbeAction { DurationConfig = new FixedActionDuration { Value = 1f } };
            var move = new MoveAction
            {
                DurationConfig = new RandomRangeActionDuration { Min = 2f, Max = 2f },
                Move = CreateMove(LinearMove.SpeedProfile.AccelerateAndBrake)
            };
            var parallel = new ParallelAction { Children = new EnemyAction[] { move, probe } };
            parallel.OnEnter(_owner.transform);
            parallel.OnTick(_owner.transform, 0.75f);
            parallel.OnTick(_owner.transform, 0.75f);
            parallel.OnTick(_owner.transform, 0.75f);
            parallel.OnTick(_owner.transform, 1f);
            parallel.OnExit(_owner.transform);
            Assert.That(_owner.transform.position.x, Is.EqualTo(7.25f).Within(0.0001f));
            Assert.That(probe.TickedTime, Is.EqualTo(1f));
            Assert.That(probe.Exits, Is.EqualTo(1));
        }

        [Test]
        public void LegacySerializedMoveDefaultsToConstant()
        {
            var move = JsonUtility.FromJson<LinearMove>("{\"Speed\":4,\"Direction\":3}");
            Assert.That(move.Profile, Is.EqualTo(LinearMove.SpeedProfile.Constant));
            move.OnEnter(_owner.transform, 1f);
            move.OnTick(_owner.transform, 1.25f);
            Assert.That(_owner.transform.position.x, Is.EqualTo(5f));
        }

        sealed class ProbeAction : EnemyAction
        {
            public float TickedTime;
            public int Exits;
            public override void OnTick(Transform enemy, float dt) => TickedTime += dt;
            public override void OnExit(Transform enemy) => Exits++;
        }
    }
}
