using System;
using System.Collections.Generic;
using System.Linq;
using ShinySTG.Level;
using ShinySTG.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameFlow.Editor
{
    /// <summary>只创建缺少的资产；已有正式游戏场景和人物配置不会被覆盖。</summary>
    public static class FirstPlayableSetup
    {
        const string SourceScene = "Assets/Scenes/SampleScene.unity";
        const string GameScene = "Assets/Scenes/Gameplay.unity";
        const string MenuScene = "Assets/Scenes/StartMenu.unity";
        const string CharacterPath = "Assets/SO/GameFlow/NatsuhaA.asset";
        const string StagePath = "Assets/SO/GameFlow/FirstStage.asset";
        const string PlayerPath = "Assets/Prefabs/Player/NatsuhaA.prefab";

        [MenuItem("STG/Game Flow/Setup First Playable")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            // 不保存或丢弃用户正在编辑的场景；请先由用户自行保存。
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("请先保存当前场景，再运行 Setup First Playable。");

            var previous = SceneManager.GetActiveScene();
            Scene game = default;
            Scene menu = default;
            bool openedGame = false, openedMenu = false;
            try
            {
                Folder("Assets/SO", "GameFlow");
                Folder("Assets/Prefabs", "Player");
                bool newGame = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) == null;
                if (newGame && !AssetDatabase.CopyAsset(SourceScene, GameScene))
                    throw new InvalidOperationException("无法复制 SampleScene 为 Gameplay。");
                game = Open(GameScene, out openedGame);
                var players = Components<PlayerController>(game);
                var levels = Components<LevelController>(game);
                if (levels.Length != 1) throw new InvalidOperationException("Gameplay 必须恰有一个 LevelController。");
                var level = levels[0];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
                Vector3 spawn = players.Length == 1 ? players[0].transform.position : new Vector3(0f, -3f, 0f);
                if (prefab == null)
                {
                    if (players.Length != 1) throw new InvalidOperationException("提取角色需要测试场景中恰有一个玩家。");
                    prefab = PrefabUtility.SaveAsPrefabAsset(players[0].gameObject, PlayerPath);
                    if (prefab == null) throw new InvalidOperationException("无法保存玩家 prefab。");
                }
                var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath);
                if (character == null)
                {
                    character = ScriptableObject.CreateInstance<CharacterDefinition>();
                    character.Id = "natsuha-a";
                    character.DisplayName = "Natsuha A";
                    character.Description = "标准机体 · 方向键移动 / Z 射击 / Shift 低速";
                    character.PlayerPrefab = prefab.GetComponent<PlayerController>();
                    AssetDatabase.CreateAsset(character, CharacterPath);
                }
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath);
                if (stage == null)
                {
                    stage = ScriptableObject.CreateInstance<StageDefinition>();
                    stage.Id = "stage-1";
                    stage.ScenePath = GameScene;
                    stage.Level = level.Definition;
                    stage.SpawnPosition = spawn;
                    AssetDatabase.CreateAsset(stage, StagePath);
                }
                var request = new GameStartRequest(character, stage);
                if (!request.Validate(out var error)) throw new InvalidOperationException(error);

                if (newGame)
                {
                    // HUD 改为跟随动态生成的 Player.Instance。
                    foreach (var hud in Components<GameplayHudPresenter>(game)) SetReference(hud, "_player", null);
                    foreach (var player in players) Undo.DestroyObjectImmediate(player.gameObject);
                    // 测试场景的预放敌人不属于首关时间轴。
                    foreach (var enemy in Components<ShinySTG.EnemyAI.Enemy>(game))
                        if (enemy != null) Undo.DestroyObjectImmediate(enemy.gameObject);
                    Undo.RecordObject(level, "Disable automatic level start");
                    level.AutoStart = false;
                    var bootstrap = Undo.AddComponent<GameplayBootstrap>(level.gameObject);
                    SetReference(bootstrap, "_level", level);
                    SetReference(bootstrap, "_defaultCharacter", character);
                    SetReference(bootstrap, "_defaultStage", stage);
                    if (Components<GameplayHudPresenter>(game).Length == 0)
                    {
                        var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayHud.prefab");
                        if (hudPrefab == null) throw new InvalidOperationException("缺少 GameplayHud prefab。");
                        var hud = PrefabUtility.InstantiatePrefab(hudPrefab, game);
                        Undo.RegisterCreatedObjectUndo(hud, "Create gameplay HUD");
                    }
                    EditorSceneManager.MarkSceneDirty(game);
                    if (!EditorSceneManager.SaveScene(game)) throw new InvalidOperationException("保存 Gameplay 失败。");
                }
                else if (Components<GameplayBootstrap>(game).Length != 1)
                    throw new InvalidOperationException("已有 Gameplay 未配置唯一 Bootstrap；为保护已有场景，请手动配置或更名后重试。");

                menu = Open(MenuScene, out openedMenu);
                var flows = Components<FrontEndFlowController>(menu);
                var selections = Components<CharacterSelectView>(menu);
                if (flows.Length != 1 || selections.Length != 1)
                    throw new InvalidOperationException("开始场景需要唯一 FrontEndFlowController 和 CharacterSelectView。");
                SetReference(flows[0], "_firstStage", stage);
                var serialized = new SerializedObject(selections[0]);
                var entries = serialized.FindProperty("_characters");
                bool hasDefinition = false;
                for (int i = 0; i < entries.arraySize; i++)
                    hasDefinition |= entries.GetArrayElementAtIndex(i).FindPropertyRelative("Definition").objectReferenceValue != null;
                if (!hasDefinition)
                {
                    entries.arraySize = 1;
                    entries.GetArrayElementAtIndex(0).FindPropertyRelative("Definition").objectReferenceValue = character;
                    serialized.ApplyModifiedProperties();
                }
                var controls = selections[0].transform.Find("Controls")?.GetComponent<Text>();
                if (controls != null)
                {
                    Undo.RecordObject(controls, "Update character controls");
                    controls.text = "↑ ↓  切换角色    /    Z、Enter  开始    /    X、Esc  返回";
                    EditorUtility.SetDirty(controls);
                }
                EditorSceneManager.MarkSceneDirty(menu);
                if (!EditorSceneManager.SaveScene(menu)) throw new InvalidOperationException("保存开始场景失败。");
                var builds = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                foreach (string path in new[] { MenuScene, GameScene })
                {
                    var entry = builds.Find(s => s.path == path);
                    if (entry == null) builds.Add(new EditorBuildSettingsScene(path, true));
                    else entry.enabled = true;
                }
                EditorBuildSettings.scenes = builds.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[GameFlow] 首版配置完成：打开 StartMenu，选择角色并按 Z / Enter 进入游戏。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                // 异常时保留 dirty 场景，供检查和 Undo，不静默丢弃部分编辑。
                if (openedGame && game.IsValid() && !game.isDirty) EditorSceneManager.CloseScene(game, true);
                if (openedMenu && menu.IsValid() && !menu.isDirty) EditorSceneManager.CloseScene(menu, true);
            }
        }

        static Scene Open(string path, out bool opened)
        {
            var scene = SceneManager.GetSceneByPath(path);
            opened = !scene.IsValid() || !scene.isLoaded;
            return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
