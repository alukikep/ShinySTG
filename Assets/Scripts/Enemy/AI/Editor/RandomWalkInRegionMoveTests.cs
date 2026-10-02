using NUnit.Framework;
using UnityEngine;

namespace ShinySTG.EnemyAI.Editor
{
    public sealed class RandomWalkInRegionMoveTests
    {
        GameObject _owner;

        [SetUp]
        public void SetUp() => _owner = new GameObject("Random walk test");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_owner);

        static RandomWalkInRegionMove Create(int seed = 1) => new RandomWalkInRegionMove
        {
            RandomSeedOffset = seed,
            IntervalMin = 1000f,
            IntervalMax = 1000f
        };

        void AssertInside(Vector2 size)
        {
            Vector3 position = _owner.transform.position;
            Assert.That(position.x, Is.InRange(-size.x * 0.5f - 0.00001f, size.x * 0.5f + 0.00001f));
            Assert.That(position.y, Is.InRange(-size.y * 0.5f - 0.00001f, size.y * 0.5f + 0.00001f));
        }

        [TestCase(2.9f, 0f, 0f, 0f)]
        [TestCase(3f, 2f, -45f, 0f)]
        [TestCase(-3f, -2f, 135f, 0f)]
        [TestCase(0f, -2f, 180f, 0f)]
        [TestCase(2.99f, 1.99f, 0f, 360f)]
        public void EdgesAndCornersKeepTheSameSampledStrideAsCenter(float x, float y, float angle, float spread)
        {
            for (int seed = 1; seed <= 64; seed++)
            {
                var move = Create(seed);
                move.DirectionCenterDeg = angle;
                move.DirectionSpreadDeg = spread;
                _owner.transform.position = Vector3.zero;
                move.OnEnter(_owner.transform);
                move.OnTick(_owner.transform, 100f);
                float centerStride = ((Vector2)_owner.transform.position).magnitude;

                Vector2 start = new Vector2(x, y);
                _owner.transform.position = new Vector3(x, y, 7f);
                move.OnExit(_owner.transform);
                move.OnEnter(_owner.transform);
                move.OnTick(_owner.transform, 100f);
                Assert.That(Vector2.Distance(start, _owner.transform.position),
                    Is.EqualTo(centerStride).Within(0.00001f), $"Seed {seed}");
                AssertInside(move.RegionSize);
                Assert.That(_owner.transform.position.z, Is.EqualTo(7f));
            }
        }

        [Test]
        public void NarrowRegionChangesDirectionInsteadOfShorteningStride()
        {
            var move = Create();
            move.RegionSize = new Vector2(0.1f, 4f);
            move.DirectionSpreadDeg = 0f;
            move.DirectionCenterDeg = 0f;
            move.MaxStepDistance = 1f;
            move.ArrivedThreshold = 1f;
            move.OnEnter(_owner.transform);
            move.OnTick(_owner.transform, 100f);
            Assert.That(((Vector2)_owner.transform.position).magnitude, Is.EqualTo(1f).Within(0.00001f));
            AssertInside(move.RegionSize);
        }

        [Test]
        public void ImpossibleStrideClipsToFarthestCorner()
        {
            var move = Create();
            move.RegionSize = new Vector2(0.2f, 0.1f);
            move.MaxStepDistance = 2f;
            move.ArrivedThreshold = 2f;
            move.OnEnter(_owner.transform);
            move.OnTick(_owner.transform, 100f);
            Assert.That(((Vector2)_owner.transform.position).magnitude,
                Is.EqualTo(new Vector2(0.1f, 0.05f).magnitude).Within(0.00001f));
            AssertInside(move.RegionSize);
        }

        [Test]
        public void ZeroSizeRegionUsesSafeMinimumBounds()
        {
            var move = Create();
            move.RegionSize = Vector2.zero;
            move.OnEnter(_owner.transform);
            move.OnTick(_owner.transform, 100f);
            AssertInside(new Vector2(0.001f, 0.001f));
            Assert.That(((Vector2)_owner.transform.position).magnitude, Is.GreaterThan(0f));
        }

        [TestCase(RandomWalkInRegionMove.SpeedCurve.Constant)]
        [TestCase(RandomWalkInRegionMove.SpeedCurve.FastToSlow)]
        [TestCase(RandomWalkInRegionMove.SpeedCurve.SlowToFast)]
        public void CurvesStayInsideAndAgreeAcrossFrameSizes(RandomWalkInRegionMove.SpeedCurve curve)
        {
            Vector3 expected = Vector3.zero;
            foreach (int frames in new[] { 1, 16, 256 })
            {
                var move = Create();
                move.Mode = curve;
                _owner.transform.position = new Vector3(3f, 2f, 0f);
                move.OnEnter(_owner.transform);
                for (int frame = 0; frame < frames; frame++)
                {
                    move.OnTick(_owner.transform, 10f / frames);
                    AssertInside(move.RegionSize);
                }
                if (frames == 1) expected = _owner.transform.position;
                else Assert.That(Vector3.Distance(expected, _owner.transform.position), Is.LessThan(0.00001f));
            }
        }

        [TestCase(RandomWalkInRegionMove.SpeedCurve.Constant, 0.5f)]
        [TestCase(RandomWalkInRegionMove.SpeedCurve.FastToSlow, 0.7071068f)]
        [TestCase(RandomWalkInRegionMove.SpeedCurve.SlowToFast, 0.2928932f)]
        public void HalfDurationHasExpectedIntegratedDistance(RandomWalkInRegionMove.SpeedCurve curve, float fraction)
        {
            var move = Create();
            move.Mode = curve;
            move.DirectionSpreadDeg = 0f;
            float stride = Mathf.Lerp(0.05f, 1.5f, (float)new System.Random(1).NextDouble());
            float averageFactor = curve == RandomWalkInRegionMove.SpeedCurve.Constant ? 1f : 2f / Mathf.PI;
            move.OnEnter(_owner.transform);
            move.OnTick(_owner.transform, stride / (move.PeakSpeed * averageFactor) * 0.5f);
            Assert.That(_owner.transform.position.x, Is.EqualTo(stride * fraction).Within(0.00001f));
        }

        [Test]
        public void RepeatedMovesLeaveCornersWithoutGettingStuck()
        {
            var move = Create();
            move.DirectionSpreadDeg = 0f;
            move.DirectionCenterDeg = -45f;
            _owner.transform.position = new Vector3(3f, 2f, 0f);
            move.OnEnter(_owner.transform);
            for (int step = 0; step < 200; step++)
            {
                Vector2 before = _owner.transform.position;
                move.OnTick(_owner.transform, 100f);
                float stride = Vector2.Distance(before, _owner.transform.position);
                Assert.That(stride, Is.InRange(0.04999f, 1.50001f));
                AssertInside(move.RegionSize);
                move.OnTick(_owner.transform, 1000f);
            }
        }

        [Test]
        public void OutsideStartReturnsSmoothlyThenWalksInside()
        {
            var move = Create();
            _owner.transform.position = new Vector3(4f, 3f, 7f);
            move.OnEnter(_owner.transform);
            move.OnTick(_owner.transform, 0f);
            Assert.That(_owner.transform.position, Is.EqualTo(new Vector3(4f, 3f, 7f)));
            move.OnTick(_owner.transform, 0.1f);
            Assert.That(_owner.transform.position.x, Is.InRange(3f, 4f));
            Assert.That(_owner.transform.position.y, Is.InRange(2f, 3f));
            move.OnTick(_owner.transform, 100f);
            AssertInside(move.RegionSize);
            move.OnTick(_owner.transform, 1000f);
            move.OnTick(_owner.transform, 100f);
            AssertInside(move.RegionSize);
            Assert.That(_owner.transform.position.z, Is.EqualTo(7f));
        }

        [TestCase(0f, 3f)]
        [TestCase(1.5f, 0f)]
        [TestCase(-1f, -1f)]
        public void ZeroStrideOrNonPositiveSpeedDoesNotMove(float maxStep, float speed)
        {
            var move = Create();
            move.MaxStepDistance = maxStep;
            move.PeakSpeed = speed;
            move.OnEnter(_owner.transform);
            move.OnTick(_owner.transform, 100000f);
            Assert.That(_owner.transform.position, Is.EqualTo(Vector3.zero));
            move.OnExit(_owner.transform);
            Assert.DoesNotThrow(() => move.OnTick(_owner.transform, 1f));
            Assert.DoesNotThrow(() => move.OnEnter(null));
            Assert.DoesNotThrow(() => move.OnTick(null, 1f));
        }
    }
}
