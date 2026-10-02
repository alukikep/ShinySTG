using System.Reflection;
using NUnit.Framework;
using ShinySTG.BulletCore;
using ShinySTG.GameplayCommands;
using ShinySTG.Hitbox;
using ShinySTG.Items;
using ShinySTG.Laser;
using ShinySTG.Level;
using ShinySTG.Player;
using UnityEngine;

public sealed class BulletClearDefenseTests
{
    GameObject _root;
    GameObject _source;
    BulletPool _pool;
    Bullet _prefab;
    BulletPool _previousPool;
    static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        _previousPool = BulletPool.Instance;
        _root = new GameObject("Clear defense test pool");
        _pool = _root.AddComponent<BulletPool>();
        SetInstance(typeof(BulletPool), _pool);
        _source = new GameObject("Clear defense test prefab");
        _source.SetActive(false);
        _prefab = _source.AddComponent<Bullet>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_source);
        SetInstance(typeof(BulletPool), _previousPool);
    }

    Bullet Spawn(CollisionTeam team = CollisionTeam.Enemy, bool defended = false, SpawnFogConfig fog = null)
        => _pool.Get(_prefab, Vector2.zero, 0f, 1f, 0f, 1f, team,
            defended ? new BulletModifier[] { new ClearDefenseBulletModifier() } : null, fog);

    static void SetInstance(System.Type type, object value) => type.GetProperty("Instance").SetValue(null, value);

    [TestCase(BulletClearLevel.Normal, false, true)]
    [TestCase(BulletClearLevel.Normal, true, false)]
    [TestCase(BulletClearLevel.Strong, false, true)]
    [TestCase(BulletClearLevel.Strong, true, true)]
    public void ClearLevelMatrix(BulletClearLevel level, bool defended, bool clears)
    {
        var bullet = Spawn(defended: defended);
        Assert.That(bullet.HasClearDefense, Is.EqualTo(defended));
        Assert.That(_pool.ClearAll(null, level), Is.EqualTo(clears ? 1 : 0));
        Assert.That(bullet.gameObject.activeSelf, Is.EqualTo(!clears));
        if (!clears)
        {
            Assert.That(_pool.ClearAll(null, level), Is.Zero);
            Assert.That(bullet.HasClearDefense, Is.True, "普通消弹不得消耗或解除防御。");
        }
    }

    [Test]
    public void SpawnFogIsDefendedBeforeFirstUpdateAndDirectReturnAllowsReuse()
    {
        var bullet = Spawn(defended: true, fog: new DefaultSpawnFog { Duration = 1f });
        Assert.That(bullet.IsFogged, Is.True);
        Assert.That(_pool.ClearAll(null), Is.Zero);
        _pool.Return(bullet);
        _pool.Return(bullet);
        Assert.That(bullet.HasClearDefense, Is.False);
        var reused = Spawn();
        Assert.That(reused, Is.SameAs(bullet));
        Assert.That(reused.HasClearDefense, Is.False);
        Assert.That(_pool.ClearAll(null), Is.EqualTo(1));
    }

    [Test]
    public void DefenseSourcesAreIndependentAndDetachIsIdempotent()
    {
        var bullet = Spawn();
        var template = new ClearDefenseBulletModifier();
        var first = template.Clone();
        var second = template.Clone();
        bullet.AddModifier(first);
        bullet.AddModifier(second);
        first.Detach(bullet);
        first.Detach(bullet);
        Assert.That(bullet.HasClearDefense, Is.True);
        var other = _pool.Get(_prefab, Vector2.one, 0f, 1f, 0f, 1f, CollisionTeam.Enemy,
            new BulletModifier[] { template });
        bullet.ClearModifiers();
        Assert.That(bullet.HasClearDefense, Is.False);
        Assert.That(other.HasClearDefense, Is.True, "克隆和模板不得跨子弹共享防御状态。");
    }

    [Test]
    public void DefenseIgnoresOwnTimingAndUnusedTriggerIsDetached()
    {
        const string signal = "Clear defense test signal";
        int subscribers = BulletSignalBus.DebugSubscriberCount(signal);
        var bullet = _pool.Get(_prefab, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Enemy,
            new BulletModifier[] { new ClearDefenseBulletModifier {
                StartTrigger = new OnSignalStartTrigger { SignalName = signal }, Duration = .01f, OneShot = true } });
        Assert.That(bullet.HasClearDefense, Is.True);
        Assert.That(BulletSignalBus.DebugSubscriberCount(signal), Is.EqualTo(subscribers));
        _pool.Return(bullet);
        Assert.That(BulletSignalBus.DebugSubscriberCount(signal), Is.EqualTo(subscribers));

        bullet = Spawn();
        var modifier = new ClearDefenseBulletModifier { Duration = .01f, OneShot = true };
        bullet.AddModifier(modifier);
        modifier.Modify(bullet, 1f);
        Assert.That(bullet.HasClearDefense, Is.True);
        bullet.ClearModifiers();
        Assert.That(bullet.HasClearDefense, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForceReturnAllIgnoresDefense(bool presentationOverload)
    {
        Spawn(defended: true);
        Assert.That(presentationOverload ? _pool.ReturnAll(null, new BulletClearPresentation()) : _pool.ReturnAll(),
            Is.EqualTo(1));
        Assert.That(_pool.ActiveBullets, Is.Empty);
    }

    [TestCase(BulletClearLevel.Normal)]
    [TestCase(BulletClearLevel.Strong)]
    public void CommandCombinesTeamAndLevelAndKeepsLaserSwitch(BulletClearLevel level)
    {
        var previousLaserPool = LaserPool.Instance;
        var laserSource = new GameObject("Clear defense laser prefab");
        var data = ScriptableObject.CreateInstance<LaserData>();
        try
        {
            var laserPool = _root.AddComponent<LaserPool>();
            SetInstance(typeof(LaserPool), laserPool);
            laserSource.SetActive(false);
            laserPool.DefaultPrefab = laserSource.AddComponent<LaserEntity>();
            var enemyOwner = _root.AddComponent<HitboxComponent>();
            enemyOwner.Team = CollisionTeam.Enemy;
            var laser = laserPool.Get(data, Vector2.zero, 0f, 2f, enemyOwner);
            var ordinary = Spawn();
            var defended = Spawn(defended: true);
            var player = Spawn(CollisionTeam.Player);
            var neutral = Spawn(CollisionTeam.Neutral, true);
            var command = new ClearProjectilesCommand { ClearLevel = level, IncludeLasers = false };
            command.Execute(new GlobalCommandContext(null));
            Assert.That(ordinary.gameObject.activeSelf, Is.False);
            Assert.That(defended.gameObject.activeSelf, Is.EqualTo(level == BulletClearLevel.Normal));
            Assert.That(player.gameObject.activeSelf, Is.True);
            Assert.That(neutral.gameObject.activeSelf, Is.True);
            Assert.That(laser.gameObject.activeSelf, Is.True);
            command.ClearPlayerProjectiles = command.ClearNeutralProjectiles = true;
            command.ClearLevel = BulletClearLevel.Strong;
            command.IncludeLasers = true;
            command.Execute(new GlobalCommandContext(null));
            Assert.That(_pool.ActiveBullets, Is.Empty);
            Assert.That(laserPool.ActiveLasers, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(_root.GetComponent<LaserPool>());
            Object.DestroyImmediate(laserSource);
            Object.DestroyImmediate(data);
            SetInstance(typeof(LaserPool), previousLaserPool);
        }
    }

    [Test]
    public void MissingHitboxFallsBackToNeutralAndMissingPoolsAreSafe()
    {
        var bullet = Spawn();
        bullet.Hitbox = null;
        Assert.That(_pool.ClearAll(team => team == CollisionTeam.Enemy), Is.Zero);
        Assert.That(_pool.ClearAll(team => team == CollisionTeam.Neutral), Is.EqualTo(1));
        var previousLaserPool = LaserPool.Instance;
        try
        {
            SetInstance(typeof(BulletPool), null);
            SetInstance(typeof(LaserPool), null);
            Assert.DoesNotThrow(() => new ClearProjectilesCommand().Execute(new GlobalCommandContext(null)));
        }
        finally
        {
            SetInstance(typeof(BulletPool), _pool);
            SetInstance(typeof(LaserPool), previousLaserPool);
        }
    }

    [Test]
    public void ConvertToItemsCountsOnlySuccessfullyClearedBullets()
    {
        var previousItems = ItemDropService.Instance;
        var serviceObject = new GameObject("Clear defense item service");
        var definition = ScriptableObject.CreateInstance<ItemDefinition>();
        ItemDropService service = null;
        try
        {
            // EditMode 手动调用 Awake，并保护现有场景单例。
            SetInstance(typeof(ItemDropService), null);
            service = serviceObject.AddComponent<ItemDropService>();
            if (ItemDropService.Instance != service)
                typeof(ItemDropService).GetMethod("Awake", PrivateInstance).Invoke(service, null);
            Spawn();
            Spawn(defended: true);
            var presentation = new BulletClearPresentation {
                Mode = BulletClearPresentationMode.ConvertToItems, Item = definition,
                ItemsPerBullet = 1f, ItemScatterRadius = 0f, ItemSpeed = 0f };
            Assert.That(_pool.ClearAll(null, BulletClearLevel.Normal, presentation), Is.EqualTo(1));
            Assert.That(service.ActiveItems.Count, Is.EqualTo(1));
            Assert.That(_pool.ClearAll(null, BulletClearLevel.Normal, presentation), Is.Zero);
            Assert.That(service.ActiveItems.Count, Is.EqualTo(1));
            Assert.That(_pool.ClearAll(null, BulletClearLevel.Strong, presentation), Is.EqualTo(1));
            Assert.That(service.ActiveItems.Count, Is.EqualTo(2));
        }
        finally
        {
            if (service != null)
            {
                foreach (var item in new System.Collections.Generic.List<ItemPickup>(service.ActiveItems))
                    Object.DestroyImmediate(item.gameObject);
                foreach (string field in new[] { "_fallback", "_texture" })
                {
                    var resource = typeof(ItemDropService).GetField(field, PrivateInstance).GetValue(service) as Object;
                    if (resource != null) Object.DestroyImmediate(resource);
                }
            }
            Object.DestroyImmediate(serviceObject);
            Object.DestroyImmediate(definition);
            SetInstance(typeof(ItemDropService), previousItems);
        }
    }

    [Test]
    public void ContainerChildrenReleaseAndReacquireDefense()
    {
        var bullet = Spawn();
        var sequence = new SequenceBulletModifier { Loop = true, Entries = new[] {
            new BulletModifierEntry { Modifier = new ClearDefenseBulletModifier(), Duration = .5f },
            new BulletModifierEntry { Modifier = new WaitBulletModifier(), Duration = .5f } } };
        bullet.AddModifier(sequence);
        Assert.That(bullet.HasClearDefense, Is.False, "子节点在容器阶段开始时生效。");
        sequence.Modify(bullet, .25f);
        Assert.That(bullet.HasClearDefense, Is.True);
        sequence.Modify(bullet, .5f);
        Assert.That(bullet.HasClearDefense, Is.False);
        sequence.Modify(bullet, .5f);
        Assert.That(bullet.HasClearDefense, Is.True);
        bullet.ClearModifiers();
        Assert.That(bullet.HasClearDefense, Is.False);

        var parallel = new ParallelBulletModifier { Loop = true, CycleDuration = 1f, Entries = new[] {
            new BulletModifierEntry { Modifier = new ClearDefenseBulletModifier(), Duration = .5f },
            new BulletModifierEntry { Modifier = new WaitBulletModifier(), Duration = 1f } } };
        parallel = (ParallelBulletModifier)parallel.Clone();
        bullet.AddModifier(parallel);
        parallel.Modify(bullet, .25f);
        Assert.That(bullet.HasClearDefense, Is.True);
        parallel.Modify(bullet, .5f);
        Assert.That(bullet.HasClearDefense, Is.False);
        parallel.Modify(bullet, .25f);
        Assert.That(bullet.HasClearDefense, Is.True);
        bullet.ClearModifiers();
        Assert.That(bullet.HasClearDefense, Is.False);

    }

    [TestCase(BulletClearLevel.Normal, true)]
    [TestCase(BulletClearLevel.Strong, true)]
    [TestCase(BulletClearLevel.Strong, false)]
    [TestCase(BulletClearLevel.Normal, false)]
    public void BombUsesDefinitionLevelAndLegacyFallback(BulletClearLevel level, bool useDefinition)
    {
        var previousPlayer = Player.Instance;
        var playerObject = new GameObject("Clear defense bomb player");
        var definition = ScriptableObject.CreateInstance<BombDefinition>();
        try
        {
            var player = playerObject.AddComponent<Player>();
            SetInstance(typeof(Player), player);
            var health = playerObject.GetComponent<PlayerHealth>();
            typeof(PlayerHealth).GetMethod("Awake", PrivateInstance).Invoke(health, null);
            var resources = playerObject.AddComponent<PlayerResources>();
            resources.SetBombs(1);
            typeof(Player).GetProperty("Health").SetValue(player, health);
            typeof(Player).GetProperty("Resources").SetValue(player, resources);
            var bomb = playerObject.GetComponent<PlayerBomb>();
            definition.ClearLevel = level;
            definition.IncludeLasers = false;
            if (useDefinition) typeof(PlayerBomb).GetField("_definition", PrivateInstance).SetValue(bomb, definition);
            else typeof(PlayerBomb).GetField("_clearLevel", PrivateInstance).SetValue(bomb, level);
            var defended = Spawn(defended: true);
            Spawn();
            Assert.That(bomb.TryUse(), Is.True);
            Assert.That(defended.gameObject.activeSelf, Is.EqualTo(level == BulletClearLevel.Normal));
            Assert.That(_pool.ActiveBullets.Count, Is.EqualTo(level == BulletClearLevel.Normal ? 1 : 0));
            Assert.That(resources.Bombs, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(definition);
            SetInstance(typeof(Player), previousPlayer);
        }
    }

    [Test]
    public void BattleCleanupClearsDefendedBullets()
    {
        Spawn(defended: true);
        BattleCleanup.Clear(default, null, _pool);
        Assert.That(_pool.ActiveBullets, Is.Empty);
        Assert.That(BattleRestriction.IsActive, Is.False);
    }
}
