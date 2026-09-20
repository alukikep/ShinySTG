using NUnit.Framework;
using UnityEngine;
using ShinySTG.Hitbox;

public class BulletFoundationTests
{
    // EditMode 显式设置单例，避免依赖 Player.Awake 及其游戏初始化副作用。
    static void WithAimPlayer(System.Action<Bullet, ShinySTG.Player.Player> test)
    {
        var previous = ShinySTG.Player.Player.Instance;
        var instance = typeof(ShinySTG.Player.Player).GetProperty("Instance");
        var target = new GameObject("Aim player test target");
        var go = new GameObject("Aim player test bullet");
        try
        {
            var player = target.AddComponent<ShinySTG.Player.Player>();
            instance.SetValue(null, player);
            test(go.AddComponent<Bullet>(), player);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(target);
            instance.SetValue(null, previous);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AimSamplesOnceAndOverridesPendingTurn(bool oneShot)
    {
        WithAimPlayer((bullet, player) =>
        {
            bullet.transform.position = new Vector3(2f, 3f, 0f);
            player.transform.position = new Vector3(-1f, 7f, 10f);
            bullet.Speed = 5f;
            bullet.SetModifierTurn(2f, 0.25f);
            var modifier = new AimAtPlayerModifier { OneShot = oneShot };
            modifier.Modify(bullet, 0.25f);
            var apply = typeof(Bullet).GetMethod("ApplyTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            apply.Invoke(bullet, new object[] { 0.25f });
            float expected = Mathf.Atan2(4f, -3f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(expected).Within(0.00001f));
            Assert.That(bullet.Speed, Is.EqualTo(5f));
            Assert.That(bullet.AngularSpeed, Is.Zero);
            player.transform.position = Vector3.down;
            modifier.Modify(bullet, 1f);
            apply.Invoke(bullet, new object[] { 1f });
            Assert.That(bullet.SteerAngle, Is.EqualTo(expected).Within(0.00001f));
        });
    }

    [Test]
    public void AimDelayAndCloneSampleAtActivationAfterReuse()
    {
        WithAimPlayer((bullet, player) =>
        {
            var modifier = new AimAtPlayerModifier { Delay = 1f };
            player.transform.position = Vector3.left;
            modifier.Modify(bullet, 0.5f);
            Assert.That(bullet.SteerAngle, Is.Zero);
            player.transform.position = Vector3.up;
            modifier.Modify(bullet, 0.5f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(Mathf.PI / 2f).Within(0.00001f));
            bullet.ResetForPool();
            var clone = modifier.Clone();
            player.transform.position = Vector3.down;
            clone.Modify(bullet, 0.5f);
            Assert.That(bullet.SteerAngle, Is.Zero);
            clone.Modify(bullet, 0.5f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(-Mathf.PI / 2f).Within(0.00001f));
        });
    }

    [Test]
    public void AimSignalSamplesOnUpdateAndUnsubscribes()
    {
        WithAimPlayer((bullet, player) =>
        {
            const string signal = "aim-player-foundation-test";
            var modifier = new AimAtPlayerModifier
            {
                StartTrigger = new OnSignalStartTrigger { SignalName = signal, MaxWait = 0f }
            };
            bullet.AddModifier(modifier);
            bullet.AttachSignalTriggers();
            player.transform.position = Vector3.left;
            modifier.Modify(bullet, 1f);
            Assert.That(bullet.SteerAngle, Is.Zero);
            ShinySTG.BulletCore.BulletSignalBus.Emit(signal, Vector2.zero);
            player.transform.position = Vector3.up;
            modifier.Modify(bullet, 0.1f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(Mathf.PI / 2f).Within(0.00001f));
            Assert.That(ShinySTG.BulletCore.BulletSignalBus.DebugSubscriberCount(signal), Is.Zero);
            player.transform.position = Vector3.left;
            ShinySTG.BulletCore.BulletSignalBus.Emit(signal, Vector2.zero);
            modifier.Modify(bullet, 0.1f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(Mathf.PI / 2f).Within(0.00001f));
        });
    }

    [TestCase("missing")]
    [TestCase("inactive")]
    [TestCase("coincident")]
    public void AimWithoutValidDirectionPreservesMotionAndConsumesTrigger(string state)
    {
        WithAimPlayer((bullet, player) =>
        {
            var instance = typeof(ShinySTG.Player.Player).GetProperty("Instance");
            if (state == "missing") instance.SetValue(null, null);
            if (state == "inactive") player.gameObject.SetActive(false);
            if (state == "coincident") player.transform.position = bullet.transform.position;
            bullet.SteerAngle = 0.7f;
            bullet.Speed = 3f;
            bullet.SetModifierTurn(2f, 0.1f);
            var modifier = new AimAtPlayerModifier();
            modifier.Modify(bullet, 0.1f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(0.7f));
            Assert.That(bullet.AngularSpeed, Is.EqualTo(2f));
            Assert.That(bullet.Speed, Is.EqualTo(3f));
            instance.SetValue(null, player);
            player.gameObject.SetActive(true);
            player.transform.position = Vector3.up;
            modifier.Modify(bullet, 0.1f);
            Assert.That(bullet.SteerAngle, Is.EqualTo(0.7f));
        });
    }

    sealed class LifecycleProbe : BulletModifier
    {
        public int Enters, Exits, Detaches, Ticks;
        protected override void OnWindowEnter(Bullet b) => Enters++;
        protected override void OnWindowExitCleanup(Bullet b) => Exits++;
        protected override void OnDetach(Bullet b) => Detaches++;
        public override void ModifyCore(Bullet b, float dt) => Ticks++;
    }

    [Test]
    public void HomingRejectsOutOfRadiusAndInactiveTargets()
    {
        var root = new GameObject("Homing service");
        var go = new GameObject("Homing bullet");
        var enemy = new GameObject("Homing target");
        var service = root.AddComponent<CollisionService>();
        var b = go.AddComponent<Bullet>();
        var health = enemy.AddComponent<ShinySTG.EnemyAI.EnemyHealth>();
        var hitbox = enemy.AddComponent<HitboxComponent>();
        hitbox.Team = CollisionTeam.Enemy;
        health.Hitbox = hitbox;
        var modifier = new HomingEnemyModifier { SearchRadius = 1f };
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var grid = new UniformGrid(4f, Vector2.one * -20f, Vector2.one * 20f);
        typeof(CollisionService).GetField("_grid", flags).SetValue(service, grid);
        // EditMode 不依赖组件 Awake 的调用时机。
        typeof(ShinySTG.EnemyAI.EnemyHealth).GetField("_currentHp", flags).SetValue(health, 1f);
        var search = typeof(HomingEnemyModifier).GetMethod("FindNearestTarget", flags);
        try
        {
            enemy.transform.position = Vector3.up * 2f;
            hitbox.RefreshCachedBounds();
            grid.Insert(hitbox);
            Assert.That(search.Invoke(modifier, new object[] { b, service }), Is.Null);
            enemy.transform.position = Vector3.up;
            hitbox.RefreshCachedBounds();
            grid.Clear();
            grid.Insert(hitbox);
            Assert.That(search.Invoke(modifier, new object[] { b, service }), Is.SameAs(health));
            enemy.SetActive(false);
            Assert.That(search.Invoke(modifier, new object[] { b, service }), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ClearingActiveModifierExitsAndUnsubscribesOnce()
    {
        var go = new GameObject("Detach test");
        var b = go.AddComponent<Bullet>();
        var active = new LifecycleProbe();
        var waiting = new LifecycleProbe { StartTrigger = new OnSignalStartTrigger { SignalName = "bullet-detach-test" } };
        try
        {
            b.AddModifier(active);
            b.AddModifier(waiting);
            b.AttachSignalTriggers();
            active.Modify(b, 0.1f);
            b.ResetForPool();
            b.ResetForPool();
            Assert.That(active.Exits, Is.EqualTo(1));
            Assert.That(active.Detaches, Is.EqualTo(1));
            Assert.That(waiting.Exits, Is.Zero);
            Assert.That(waiting.Detaches, Is.EqualTo(1));
            Assert.That(ShinySTG.BulletCore.BulletSignalBus.DebugSubscriberCount("bullet-detach-test"), Is.Zero);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void HomingFiltersExactRadius()
    {
        var root = new GameObject("Homing service");
        var go = new GameObject("Homing bullet");
        var enemy = new GameObject("Homing target");
        var service = root.AddComponent<CollisionService>();
        var b = go.AddComponent<Bullet>();
        var health = enemy.AddComponent<ShinySTG.EnemyAI.EnemyHealth>();
        var hitbox = enemy.AddComponent<HitboxComponent>();
        hitbox.Team = CollisionTeam.Enemy;
        health.Hitbox = hitbox;
        var modifier = new HomingEnemyModifier { SearchRadius = 1f };
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var grid = new UniformGrid(4f, Vector2.one * -20f, Vector2.one * 20f);
        typeof(CollisionService).GetField("_grid", flags).SetValue(service, grid);
        typeof(ShinySTG.EnemyAI.EnemyHealth).GetField("_currentHp", flags).SetValue(health, 1f);
        var search = typeof(HomingEnemyModifier).GetMethod("FindNearestTarget", flags);
        try
        {
            enemy.transform.position = Vector3.up * 2f;
            hitbox.RefreshCachedBounds();
            grid.Insert(hitbox);
            Assert.That(search.Invoke(modifier, new object[] { b, service }), Is.Null);
            enemy.transform.position = Vector3.up;
            hitbox.RefreshCachedBounds();
            grid.Clear();
            grid.Insert(hitbox);
            Assert.That(search.Invoke(modifier, new object[] { b, service }), Is.SameAs(health));
            enemy.SetActive(false);
            Assert.That(search.Invoke(modifier, new object[] { b, service }), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ClearingActiveModifierExitsOnce()
    {
        var go = new GameObject("Detach test");
        var b = go.AddComponent<Bullet>();
        var active = new LifecycleProbe();
        try
        {
            b.AddModifier(active);
            active.Modify(b, 0.1f);
            b.ResetForPool();
            b.ResetForPool();
            Assert.That(active.Exits, Is.EqualTo(1));
            Assert.That(active.Detaches, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void WindowClipsAccelerationAndTurn(int fps)
    {
        var go = new GameObject("Window test");
        var b = go.AddComponent<Bullet>();
        var accelerate = new AccelerateModifier { Delay = 0.035f, Duration = 0.217f, Acceleration = 10f };
        var turn = new SteerTowardModifier { Delay = 0.035f, Duration = 0.217f, TurnRate = 90f };
        try
        {
            var apply = typeof(Bullet).GetMethod("ApplyTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < fps; i++)
            {
                accelerate.Modify(b, 1f / fps);
                turn.Modify(b, 1f / fps);
                apply.Invoke(b, new object[] { 1f / fps });
            }
            Assert.That(b.Speed, Is.EqualTo(2.17f).Within(0.0001f));
            Assert.That(b.SteerAngle, Is.EqualTo(90f * Mathf.Deg2Rad * 0.217f).Within(0.0001f));
            Assert.That(b.AngularSpeed, Is.Zero);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void NonTurningExitPreservesAngularSpeedAndDetachIsIdempotent()
    {
        var go = new GameObject("Lifecycle test");
        var b = go.AddComponent<Bullet>();
        var probe = new LifecycleProbe { OneShot = true, AutoSkipOutsideWindow = false };
        try
        {
            b.AngularSpeed = 2f;
            b.AddModifier(probe);
            probe.Modify(b, 0.1f);
            probe.Modify(b, 0.1f);
            b.ClearModifiers();
            b.ClearModifiers();
            Assert.That(b.AngularSpeed, Is.EqualTo(2f));
            Assert.That(probe.Enters, Is.EqualTo(1));
            Assert.That(probe.Exits, Is.EqualTo(1));
            Assert.That(probe.Detaches, Is.EqualTo(1));
            Assert.That(probe.Ticks, Is.Zero);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void SignalLatchesRangeAndUnsubscribesAfterActivation()
    {
        const string signal = "bullet-foundation-range";
        var go = new GameObject("Signal test");
        var b = go.AddComponent<Bullet>();
        var trigger = new OnSignalStartTrigger { SignalName = signal, RequireInRange = true, MaxDistanceFromOrigin = 1f, MaxWait = 1f };
        var modifier = new AccelerateModifier { StartTrigger = trigger };
        try
        {
            b.AddModifier(modifier);
            b.AttachSignalTriggers();
            ShinySTG.BulletCore.BulletSignalBus.Emit(signal, Vector2.zero);
            go.transform.position = Vector3.right * 10f;
            modifier.Modify(b, 0.1f);
            Assert.That(modifier.IsActive, Is.True);
            Assert.That(ShinySTG.BulletCore.BulletSignalBus.DebugSubscriberCount(signal), Is.Zero);
            var clone = (OnSignalStartTrigger)trigger.Clone();
            Assert.That(clone.ShouldActivate(b, 0.1f), Is.False);
            Assert.That(clone.ShouldActivate(b, 1f), Is.True);
            Assert.That(trigger.SignalReceived, Is.True);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(-1, true)]
    public void BounceHonorsZeroAndForbiddenCorner(int count, bool expected)
    {
        var go = new GameObject("Bounce test");
        var b = go.AddComponent<Bullet>();
        var modifier = new BounceBulletModifier { MaxBounces = count, Walls = BounceBulletModifier.BounceWalls.ExceptBottom };
        var bounds = ShinySTG.Stage.BoundsService.Instance;
        var rect = bounds != null ? bounds.CullingArea : new Rect(-10f, -10f, 20f, 20f);
        try
        {
            modifier.Modify(b, 0.1f);
            go.transform.position = new Vector3(rect.xMax + 1f, rect.yMin - 1f);
            Assert.That(modifier.TryBounceOnOutOfBounds(b), Is.False);
            go.transform.position = new Vector3(rect.xMax + 1f, rect.center.y);
            Assert.That(modifier.TryBounceOnOutOfBounds(b), Is.EqualTo(expected));
            go.transform.position = new Vector3(rect.xMax + 1f, rect.center.y);
            Assert.That(modifier.TryBounceOnOutOfBounds(b), Is.EqualTo(count < 0));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void FadeUsesSeparateOpacityAndCloneRestarts()
    {
        var go = new GameObject("Fade test");
        var renderer = go.AddComponent<SpriteRenderer>();
        var b = go.AddComponent<Bullet>();
        b.Renderer = renderer;
        var color = new BulletColorModifier { Mode = BulletColorModifier.ColorMode.FadeByLifetime, ReferenceLifetime = 1f, FadeStart = 0f };
        try
        {
            color.Modify(b, 1f);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_BulletOpacity"), Is.Zero);
            Assert.That(block.GetColor("_TintColor").a, Is.EqualTo(1f));
            var clone = color.Clone();
            clone.Modify(b, 0.1f);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_BulletOpacity"), Is.EqualTo(0.9f).Within(0.0001f));
            b.ResetForPool();
            renderer.GetPropertyBlock(block);
            Assert.That(block.HasFloat(Shader.PropertyToID("_BulletOpacity")), Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void CadencePreservesRate(int fps)
    {
        float timer = 0f;
        int count = 0;
        for (int i = 0; i < fps * 10; i++) count += FireCadence.Tick(ref timer, 12f, 1f / fps);
        Assert.That(count, Is.InRange(120, 121));
    }

    [Test]
    public void CadenceHandlesPauseDisabledRateAndHitches()
    {
        float timer = 0f;
        Assert.That(FireCadence.Tick(ref timer, 12f, 0f), Is.Zero);
        Assert.That(FireCadence.Tick(ref timer, 0f, 0.1f), Is.Zero);
        Assert.That(FireCadence.Tick(ref timer, -1f, 0.1f), Is.Zero);
        Assert.That(FireCadence.Tick(ref timer, 12f, 10f), Is.EqualTo(8));
        Assert.That(timer, Is.GreaterThan(0f));
        Assert.That(FireCadence.Tick(ref timer, 12f, 0.001f), Is.Zero);
    }

    [Test]
    public void SplitClonesOwnIndependentTriggers()
    {
        var template = new FireOnDurationBulletModifier { StartTrigger = new OnSignalStartTrigger() };
        var first = template.Clone();
        var second = template.Clone();
        Assert.That(first.StartTrigger, Is.Not.SameAs(template.StartTrigger));
        Assert.That(first.StartTrigger, Is.Not.SameAs(second.StartTrigger));
        ((FireOnDurationBulletModifier)first).ModifyCore(null, 0f);
    }

    [Test]
    public void PoolRestoresFogTintAndRejectsDuplicateReturns()
    {
        var root = new GameObject("Bullet foundation test pool");
        var source = new GameObject("Bullet foundation test prefab");
        source.SetActive(false);
        var renderer = source.AddComponent<SpriteRenderer>();
        var material = new Material(Shader.Find("STG/BulletTint"));
        material.SetColor("_TintColor", Color.green);
        renderer.sharedMaterial = material;
        source.transform.localScale = Vector3.one * 0.3f;
        var prefab = source.AddComponent<Bullet>();
        prefab.Renderer = renderer;
        var pool = root.AddComponent<BulletPool>();
        try
        {
            var fog = new DefaultSpawnFog { Duration = 1f, FogStartScale = 2f };
            var first = pool.Get(prefab, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Enemy, null, fog);
            uint version = first.SpawnVersion;
            var tinted = new MaterialPropertyBlock();
            tinted.SetColor("_TintColor", Color.red);
            first.Renderer.SetPropertyBlock(tinted);
            pool.Return(first);
            pool.Return(first);
            var reused = pool.Get(prefab, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Player);
            var next = pool.Get(prefab, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Player);
            Assert.That(reused, Is.SameAs(first));
            Assert.That(next, Is.Not.SameAs(reused));
            Assert.That(reused.SpawnVersion, Is.Not.EqualTo(version));
            Assert.That(reused.transform.localScale, Is.EqualTo(Vector3.one * 0.3f));
            reused.Renderer.GetPropertyBlock(tinted);
            Assert.That(tinted.GetColor("_TintColor"), Is.Not.EqualTo(Color.red));
            Assert.That(reused.Renderer.sharedMaterial.GetColor("_TintColor"), Is.EqualTo(Color.green));
            Assert.That(reused.Hitbox.IsFogged, Is.False);
            Assert.That(pool.ActiveBullets.Count, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    public void PatternCountMatchesSpawnCount(int count)
    {
        var root = new GameObject("Pattern count test pool");
        var source = new GameObject("Pattern count test prefab");
        source.SetActive(false);
        var prefab = source.AddComponent<Bullet>();
        var pool = root.AddComponent<BulletPool>();
        var patterns = new FirePattern[] {
            ScriptableObject.CreateInstance<RingFirePattern>(),
            ScriptableObject.CreateInstance<ArcFirePattern>(),
            ScriptableObject.CreateInstance<LineFirePattern>()
        };
        try
        {
            ((RingFirePattern)patterns[0]).Count = count;
            ((ArcFirePattern)patterns[1]).Count = count;
            ((LineFirePattern)patterns[2]).Count = count;
            foreach (var pattern in patterns)
            {
                pattern.BulletPrefab = prefab;
                pattern.Fire(Vector2.zero, 0f, pool);
                Assert.That(pool.ActiveBullets.Count, Is.EqualTo(pattern.GetFireCount()));
                Assert.That(pattern.GetFireCount(), Is.EqualTo(System.Math.Max(0, count)));
                pool.ReturnAll();
            }
        }
        finally
        {
            foreach (var pattern in patterns) Object.DestroyImmediate(pattern);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void DeferredReturnDoesNotConsumeReusedBullet()
    {
        var root = new GameObject("Deferred return test");
        var source = new GameObject("Deferred return prefab");
        source.SetActive(false);
        var prefab = source.AddComponent<Bullet>();
        var pool = root.AddComponent<BulletPool>();
        var service = root.AddComponent<CollisionService>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            var first = pool.Get(prefab, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Enemy);
            typeof(CollisionService).GetMethod("QueueReturn", flags).Invoke(service, new object[] { first });
            pool.ReturnAll();
            var reused = pool.Get(prefab, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Enemy);
            typeof(CollisionService).GetMethod("FlushReturns", flags).Invoke(service, null);
            Assert.That(reused, Is.SameAs(first));
            Assert.That(pool.ActiveBullets.Count, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(source);
        }
    }
}
