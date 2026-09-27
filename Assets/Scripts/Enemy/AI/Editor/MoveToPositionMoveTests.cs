using NUnit.Framework;
using UnityEngine;

namespace ShinySTG.EnemyAI.Editor
{
    public sealed class MoveToPositionMoveTests
    {
        GameObject _owner;

        [SetUp]
        public void SetUp() => _owner = new GameObject("Move to position test");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_owner);

        static MoveToPositionMove Create(MoveToPositionMove.SpeedProfile profile) => new MoveToPositionMove
        {
            TargetPosition = new Vector2(10f, 4f),
            Profile = profile,
            AccelerationTime = 0.25f,
            BrakingTime = 0.125f
        };

        [TestCase(MoveToPositionMove.SpeedProfile.Constant)]
        [TestCase(MoveToPositionMove.SpeedProfile.Accelerate)]
        [TestCase(MoveToPositionMove.SpeedProfile.Brake)]
        [TestCase(MoveToPositionMove.SpeedProfile.AccelerateAndBrake)]
        public void EveryProfileArrivesAtTargetAcrossFrameSizes(MoveToPositionMove.SpeedProfile profile)
        {
            foreach (int frames in new[] { 1, 16, 256 })
            {
                _owner.transform.position = Vector3.zero;
                var move = Create(profile);
                move.OnEnter(_owner.transform, 2f);
                for (int i = 0; i < frames; i++) move.OnTick(_owner.transform, 2f / frames);
                Assert.That(Vector2.Distance(_owner.transform.position, new Vector3(10f, 4f, 0f)), Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void OversizedTickClampsAndDoesNotDriftAfterArrival()
        {
            var move = Create(MoveToPositionMove.SpeedProfile.AccelerateAndBrake);
            move.OnEnter(_owner.transform, 2f);
            move.OnTick(_owner.transform, 5f);
            Assert.That(Vector2.Distance(_owner.transform.position, new Vector3(10f, 4f, 0f)), Is.LessThan(0.0001f));
            move.OnTick(_owner.transform, 1f);
            Assert.That(Vector2.Distance(_owner.transform.position, new Vector3(10f, 4f, 0f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void ZeroDistanceAndShortDurationAreSafe()
        {
            var move = new MoveToPositionMove { TargetPosition = Vector2.zero, Profile = MoveToPositionMove.SpeedProfile.AccelerateAndBrake };
            move.OnEnter(_owner.transform, 0f);
            move.OnTick(_owner.transform, 1f);
            Assert.That(_owner.transform.position, Is.EqualTo(Vector3.zero));
        }
    }
}
