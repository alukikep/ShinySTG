using System.Reflection;
using NUnit.Framework;
using ShinySTG.EnemyAI.Boss;
using ShinySTG.Level;
using ShinySTG.Player;
using ShinySTG.Items;
using ShinySTG.Laser;
using ShinySTG.Hitbox;
using UnityEngine;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class BattleCleanupTests
    {
        [Test]
        public void NestedRestrictionsPreserveMovementAndReleaseOnlyTheirOwnToken()
        {
            var go = new GameObject("battle restriction test");
            var first = BattleRestriction.Acquire();
            var second = BattleRestriction.Acquire();
            try
            {
                var movement = go.AddComponent<PlayerMovement>();
                var shooting = go.AddComponent<PlayerShooting>();
                var health = go.AddComponent<PlayerHealth>();
                health.SpawnInvincibleDuration = 0f;
                Invoke(health, "Awake");
                movement.MoveInput = Vector2.right;
                movement.FocusHeld = true;
                shooting.FireHeld = true;
                int lives = health.Lives;
                health.TakeHit();
                health.DebugTriggerGraze();
                Assert.AreEqual(lives, health.Lives);
                Assert.Zero(health.GrazeCount);
                Assert.AreEqual(Vector2.right, movement.MoveInput);
                Assert.IsTrue(movement.FocusHeld);
                Assert.IsFalse(shooting.FireHeld);
                first.Dispose();
                first.Dispose();
                Assert.IsTrue(BattleRestriction.IsActive);
                second.Dispose();
                Assert.IsFalse(BattleRestriction.IsActive);
                Assert.IsTrue(shooting.FireHeld);
                health.TakeHit();
                Assert.AreEqual(lives - 1, health.Lives);
            }
            finally { first.Dispose(); second.Dispose(); Object.DestroyImmediate(go); }
        }

        [Test]
        public void EndingDefersProcessDisposalAndRestartReleasesRestriction()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Additive);
            var go = new GameObject("battle ending test");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var level = go.AddComponent<LevelController>();
                level.AutoStart = false;
                level.AutoFindBulletPool = false;
                definition.AutoSwitchBgm = false;
                level.Definition = definition;
                level.BeginLevel();
                var process = new Process();
                level.Runtime.AddTimelineProcess(process);
                level.EndLevel(LevelEndReason.Cleared);
                Assert.IsFalse(BattleRestriction.IsActive);
                Invoke(level, "PublishEnd");
                Assert.IsTrue(BattleRestriction.IsActive);
                Assert.IsTrue(level.IsBattleCleanupPending);
                Assert.Zero(process.Disposals);
                Invoke(level, "Update");
                Assert.AreEqual(1, process.Disposals);
                Assert.IsFalse(level.IsBattleCleanupPending);
                level.ReloadLevel();
                Assert.IsFalse(BattleRestriction.IsActive);
                Assert.AreEqual(1, process.Disposals);
                level.EndLevel(LevelEndReason.Aborted);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(definition);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
            Assert.IsFalse(BattleRestriction.IsActive);
        }

        [Test]
        public void StoppingLivingBossDoesNotPublishDefeat()
        {
            var levelObject = new GameObject("boss stop level");
            var bossObject = new GameObject("boss stop test");
            try
            {
                var level = levelObject.AddComponent<LevelController>();
                var controller = bossObject.AddComponent<BossController>();
                var health = bossObject.GetComponent<BossHealth>();
                health.Initialize(new[] { new BossHealth.HealthBar { Id = "test", MaxHp = 10f } });
                controller.Health = health;
                int defeated = 0;
                level.OnBossDefeated += _ => defeated++;
                controller.Stop();
                controller.Stop();
                Assert.Zero(defeated);
                Assert.IsFalse(health.IsDead);
            }
            finally { Object.DestroyImmediate(bossObject); Object.DestroyImmediate(levelObject); }
        }

        [Test]
        public void ProjectilesAndItemsReturnToPoolsAndCanBeReused()
        {
            var poolObject = new GameObject("cleanup pools");
            var bulletPrefab = new GameObject("cleanup bullet template");
            var laserPrefab = new GameObject("cleanup laser template");
            var data = ScriptableObject.CreateInstance<LaserData>();
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var drops = ScriptableObject.CreateInstance<DropProfile>();
            ItemPickup pickup = null;
            try
            {
                bulletPrefab.SetActive(false);
                laserPrefab.SetActive(false);
                var bullets = poolObject.AddComponent<BulletPool>();
                var lasers = poolObject.AddComponent<LaserPool>();
                var items = poolObject.AddComponent<ItemDropService>();
                if (ItemDropService.Instance != items) Invoke(items, "Awake");
                bullets.DefaultPrefab = bulletPrefab.AddComponent<Bullet>();
                lasers.DefaultPrefab = laserPrefab.AddComponent<LaserEntity>();
                drops.Entries = new[] { new DropProfile.Entry { Item = item } };
                var bullet = bullets.Get(null, Vector2.one, 0f, 1f, 0f, 1f, CollisionTeam.Player);
                var laser = lasers.Get(data, Vector2.one, 0f, 4f, null);
                ItemDropService.Spawn(drops, Vector2.one);
                pickup = items.ActiveItems[0];
                using (BattleRestriction.Acquire())
                {
                    Assert.IsNull(bullets.Get(null, Vector2.zero, 0f, 1f, 0f, 1f, CollisionTeam.Enemy));
                    Assert.IsNull(lasers.Get(data, Vector2.zero, 0f, 4f, null));
                    ItemDropService.Spawn(drops, Vector2.zero);
                    Assert.AreEqual(1, items.ActiveItems.Count);
                    bullets.ReturnAll();
                    lasers.ReturnAll();
                    items.ReturnAll();
                    items.ReturnAll();
                }
                Assert.IsEmpty(bullets.ActiveBullets);
                Assert.IsEmpty(lasers.ActiveLasers);
                Assert.IsEmpty(items.ActiveItems);
                Assert.IsFalse(pickup.gameObject.activeSelf);
                Assert.AreSame(bullet, bullets.Get(null, Vector2.zero, 0f, 2f, 0f, 1f, CollisionTeam.Enemy));
                Assert.AreSame(laser, lasers.Get(data, Vector2.zero, 0f, 2f, null));
                ItemDropService.Spawn(drops, Vector2.zero);
                Assert.AreSame(pickup, items.ActiveItems[0]);
                Assert.IsFalse(pickup.IsCollected);
                bullets.ReturnAll();
                lasers.ReturnAll();
                items.ReturnAll();
            }
            finally
            {
                if (pickup != null) Object.DestroyImmediate(pickup.gameObject);
                var items = poolObject.GetComponent<ItemDropService>();
                foreach (string field in new[] { "_fallback", "_texture" })
                {
                    var resource = typeof(ItemDropService).GetField(field,
                        BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(items) as Object;
                    if (resource != null) Object.DestroyImmediate(resource);
                }
                Object.DestroyImmediate(poolObject);
                Object.DestroyImmediate(bulletPrefab);
                Object.DestroyImmediate(laserPrefab);
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(drops);
            }
        }

        sealed class Process : ILevelTimelineProcess
        {
            public int Disposals;
            public bool IsComplete => false;
            public bool BlocksTimeline => true;
            public void Tick(float dt) { }
            public void Dispose() => Disposals++;
        }

        static void Invoke(object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
