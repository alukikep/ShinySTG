using System.Reflection;
using NUnit.Framework;
using ShinySTG.Audio;
using ShinySTG.Level;
using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class GameFlowTests
    {
        [Test]
        public void MissingAndDisabledPlayerAreRejectedBeforeLoading()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var playerObject = new GameObject("request test player");
            playerObject.SetActive(false);
            try
            {
                stage.ScenePath = "Assets/Scenes/Gameplay.unity";
                stage.Level = level;
                var request = new GameStartRequest(character, stage);
                Assert.IsFalse(request.Validate(out _));
                character.PlayerPrefab = playerObject.AddComponent<PlayerController>();
                Assert.IsFalse(request.Validate(out _));
                playerObject.SetActive(true);
                Assert.IsTrue(request.Validate(out _));
                stage.SpawnPosition = new Vector3(float.NaN, 0, 0);
                Assert.IsFalse(request.Validate(out _));
                stage.SpawnPosition = Vector3.zero;
                stage.Level = null;
                Assert.IsFalse(request.Validate(out _));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void AudioRebindsWhenControllerChangesAndUnbindsForSilentStage()
        {
            var first = new GameObject("first level").AddComponent<LevelController>();
            var second = new GameObject("second level").AddComponent<LevelController>();
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            var binding = ScriptableObject.CreateInstance<LevelAudioBinding>();
            var instanceField = typeof(Singleton<LevelController>).GetField("<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instanceField.GetValue(null);
            var hub = new AudioEventHub(null);
            var subscribed = typeof(AudioEventHub).GetField("_subscribedLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                definition.AudioBinding = binding;
                instanceField.SetValue(null, first);
                hub.TryBind(definition);
                Assert.AreSame(first, subscribed.GetValue(hub));
                instanceField.SetValue(null, second);
                hub.TryBind(definition);
                Assert.AreSame(second, subscribed.GetValue(hub));
                definition.AutoSwitchBgm = false;
                hub.TryBind(definition);
                Assert.IsNull(subscribed.GetValue(hub));
            }
            finally
            {
                hub.DisableAutoSwitch();
                instanceField.SetValue(null, previous);
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(binding);
            }
        }
    }
}
