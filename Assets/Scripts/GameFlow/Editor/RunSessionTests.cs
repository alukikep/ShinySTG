using NUnit.Framework;
using ShinySTG.Level;
using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class RunSessionTests
    {
        [Test]
        public void SequenceSnapshotsOrderAndRejectsInvalidLaterStages()
        {
            var sequence = ScriptableObject.CreateInstance<StageSequenceDefinition>();
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            var first = ScriptableObject.CreateInstance<StageDefinition>();
            var second = ScriptableObject.CreateInstance<StageDefinition>();
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var player = new GameObject("sequence test player");
            player.SetActive(false);
            try
            {
                character.PlayerPrefab = player.AddComponent<PlayerController>();
                player.SetActive(true);
                first.Id = "first";
                second.Id = "second";
                first.ScenePath = second.ScenePath = "Assets/Scenes/Gameplay.unity";
                first.Level = second.Level = level;
                sequence.Stages = new[] { first, second };
                var request = GameStartRequest.FromSequence(character, sequence);
                Assert.IsTrue(request.Validate(out _));
                sequence.Stages[0] = second;
                Assert.AreSame(first, request.Stage);
                second.Level = null;
                Assert.IsFalse(request.Validate(out var error));
                StringAssert.Contains("第 2 关", error);
                second.Level = level;
                second.ScenePath = "Assets/Scenes/Other.unity";
                Assert.IsFalse(request.Validate(out _));
                second.ScenePath = first.ScenePath;
                var session = new RunSession(request);
                Assert.IsFalse(session.TryAdvanceStage());
                var result = new StageResult(0, "first", 100, 100, 3, 2, 125, 4);
                Assert.IsTrue(session.TryRecordResult(result));
                Assert.IsFalse(session.TryRecordResult(result));
                Assert.IsTrue(session.TryAdvanceStage());
                Assert.AreSame(second, session.CurrentStage);
                Assert.IsFalse(session.TryRecordResult(result));
                Assert.IsTrue(session.TryRecordResult(new StageResult(1, "second", 50, 150, 2, 1, 125, 7)));
                Assert.IsTrue(session.IsComplete);
                Assert.IsFalse(session.TryAdvanceStage());
                Assert.AreEqual(2, session.Results.Count);
                sequence.Stages = new StageDefinition[0];
                Assert.IsFalse(GameStartRequest.FromSequence(character, sequence).Validate(out _));
                Assert.IsTrue(new GameStartRequest(character, first).Validate(out _));
                var levelObject = new GameObject("settlement test level");
                try
                {
                    var controller = levelObject.AddComponent<LevelController>();
                    controller.AutoStart = false;
                    controller.AutoFindBulletPool = false;
                    controller.Definition = level;
                    level.AutoSwitchBgm = false;
                    var single = new RunSession(new GameStartRequest(character, first));
                    var actualPlayer = character.PlayerPrefab;
                    actualPlayer.Resources.AddScore(100);
                    using (var settlement = new StageSettlement(controller, actualPlayer, single))
                    {
                        controller.BeginLevel();
                        actualPlayer.Resources.AddScore(50);
                        controller.EndLevel(LevelEndReason.Cleared);
                        typeof(LevelController).GetMethod("PublishEnd", System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
                        Assert.AreEqual(1, single.Results.Count);
                        Assert.AreEqual(50, single.Results[0].Score);
                        Assert.AreEqual(150, single.Results[0].TotalScore);
                        controller.ReloadLevel();
                        Assert.AreEqual(0, single.Results.Count);
                        actualPlayer.Resources.AddScore(25);
                        controller.EndLevel(LevelEndReason.Aborted);
                        Assert.AreEqual(0, single.Results.Count);
                    }

                    // 连续两关共用玩家：资源和累计分数保留，每关重新建立得分基线。
                    var twoStages = new RunSession(request);
                    using (var settlement = new StageSettlement(controller, actualPlayer, twoStages))
                    {
                        controller.BeginLevel();
                        actualPlayer.Resources.AddScore(40);
                        controller.EndLevel(LevelEndReason.Cleared);
                        var publish = typeof(LevelController).GetMethod("PublishEnd",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        publish.Invoke(controller, null);
                        Assert.IsTrue(controller.IsBattleCleanupPending);
                        Assert.AreEqual(40, twoStages.Results[0].Score);
                        Assert.IsTrue(twoStages.TryAdvanceStage());
                        int lives = actualPlayer.Health.Lives;
                        int bombs = actualPlayer.Resources.Bombs;
                        long total = actualPlayer.Resources.Score;
                        using (BattleRestriction.Acquire())
                        {
                            controller.Definition = second.Level;
                            controller.BeginLevel();
                            Assert.IsTrue(BattleRestriction.IsActive, "揭幕前宿主限制必须保持");
                            Assert.IsFalse(controller.IsBattleCleanupPending);
                            Assert.AreEqual(1, twoStages.Results.Count);
                            Assert.AreEqual(lives, actualPlayer.Health.Lives);
                            Assert.AreEqual(bombs, actualPlayer.Resources.Bombs);
                            Assert.AreEqual(total, actualPlayer.Resources.Score);
                        }
                        Assert.IsFalse(BattleRestriction.IsActive);
                        actualPlayer.Resources.AddScore(60);
                        controller.EndLevel(LevelEndReason.Cleared);
                        publish.Invoke(controller, null);
                        publish.Invoke(controller, null);
                        Assert.IsTrue(twoStages.IsComplete);
                        Assert.AreEqual(2, twoStages.Results.Count);
                        Assert.AreEqual(60, twoStages.Results[1].Score);
                        Assert.AreEqual(total + 60, twoStages.Results[1].TotalScore);
                    }
                }
                finally { Object.DestroyImmediate(levelObject); }
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(sequence);
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(level);
            }
        }
    }
}
