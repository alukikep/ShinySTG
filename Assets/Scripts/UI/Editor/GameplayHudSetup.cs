using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinySTG.UI.Editor
{
    internal static class GameplayHudSetup
    {
        const string PrefabPath = "Assets/Prefabs/UI/GameplayHud.prefab";

        [MenuItem("STG/UI/Create Gameplay HUD")]
        static void Create()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null
                || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<GameplayHudPresenter>(true);
                if (existing != null)
                {
                    Selection.activeGameObject = existing.gameObject;
                    Debug.LogWarning("[HUD] 场景已有 HUD，跳过重复创建。", existing);
                    return;
                }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[HUD] 找不到 " + PrefabPath);
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Gameplay HUD");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create Gameplay HUD");
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
    }
}
