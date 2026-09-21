using System.Reflection;
using NUnit.Framework;
using ShinySTG.Level;
using ShinySTG.Player;
using ShinySTG.UI;
using UnityEngine;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class PauseContinueTests
    {
        [Test]
        public void ContinuePreservesRuntimeAndDefersPendingClearWithoutCleanup()
        {
            var go = new GameObject("continue test");
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var level = go.AddComponent<LevelController>();
                level.AutoStart = level.AutoFindBulletPool = false;
                definition.AutoSwitchBgm = false;
                level.Definition = definition;
                level.BeginLevel();
                var runtime = level.Runtime;
                runtime.Tick(.5f);
                int attempt = level.AttemptId;
                int ended = 0;
                level.OnLevelEnded += (_, __) => ended++;
                level.EndLevel(LevelEndReason.Cleared);
                Assert.IsTrue(level.TryAwaitContinue(attempt));
                Publish(level);
                Assert.AreEqual(0, ended);
                Assert.IsFalse(level.IsBattleCleanupPending);
                Assert.IsFalse(level.TryEndLevel(LevelEndReason.Cleared, attempt));
                Assert.IsFalse(level.TryResumeContinue(attempt - 1));
                Assert.IsTrue(level.TryResumeContinue(attempt));
                Assert.AreSame(runtime, level.Runtime);
                Assert.AreEqual(.5f, level.Elapsed);
                Assert.AreEqual(attempt, level.AttemptId);
                Publish(level);
                Assert.AreEqual(LevelEndReason.Cleared, level.EndReason);
                Assert.AreEqual(1, ended);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void WaitingContinueCanAbortAndReloadRejectsStaleResume()
        {
            var go = new GameObject("continue abort test");
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var level = go.AddComponent<LevelController>();
                level.AutoStart = level.AutoFindBulletPool = false;
                definition.AutoSwitchBgm = false;
                level.Definition = definition;
                level.BeginLevel();
                int attempt = level.AttemptId;
                Assert.IsTrue(level.TryAwaitContinue(attempt));
                level.EndLevel(LevelEndReason.Aborted);
                Assert.AreEqual(LevelEndReason.Aborted, level.EndReason);
                Assert.IsFalse(level.IsAwaitingContinue);
                level.ReloadLevel();
                Assert.IsFalse(level.TryResumeContinue(attempt));
                Assert.IsTrue(level.IsRunning);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void PauseRestoresTimeAndPreservesOtherOwnersLocks()
        {
            float previous = Time.timeScale;
            try
            {
                Time.timeScale = .5f;
                using var control = PlayerControlLock.Acquire();
                using var battle = BattleRestriction.Acquire();
                var pause = new GameplayPause();
                Assert.AreEqual(0f, Time.timeScale);
                Assert.IsTrue(GameplayPause.IsPaused);
                pause.Dispose();
                pause.Dispose();
                Assert.AreEqual(.5f, Time.timeScale);
                Assert.IsFalse(GameplayPause.IsPaused);
                Assert.IsTrue(PlayerControlLock.IsLocked);
                Assert.IsTrue(BattleRestriction.IsActive);
            }
            finally { Time.timeScale = previous; }
        }

        [Test]
        public void ThreeTotalLivesReviveWithTwoSpareLives()
        {
            var go = new GameObject("continue health test");
            try
            {
                var health = go.AddComponent<PlayerHealth>();
                health.AddLife(1 - health.Lives);
                health.SpawnInvincibleDuration = 0f;
                typeof(PlayerHealth).GetProperty(nameof(PlayerHealth.InvincibleRemaining))
                    .SetValue(health, 0f);
                health.TakeHit();
                Assert.IsTrue(health.IsDead);
                health.AddLife(3);
                Assert.IsTrue(health.CompleteRevive());
                Assert.AreEqual(3, health.Lives);
                Assert.AreEqual(2, health.Lives - 1);
                Assert.IsTrue(health.IsInvincible);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MenuRequiresReleaseAndRepeatsUsingUnscaledClock()
        {
            var input = new KeyboardMenuNavigation();
            input.Read(false, false, true, true, false, false, 0f, .35f, .1f,
                out _, out bool confirm, out _);
            Assert.IsFalse(confirm);
            input.Read(false, false, false, false, false, false, .1f, .35f, .1f, out _, out _, out _);
            input.Read(false, true, false, false, false, false, .2f, .35f, .1f, out int move, out _, out _);
            Assert.AreEqual(1, move);
            input.Read(false, true, false, false, false, false, .3f, .35f, .1f, out move, out _, out _);
            Assert.AreEqual(0, move);
            input.Read(false, true, false, false, false, false, .6f, .35f, .1f, out move, out _, out _);
            Assert.AreEqual(1, move);
            input.Read(false, true, true, true, false, false, .7f, .35f, .1f, out move, out confirm, out _);
            Assert.IsTrue(confirm);
            Assert.AreEqual(0, move);
        }

        static void Publish(LevelController level) => typeof(LevelController)
            .GetMethod("PublishEnd", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(level, null);
    }
}
