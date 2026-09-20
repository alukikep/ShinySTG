using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinySTG.GameFlow.Editor
{
    [CustomEditor(typeof(StageFadePrototype))]
    public sealed class StageFadePrototypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("视觉原型：保留移动，不清场、不暂停战斗。请在安全的空场测试。仅映射玩家根节点的普通 SpriteRenderer，不复制自定义材质效果。", MessageType.Info);
            var prototype = (StageFadePrototype)target;
            if (prototype.BattleArea == null)
                EditorGUILayout.HelpBox("必须绑定 Battle Area。可创建空 UI 矩形后用 Rect Tool 调整到主画面边界，不要包含侧栏。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(prototype.BattleArea == null ? "创建并绑定 BattleArea" : "选中 BattleArea 调整范围"))
                    CreateBattleArea(prototype);
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !prototype.isActiveAndEnabled))
            {
                using (new EditorGUI.DisabledScope(prototype.IsPlaying))
                    if (GUILayout.Button("播放渐黑 / 全黑移动 / 渐亮")) prototype.PlayPrototype();
                using (new EditorGUI.DisabledScope(!prototype.IsPlaying))
                    if (GUILayout.Button("取消并恢复画面")) prototype.Cancel();
            }
        }

        static void CreateBattleArea(StageFadePrototype prototype)
        {
            if (prototype.BattleArea != null)
            {
                Selection.activeGameObject = prototype.BattleArea.gameObject;
                return;
            }
            var scene = prototype.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || EditorUtility.IsPersistent(prototype)
                || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            ShinySTG.UI.GameplayViewportLayout layout = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<ShinySTG.UI.GameplayViewportLayout>(true))
            {
                if (layout != null)
                {
                    Debug.LogWarning("[StageFade] 场景有多个 HUD，请手动创建并绑定 Battle Area。", prototype);
                    return;
                }
                layout = candidate;
            }
            if (layout == null || layout.LayoutRoot == null || layout.LayoutRoot.GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning("[StageFade] 未找到唯一 HUD LayoutRoot。请在 HUD Canvas 下创建空 UI 矩形并拖入 Battle Area。", prototype);
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Battle Area");
            try
            {
                var go = new GameObject("BattleArea", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Create Battle Area");
                Undo.SetTransformParent(go.transform, layout.LayoutRoot, "Parent Battle Area");
                var rect = (RectTransform)go.transform;
                Undo.RecordObject(rect, "Initialize Battle Area");
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                // 初始占位范围；由用户在 Scene 视图对齐实际画面，不推断侧栏布局。
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(0.7f, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Undo.RecordObject(prototype, "Bind Battle Area");
                prototype.ConfigureBattleArea(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(prototype);
                EditorUtility.SetDirty(prototype);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = go;
                Debug.Log("[StageFade] 已创建占位 BattleArea，请用 Rect Tool 调整到实际主画面边界并保存场景。", go);
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        [MenuItem("STG/Game Flow/Create Fade Prototype For Selected Camera")]
        static void Create()
        {
            var camera = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Camera>() : null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || camera == null
                || EditorUtility.IsPersistent(camera) || PrefabStageUtility.GetCurrentPrefabStage() != null
                || !camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded
                || !camera.orthographic || !camera.isActiveAndEnabled || camera.targetTexture != null
                || camera.targetDisplay != 0)
            {
                Debug.LogWarning("[StageFade] 请在普通场景中选中输出到主屏幕的正交战斗 Camera，退出 Play 后执行菜单。");
                return;
            }
            var scene = camera.gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<StageFadePrototype>(true);
                if (existing == null) continue;
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Stage Fade Prototype");
            try
            {
                var root = new GameObject("StageFadePrototype");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create Stage Fade Prototype");
                var prototype = Undo.AddComponent<StageFadePrototype>(root);
                Undo.RecordObject(prototype, "Bind Gameplay Camera");
                prototype.Configure(camera);
                EditorUtility.SetDirty(prototype);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
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
