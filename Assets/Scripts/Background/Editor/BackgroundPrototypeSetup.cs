using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinySTG.Background.Editor
{
    /// <summary>创建第一阶段双相机占位布景；不负责运行时滚动或演出。</summary>
    internal static class BackgroundPrototypeSetup
    {
        const string RootName = "StageBackgroundRoot";
        const string LayerName = "Background3D";
        const string MenuPath = "STG/Background/Create Prototype For Selected Camera";

        [MenuItem(MenuPath)]
        static void CreatePrototype()
        {
            var gameplayCamera = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Camera>() : null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || gameplayCamera == null)
            {
                Debug.LogWarning("[Background] 请退出播放模式，在 Hierarchy 中选中战斗 Camera 后执行菜单。");
                return;
            }

            var scene = gameplayCamera.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || EditorUtility.IsPersistent(gameplayCamera)
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogWarning("[Background] 请在普通场景中操作，不能在 Prefab 编辑模式或资产上创建。", gameplayCamera);
                return;
            }

            if (GraphicsSettings.currentRenderPipeline != null || !gameplayCamera.orthographic
                || !gameplayCamera.isActiveAndEnabled || gameplayCamera.targetTexture != null)
            {
                Debug.LogWarning("[Background] 原型要求内置渲染管线、启用的正交战斗相机，且直接输出到屏幕。", gameplayCamera);
                return;
            }

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer == -1)
            {
                Debug.LogWarning("[Background] 请先在 Project Settings > Tags and Layers 的空闲 User Layer 添加 Background3D。");
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != RootName) continue;
                Debug.LogWarning("[Background] 场景中已有 StageBackgroundRoot，跳过重复创建。", root);
                Selection.activeGameObject = root;
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Background Prototype");
            try
            {
                var backgroundRoot = new GameObject(RootName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(backgroundRoot, scene);
                Undo.RegisterCreatedObjectUndo(backgroundRoot, "Create Background Root");
                backgroundRoot.layer = layer;

                var rig = CreateChild("CameraRig", backgroundRoot.transform, layer);
                rig.transform.localPosition = new Vector3(0f, 8f, -10f);
                rig.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                var cameraObject = CreateChild("BackgroundCamera", rig.transform, layer);
                var backgroundCamera = Undo.AddComponent<Camera>(cameraObject);
                backgroundCamera.orthographic = false;
                backgroundCamera.fieldOfView = 50f;
                backgroundCamera.nearClipPlane = 0.3f;
                backgroundCamera.farClipPlane = 150f;
                backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
                backgroundCamera.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
                backgroundCamera.cullingMask = 1 << layer;
                backgroundCamera.depth = gameplayCamera.depth - 1f;
                backgroundCamera.rect = gameplayCamera.rect;
                backgroundCamera.targetDisplay = gameplayCamera.targetDisplay;
                backgroundCamera.allowHDR = gameplayCamera.allowHDR;
                backgroundCamera.allowMSAA = gameplayCamera.allowMSAA;

                var content = CreateChild("BackgroundContent", backgroundRoot.transform, layer);
                CreateBlock("Road", content.transform, layer,
                    new Vector3(0f, -0.25f, 30f), new Vector3(10f, 0.5f, 100f));
                for (int i = 0; i < 10; i++)
                {
                    float z = -4f + i * 8f;
                    float height = 2f + i % 3;
                    CreateBlock($"PillarLeft_{i:00}", content.transform, layer,
                        new Vector3(-6f, height * 0.5f, z), new Vector3(1.5f, height, 1.5f));
                    CreateBlock($"PillarRight_{i:00}", content.transform, layer,
                        new Vector3(6f, height * 0.5f, z), new Vector3(1.5f, height, 1.5f));
                }

                var lightObject = CreateChild("BackgroundLight", backgroundRoot.transform, layer);
                lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
                var light = Undo.AddComponent<Light>(lightObject);
                light.type = LightType.Directional;
                light.intensity = 0.8f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << layer;

                Undo.RecordObject(gameplayCamera, "Configure Gameplay Camera Composition");
                gameplayCamera.clearFlags = CameraClearFlags.Depth;
                gameplayCamera.cullingMask &= ~(1 << layer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameplayCamera);
                EditorUtility.SetDirty(gameplayCamera);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = rig;
                Debug.Log("[Background] 原型已创建。请保存场景，进入 Play 后调整 CameraRig，验证战斗画面固定。可用一次 Undo 撤销整组配置。", backgroundRoot);
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        internal static GameObject CreateChild(string name, Transform parent, int layer)
        {
            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create Background Object");
            Undo.SetTransformParent(child.transform, parent, "Parent Background Object");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            child.layer = layer;
            return child;
        }

        internal static void CreateBlock(string name, Transform parent, int layer, Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(block, "Create Background Block");
            block.name = name;
            block.layer = layer;
            Undo.SetTransformParent(block.transform, parent, "Parent Background Block");
            block.transform.localPosition = position;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = scale;
            Undo.DestroyObjectImmediate(block.GetComponent<Collider>());
            var renderer = block.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
