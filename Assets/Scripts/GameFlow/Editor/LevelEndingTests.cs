using System.Reflection;
using NUnit.Framework;
using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class LevelEndingTests
    {
        [Test]
        public void FailureWinsPendingClearAndRestartRejectsOldAttempt()
        {
            var go = new GameObject("level ending test");
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var level = go.AddComponent<LevelController>();
                level.AutoStart = false;
                level.AutoFindBulletPool = false;
                definition.AutoSwitchBgm = false;
                level.Definition = definition;
                int notifications = 0, legacyNotifications = 0;
                level.OnLevelEnded += (_, reason) => notifications++;
                level.OnLevelComplete += _ => legacyNotifications++;
                level.BeginLevel();
                int oldAttempt = level.AttemptId;
                Assert.IsTrue(level.TryEndLevel(LevelEndReason.Cleared, oldAttempt));
                Assert.IsFalse(level.IsRunning);
                Assert.AreEqual(0, notifications);
                level.EndLevel(LevelEndReason.Failed);
                Publish(level);
                Assert.AreEqual(LevelEndReason.Failed, level.EndReason);
                Assert.AreEqual(1, notifications);
                Assert.AreEqual(1, legacyNotifications);
                Assert.IsFalse(level.TryEndLevel(LevelEndReason.Cleared, oldAttempt));
                level.ReloadLevel();
                Assert.IsNull(level.EndReason);
                Assert.IsFalse(level.TryEndLevel(LevelEndReason.Failed, oldAttempt));
                level.EndLevel(LevelEndReason.Cleared);
                level.ReloadLevel();
                Publish(level);
                Assert.AreEqual(1, notifications);
                level.EndLevel(LevelEndReason.Cleared);
                level.EndLevel(LevelEndReason.Aborted);
                Assert.AreEqual(LevelEndReason.Aborted, level.EndReason);
                Assert.AreEqual(2, notifications);
                level.ReloadLevel();
                level.EndLevel(LevelEndReason.Cleared);
                Publish(level);
                Assert.AreEqual(LevelEndReason.Cleared, level.EndReason);
                Assert.AreEqual(3, notifications);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(definition);
            }
        }

        static void Publish(LevelController level) => typeof(LevelController)
            .GetMethod("PublishEnd", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(level, null);
    }
}
