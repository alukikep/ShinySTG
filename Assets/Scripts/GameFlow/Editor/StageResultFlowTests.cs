using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShinySTG.Level;
using ShinySTG.Player;
using ShinySTG.UI;
using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class StageResultFlowTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(false)]
        [TestCase(true)]
        public void ClearWaitsForConfirmationAndStaleAttemptCannotAdvance(bool finalStage)
        {
            var host = new GameObject("stage result test");
            host.SetActive(false);
            var playerObject = new GameObject("stage result player");
            playerObject.SetActive(false);
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            var sequence = ScriptableObject.CreateInstance<StageSequenceDefinition>();
            IEnumerator finish = null;
            GameFlowController flow = null;
            try
            {
                var player = playerObject.AddComponent<PlayerController>();
                playerObject.SetActive(true);
                character.PlayerPrefab = player;
                stage.Id = "test";
                stage.ScenePath = "Assets/Scenes/Gameplay.unity";
                stage.Level = definition;
                definition.AutoSwitchBgm = false;
                sequence.Stages = finalStage ? new[] { stage } : new[] { stage, stage };
                var session = new RunSession(GameStartRequest.FromSequence(character, sequence));
                var level = host.AddComponent<LevelController>();
                level.AutoStart = level.AutoFindBulletPool = false;
                level.Definition = definition;
                var bootstrap = host.AddComponent<GameplayBootstrap>();
                Set(bootstrap, "_level", level);
                Set(bootstrap, "<SpawnedPlayer>k__BackingField", player);
                flow = host.AddComponent<GameFlowController>();
                Set(flow, "_bootstrap", bootstrap);
                Set(flow, "<CurrentSession>k__BackingField", session);
                var viewObject = new GameObject("result view", typeof(RectTransform));
                viewObject.transform.SetParent(host.transform);
                var view = viewObject.AddComponent<StageResultView>();
                Call(view, "Awake");
                Set(flow, "_stageResultView", view);
                using var settlement = new StageSettlement(level, player, session);
                level.BeginLevel();
                player.Resources.AddScore(123);
                level.EndLevel(LevelEndReason.Cleared);
                Call(level, "PublishEnd");
                finish = (IEnumerator)Call(flow, "FinishStage", bootstrap, session, level.AttemptId);
                Assert.IsTrue(finish.MoveNext());
                Assert.IsTrue(finish.MoveNext(), "必须等待清场");
                Call(level, "ClearBattle");
                Assert.IsFalse(finish.MoveNext());
                Assert.IsTrue(flow.IsShowingStageResults);
                Assert.IsFalse(flow.IsShowingResults, "末关也必须先等待单关确认");
                Assert.AreEqual(0, session.CurrentStageIndex);
                Assert.AreEqual(123, session.Results[0].Score);
                Assert.IsTrue(PlayerControlLock.IsLocked);
                // 调试重开撤销结算；尚未执行 Update 时收到旧确认也必须被拒绝。
                level.ReloadLevel();
                Call(flow, "ConfirmStageResult");
                Assert.IsFalse(flow.IsShowingStageResults);
                Assert.IsFalse(view.gameObject.activeSelf);
                Assert.AreEqual(0, session.CurrentStageIndex);
                Assert.AreEqual(0, session.Results.Count);
                Assert.IsFalse(PlayerControlLock.IsLocked);
            }
            finally
            {
                (finish as System.IDisposable)?.Dispose();
                if (flow != null) Call(flow, "ReleaseControl");
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(sequence);
            }
        }

        [Test]
        public void ConfirmationRequiresReleaseAgainAfterFocusReset()
        {
            var navigation = new KeyboardMenuNavigation();
            bool Read(bool held, bool down)
            {
                navigation.Read(false, false, held, down, false, false, 0, .35f, .1f,
                    out _, out bool confirm, out _);
                return confirm;
            }
            Assert.IsFalse(Read(true, true));
            Assert.IsFalse(Read(true, false));
            Assert.IsFalse(Read(false, false));
            navigation.Reset();
            Assert.IsFalse(Read(true, true));
            Assert.IsFalse(Read(false, false));
            Assert.IsTrue(Read(true, true));
            Assert.IsFalse(Read(true, true));
        }

        static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, Private).Invoke(target, args);
    }
}
