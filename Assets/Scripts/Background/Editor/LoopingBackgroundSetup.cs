using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    internal static class LoopingBackgroundSetup
    {
        [MenuItem("STG/Background/Upgrade Selected Background To Loop")]
        static void Upgrade()
        {
            var selected = Selection.activeGameObject;
            var root = selected != null ? selected.transform : null;
            while (root != null && root.name != "StageBackgroundRoot") root = root.parent;
            if (EditorApplication.isPlayingOrWillChangePlaymode || root == null
                || EditorUtility.IsPersistent(root) || !root.gameObject.scene.IsValid()
                || !root.gameObject.scene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null
                || PrefabUtility.IsPartOfPrefabInstance(root))
            {
                Debug.LogWarning("[Background] 请在非播放模式下，选中普通场景中的 StageBackgroundRoot 或其子物体；Prefab 实例须先解包。");
                return;
            }
            if (root.GetComponentInChildren<LoopingBackgroundStrip>(true) != null
                || root.Find("LoopingBackgroundContent") != null)
            {
                Debug.LogWarning("[Background] 已有循环布景，跳过重复升级。", root);
                return;
            }
            int layer = LayerMask.NameToLayer("Background3D");
            var original = root.Find("BackgroundContent");
            if (layer == -1 || original == null)
            {
                Debug.LogWarning("[Background] 缺少 Background3D 层或第一阶段的 BackgroundContent。", root);
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Upgrade Background To Loop");
            try
            {
                var content = BackgroundPrototypeSetup.CreateChild("LoopingBackgroundContent", root, layer);
                // 8 段覆盖后端 -80 至前端 176；默认相机远裁剪为 150。
                for (int i = 0; i < 8; i++)
                {
                    var segment = BackgroundPrototypeSetup.CreateChild($"Segment_{i:00}", content.transform, layer);
                    segment.transform.localPosition = new Vector3(0f, 0f, -64f + i * 32f);
                    BackgroundPrototypeSetup.CreateBlock("Road", segment.transform, layer,
                        new Vector3(0f, -0.25f, 0f), new Vector3(10f, 0.5f, 32f));
                    for (int j = 0; j < 4; j++)
                    {
                        float z = -12f + j * 8f;
                        float height = 2f + j % 3;
                        BackgroundPrototypeSetup.CreateBlock($"PillarLeft_{j}", segment.transform, layer,
                            new Vector3(-6f, height * 0.5f, z), new Vector3(1.5f, height, 1.5f));
                        BackgroundPrototypeSetup.CreateBlock($"PillarRight_{j}", segment.transform, layer,
                            new Vector3(6f, height * 0.5f, z), new Vector3(1.5f, height, 1.5f));
                    }
                }
                Undo.AddComponent<LoopingBackgroundStrip>(content);
                Undo.RecordObject(original.gameObject, "Hide Original Background");
                original.gameObject.SetActive(false);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                Selection.activeGameObject = content;
                Debug.Log("[Background] 已创建循环布景并隐藏旧 BackgroundContent。保存后进入 Play 即可滚动；组件菜单支持暂停、恢复、重置。", content);
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }
    }
}
