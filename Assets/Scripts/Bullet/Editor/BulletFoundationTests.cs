using NUnit.Framework;
using UnityEngine;
using ShinySTG.Hitbox;

public class BulletFoundationTests
{
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
