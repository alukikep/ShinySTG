using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    public sealed class BackgroundFogMaterialWindow : EditorWindow
    {
        const string ShaderName = "ShinySTG/Background/Distance Fog Unlit";
        [SerializeField] GameObject _root;
        [SerializeField] Material _material;

        [MenuItem("STG/Background/Apply Fog Material")]
        static void Open()
        {
            var window = GetWindow<BackgroundFogMaterialWindow>("Background Fog");
            window._root = Selection.activeGameObject;
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("在 Project 创建 Material，将 Shader 设为 ShinySTG/Background/Distance Fog Unlit，再拖入此处。会替换目标层级内 Background3D 层 MeshRenderer 的所有材质槽，包含非激活对象。支持整组 Undo。", MessageType.Info);
            _root = (GameObject)EditorGUILayout.ObjectField("Background Root", _root, typeof(GameObject), true);
            _material = (Material)EditorGUILayout.ObjectField("Fog Material", _material, typeof(Material), false);
            EditorGUILayout.HelpBox("建议选择 LoopingBackgroundContent。该材质是无光照、不透明材质；批量应用会统一贴图与颜色，不自动转换原材质。多种贴图请分别制作雾材质并手动赋值。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || _root == null || _material == null))
                if (GUILayout.Button("Apply To Background Meshes")) Apply();
        }

        void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || _root == null || _material == null) return;
            if (EditorUtility.IsPersistent(_root) || !_root.scene.IsValid() || !_root.scene.isLoaded
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogWarning("[Background] 请在普通场景中选择布景根节点。Prefab 材质请在 Prefab 模式手动配置。");
                return;
            }
            int layer = LayerMask.NameToLayer("Background3D");
            if (layer < 0 || _material.shader == null || _material.shader.name != ShaderName
                || !EditorUtility.IsPersistent(_material))
            {
                Debug.LogWarning("[Background] 请配置 Background3D 层，并指定使用背景距离雾 Shader 的材质资产。");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Background Fog Material");
            try
            {
                int count = 0;
                foreach (var renderer in _root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.gameObject.layer != layer) continue;
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0) continue;
                    Undo.RecordObject(renderer, "Assign Background Fog Material");
                    for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    EditorUtility.SetDirty(renderer);
                    count++;
                }
                if (count > 0) EditorSceneManager.MarkSceneDirty(_root.scene);
                Debug.Log($"[Background] 已为 {count} 个背景 MeshRenderer 应用雾材质。", _root);
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
