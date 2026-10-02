using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShinySTG.GameplayCommands;
using ShinySTG.Items;
using ShinySTG.Level;
using ShinySTG.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ItemPickupTests
{
    static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject _serviceObject;
    GameObject _playerObject;
    ItemDropService _service;
    ItemDropService _previousService;
    Player _player;
    Player _previousPlayer;
    ItemDefinition _definition;
    DropProfile _profile;
    readonly HashSet<ItemPickup> _items = new();

    [SetUp]
    public void SetUp()
    {
        _previousService = ItemDropService.Instance;
        _previousPlayer = Player.Instance;
        typeof(ItemDropService).GetProperty(nameof(ItemDropService.Instance)).SetValue(null, null);
        typeof(Player).GetProperty(nameof(Player.Instance)).SetValue(null, null);
        _serviceObject = new GameObject("Item pickup test service");
        _service = _serviceObject.AddComponent<ItemDropService>();
        if (ItemDropService.Instance != _service)
            typeof(ItemDropService).GetMethod("Awake", PrivateInstance).Invoke(_service, null);
        _service.EnableAutoCollect = false;
        _service.Gravity = 0f;
        _service.HorizontalDrag = 0f;
        _service.FallSpeed = 100f;
        _service.AttractionSpeed = 10f;

        _playerObject = new GameObject("Item pickup test player");
        _player = _playerObject.AddComponent<Player>();
        SetPlayerProperty(nameof(Player.Health), _playerObject.GetComponent<PlayerHealth>());
        SetPlayerProperty(nameof(Player.Hitbox), _playerObject.GetComponent<PlayerHitbox>());
        SetPlayerProperty(nameof(Player.Resources), _playerObject.GetComponent<PlayerResources>()
            ?? _playerObject.AddComponent<PlayerResources>());
        _player.Health.AddLife(-int.MaxValue);
        _player.Health.AddLife(2);
        _player.Health.SpawnInvincibleDuration = 0f;
        _player.Health.ReviveInvincibleDuration = 0f;
        typeof(PlayerHealth).GetProperty(nameof(PlayerHealth.InvincibleRemaining)).SetValue(_player.Health, 0f);
        _player.Hitbox.AttractionEnabled = false;
        _definition = ScriptableObject.CreateInstance<ItemDefinition>();
        _definition.Kind = ItemKind.Score;
        _profile = ScriptableObject.CreateInstance<DropProfile>();
        _profile.Entries = new[] { new DropProfile.Entry { Item = _definition } };
        _profile.SpawnRadius = 0f;
        _profile.SpeedRange = new Vector2(4f, 4f);
        _profile.SpreadDegrees = 0f;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _items) if (item != null) Object.DestroyImmediate(item.gameObject);
        _items.Clear();
        // 服务资源先在 EditMode 销毁，避免 OnDestroy 使用运行时 Destroy。
        if (_service != null)
            foreach (string field in new[] { "_fallback", "_texture" })
            {
                var resource = typeof(ItemDropService).GetField(field, PrivateInstance).GetValue(_service) as Object;
                if (resource != null) Object.DestroyImmediate(resource);
            }
        Object.DestroyImmediate(_serviceObject);
        Object.DestroyImmediate(_playerObject);
        Object.DestroyImmediate(_definition);
        Object.DestroyImmediate(_profile);
        typeof(ItemDropService).GetProperty(nameof(ItemDropService.Instance)).SetValue(null, _previousService);
        typeof(Player).GetProperty(nameof(Player.Instance)).SetValue(null, _previousPlayer);
    }

    void SetPlayerProperty(string name, object value) => typeof(Player).GetProperty(name).SetValue(_player, value);

    ItemPickup Spawn(bool autoAttract = false, Vector2? velocity = null)
    {
        Assert.That(ItemDropService.SpawnSingle(_definition, new Vector2(5f, 0f),
            velocity ?? Vector2.up * 4f, autoAttract), Is.True);
        var item = _service.ActiveItems[_service.ActiveItems.Count - 1];
        _items.Add(item);
        return item;
    }

    bool Tick(ItemPickup item, Player player, float dt = 0.1f) =>
        (bool)typeof(ItemPickup).GetMethod("Tick", PrivateInstance).Invoke(item, new object[] { dt, _service, player });

    static void AllowCollection(ItemPickup item) =>
        typeof(ItemPickup).GetProperty(nameof(ItemPickup.SpawnFrame)).SetValue(item, Time.frameCount - 1);

    static void AssertPosition(ItemPickup item, Vector2 expected) =>
        Assert.That(Vector2.Distance(item.transform.position, expected), Is.LessThan(0.0001f));

    [TestCase(false)]
    [TestCase(true)]
    public void SpawnOptionControlsRemoteAttractionAndIgnoresScatterVelocity(bool autoAttract)
    {
        var item = Spawn(autoAttract);
        Assert.That(Tick(item, _player), Is.True);
        AssertPosition(item, autoAttract ? new Vector2(4f, 0f) : new Vector2(5f, 0.4f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SpawnDropsCommandUsesProfileAttractionOption(bool autoAttract)
    {
        _profile.AutoAttractOnSpawn = autoAttract;
        _serviceObject.transform.position = new Vector2(5f, 0f);
        new SpawnDropsCommand { Profile = _profile }.Execute(new GlobalCommandContext(_serviceObject.transform));
        Assert.That(_service.ActiveItems.Count, Is.EqualTo(1));
        var item = _service.ActiveItems[0];
        _items.Add(item);
        Assert.That(item.transform.parent, Is.Null);
        Tick(item, _player);
        AssertPosition(item, autoAttract ? new Vector2(4f, 0f) : new Vector2(5f, 0.4f));
    }

    [TestCase("absent")]
    [TestCase("disabled")]
    [TestCase("hitbox")]
    [TestCase("death")]
    [TestCase("restriction")]
    public void AttractionAndCollectionResumeWhenPlayerCanInteractAgain(string unavailable)
    {
        var item = Spawn(true);
        Tick(item, _player);
        IDisposable restriction = null;
        try
        {
            if (unavailable == "disabled") _player.enabled = false;
            if (unavailable == "hitbox") _player.Hitbox.enabled = false;
            if (unavailable == "death") _player.Health.TakeHit();
            if (unavailable == "restriction") restriction = BattleRestriction.Acquire();
            AllowCollection(item);
            var target = unavailable == "absent" ? null : _player;
            Tick(item, target);
            AssertPosition(item, new Vector2(4f, 0f));
            Assert.That(item.TryCollect(target), Is.False);
        }
        finally
        {
            restriction?.Dispose();
            _player.enabled = true;
            _player.Hitbox.enabled = true;
            if (unavailable == "death") Assert.That(_player.Health.CompleteRevive(), Is.True);
        }
        Tick(item, _player);
        AssertPosition(item, new Vector2(3f, 0f));
    }

    [Test]
    public void ReusedPickupDoesNotRetainAutomaticAttractionOrCollectionState()
    {
        var first = Spawn(true);
        Tick(first, _player);
        AllowCollection(first);
        Assert.That(first.TryCollect(_player), Is.True);
        _service.FlushCollected();
        var reused = Spawn();
        Assert.That(reused, Is.SameAs(first));
        Assert.That(reused.IsCollected, Is.False);
        Assert.That(reused.SpawnFrame, Is.EqualTo(Time.frameCount));
        Tick(reused, _player);
        AssertPosition(reused, new Vector2(5f, 0.4f));
    }

    [Test]
    public void OrdinaryAttractionAndCollectionLineStillStartPersistentTracking()
    {
        var item = Spawn(velocity: Vector2.zero);
        _player.Hitbox.AttractionEnabled = true;
        _player.Hitbox.FastAttractionSize = Vector2.one * 12f;
        Tick(item, _player);
        AssertPosition(item, new Vector2(4f, 0f));
        _player.Hitbox.FastAttractionSize = Vector2.zero;
        Tick(item, _player);
        AssertPosition(item, new Vector2(3f, 0f));
        _service.ReturnAll();
        item = Spawn(velocity: Vector2.zero);
        _service.EnableAutoCollect = true;
        _service.AutoCollectLineY = 0f;
        typeof(ItemDropService).GetMethod("UpdateAutoCollectState", PrivateInstance).Invoke(_service, new object[] { _player });
        Tick(item, _player);
        AssertPosition(item, new Vector2(4f, 0f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AutomaticAttractionStillHonorsLifetimeAndDespawnLine(bool expired)
    {
        var item = Spawn(true);
        if (expired) _service.Lifetime = 0.1f;
        else _service.DespawnBelowY = 1f;
        Assert.That(Tick(item, _player), Is.False);
    }

    [TestCase(ItemKind.Score, 100)]
    [TestCase(ItemKind.Score, 0)]
    [TestCase(ItemKind.Score, -1)]
    [TestCase(ItemKind.SmallPower, 100)]
    [TestCase(ItemKind.LargePower, 100)]
    [TestCase(ItemKind.Bomb, 100)]
    [TestCase(ItemKind.OneUp, 100)]
    public void ScoreValueIsAwardedExactlyOnceForEveryItemKind(ItemKind kind, int value)
    {
        _definition.Kind = kind;
        _definition.ScoreValue = value;
        int notifications = 0;
        _player.Resources.OnScoreChanged += _ => notifications++;
        var item = Spawn(true);
        Assert.That(item.TryCollect(_player), Is.False, "出生帧不能结算奖励。");
        AllowCollection(item);
        Assert.That(item.TryCollect(_player), Is.True);
        Assert.That(item.TryCollect(_player), Is.False);
        Assert.That(_player.Resources.Score, Is.EqualTo(Mathf.Max(0, value)));
        Assert.That(notifications, Is.EqualTo(value > 0 ? 1 : 0));
    }

    [Test]
    public void RewardCallbackCanReturnPickupToPoolWithoutBreakingBonusScore()
    {
        _definition.Kind = ItemKind.Bomb;
        _definition.ScoreValue = 25;
        _player.Resources.OnBombsChanged += _ => _service.ReturnAll();
        var item = Spawn();
        AllowCollection(item);
        Assert.That(item.TryCollect(_player), Is.True);
        Assert.That(_service.ActiveItems.Count, Is.Zero);
        Assert.That(_player.Resources.Score, Is.EqualTo(25));
    }

    [Test]
    public void ScoreItemRequiresPlayerResources()
    {
        var item = Spawn();
        AllowCollection(item);
        SetPlayerProperty(nameof(Player.Resources), null);
        Assert.That(item.TryCollect(_player), Is.False);
        Assert.That(item.IsCollected, Is.False);
    }

    [Test]
    public void ProfileAttractionOptionSupportsSerializedUndoAndDirtyState()
    {
        Assert.That(_profile.AutoAttractOnSpawn, Is.False);
        Undo.IncrementCurrentGroup();
        var serialized = new SerializedObject(_profile);
        serialized.FindProperty(nameof(DropProfile.AutoAttractOnSpawn)).boolValue = true;
        Assert.That(serialized.ApplyModifiedProperties(), Is.True);
        Undo.FlushUndoRecordObjects();
        Assert.That(_profile.AutoAttractOnSpawn, Is.True);
        Assert.That(EditorUtility.IsDirty(_profile), Is.True);
        Undo.PerformUndo();
        Assert.That(_profile.AutoAttractOnSpawn, Is.False);
    }

    [Test]
    public void ConversionInspectorIncludesAttractionOptionAndSupportsUndo()
    {
        var encounter = ScriptableObject.CreateInstance<ShinySTG.Level.Encounter.BossEncounterDefinition>();
        try
        {
            var command = new ClearProjectilesCommand();
            encounter.Phases = new ShinySTG.EnemyAI.Boss.BossPhase[] { new ShinySTG.EnemyAI.Boss.ShooterPhase {
                ExitCommands = new GlobalCommand[] { command } } };
            Undo.IncrementCurrentGroup();
            var serialized = new SerializedObject(encounter);
            var presentation = serialized.FindProperty("Phases").GetArrayElementAtIndex(0)
                .FindPropertyRelative("ExitCommands").GetArrayElementAtIndex(0)
                .FindPropertyRelative(nameof(ClearProjectilesCommand.Presentation));
            Assert.That(presentation, Is.Not.Null);
            presentation.FindPropertyRelative(nameof(BulletClearPresentation.Mode)).enumValueIndex =
                (int)BulletClearPresentationMode.ConvertToItems;
            presentation.FindPropertyRelative(nameof(BulletClearPresentation.AutoAttractOnSpawn)).boolValue = true;
            var drawer = new BulletClearPresentationDrawer();
            float expected = 7 * EditorGUIUtility.singleLineHeight + 6 * EditorGUIUtility.standardVerticalSpacing;
            Assert.That(drawer.GetPropertyHeight(presentation, GUIContent.none), Is.EqualTo(expected));
            Assert.That(serialized.ApplyModifiedProperties(), Is.True);
            Undo.FlushUndoRecordObjects();
            Assert.That(command.Presentation.AutoAttractOnSpawn, Is.True);
            Assert.That(EditorUtility.IsDirty(encounter), Is.True);
            Undo.PerformUndo();
            var restored = (ClearProjectilesCommand)encounter.Phases[0].ExitCommands[0];
            Assert.That(restored.Presentation.AutoAttractOnSpawn, Is.False);
        }
        finally { Object.DestroyImmediate(encounter); }
    }
}
