using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    public sealed class BackgroundPrefabBuilder : EditorWindow
    {
        [SerializeField] LoopingBackgroundStrip _strip;
        [SerializeField] GameObject _prefab;
        [SerializeField] float _length = 32f;
        [SerializeField] float _rearEdge = -48f;
        [SerializeField] int _count = 8;

        [MenuItem("STG/Background/Build Loop From Prefab")]
        static void Open()
        {
            var window = GetWindow<BackgroundPrefabBuilder>("Background Prefab Builder");
            var selected = Selection.activeGameObject;
            if (selected != null)
                window.SetStrip(selected.GetComponentInParent<LoopingBackgroundStrip>());
            window.Show();
        }

        void SetStrip(LoopingBackgroundStrip strip)
        {
            if (strip == null) return;
            _strip = strip;
            var serialized = new SerializedObject(strip);
            _length = serialized.FindProperty("_segmentLength").floatValue;
            _rearEdge = serialized.FindProperty("_rearEdge").floatValue;
            _count = Mathf.Clamp(strip.transform.childCount, 2, 128);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("替换目标循环组件下的全部子物体，支持一次 Undo。相机、速度、暂停设置及外部绑定保持不变。生成后请保存场景。", MessageType.Info);
            var strip = (LoopingBackgroundStrip)EditorGUILayout.ObjectField("Target Strip", _strip, typeof(LoopingBackgroundStrip), true);
            if (strip != _strip) { _strip = strip; SetStrip(strip); }
            _prefab = (GameObject)EditorGUILayout.ObjectField("Segment Prefab", _prefab, typeof(GameObject), false);
            _length = EditorGUILayout.FloatField("Segment Length", _length);
            _rearEdge = EditorGUILayout.FloatField("Rear Edge", _rearEdge);
            _count = EditorGUILayout.IntField("Segment Count", _count);
            EditorGUILayout.HelpBox("Prefab 根节点保持零位置/旋转、单位缩放；沿局部 Z 轴拼接，几何中心在原点，接缝为 ±半段长度。长度由你指定，不根据装饰物包围盒推算。", MessageType.None);
            string error = Validate();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            using (new EditorGUI.DisabledScope(error != null))
                if (GUILayout.Button("Replace Segments From Prefab")) Build();
        }

        string Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "请退出 Play 后生成。";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "请退出 Prefab 编辑模式。";
            if (_strip == null || EditorUtility.IsPersistent(_strip) || !_strip.gameObject.scene.IsValid()
                || !_strip.gameObject.scene.isLoaded) return "请选择场景中的 LoopingBackgroundStrip。";
            if (PrefabUtility.IsPartOfPrefabInstance(_strip)) return "目标循环组件属于 Prefab 实例，请先解包目标。路段 Prefab 不需解包。";
            if (_prefab == null || !PrefabUtility.IsPartOfPrefabAsset(_prefab)
                || AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(_prefab)) != _prefab)
                return "请选择 Project 中的 Prefab 根资产。";
            if (!_prefab.activeSelf) return "路段 Prefab 根节点必须启用。";
            if (_prefab.transform.localPosition != Vector3.zero
                || Quaternion.Angle(_prefab.transform.localRotation, Quaternion.identity) > 0.001f
                || _prefab.transform.localScale != Vector3.one) return "请将 Prefab 根节点设为零位置/旋转、单位缩放，造型偏移放在子物体。";
            if (float.IsNaN(_length) || float.IsInfinity(_length) || _length <= 0f
                || float.IsNaN(_rearEdge) || float.IsInfinity(_rearEdge)
                || _count < 2 || _count > 128 || float.IsInfinity(_rearEdge + _length * _count))
                return "长度必须为有限正数，后端位置必须有限，数量需为 2~128。";
            if (LayerMask.NameToLayer("Background3D") < 0) return "请先创建 Background3D Layer。";
            // 白名单含非激活子物体和缺失脚本；本版只允许静态几何。
            foreach (var component in _prefab.GetComponentsInChildren<Component>(true))
                if (!(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer)
                    && !(component is SpriteRenderer) && !(component is LODGroup))
                    return "路段仅支持 Transform、MeshFilter、MeshRenderer、SpriteRenderer、LODGroup。请移除碰撞体、脚本、粒子、动画及 Missing Script。";
            if (_prefab.GetComponentsInChildren<Renderer>(true).Length == 0) return "Prefab 中没有可绘制的静态几何。";
            return null;
        }

        void Build()
        {
            string error = Validate();
            if (error != null) { Debug.LogWarning(error); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Background From Prefab");
            try
            {
                // 先完整创建新实例，成功后再移除原路段；异常回滚整组。
                var previous = new GameObject[_strip.transform.childCount];
                for (int i = 0; i < previous.Length; i++) previous[i] = _strip.transform.GetChild(i).gameObject;
                int layer = LayerMask.NameToLayer("Background3D");
                for (int i = 0; i < _count; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, _strip.transform);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Background Segment");
                    Undo.RecordObject(instance, "Name Background Segment");
                    instance.name = $"Segment_{i:00}";
                    Undo.RecordObject(instance.transform, "Position Background Segment");
                    instance.transform.localPosition = new Vector3(0f, 0f, _rearEdge - _length * 0.5f + i * _length);
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                    {
                        Undo.RecordObject(child.gameObject, "Set Background Layer");
                        child.gameObject.layer = layer;
                        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                    }
                }
                foreach (var old in previous) Undo.DestroyObjectImmediate(old);
                var serialized = new SerializedObject(_strip);
                serialized.FindProperty("_segmentLength").floatValue = _length;
                serialized.FindProperty("_rearEdge").floatValue = _rearEdge;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_strip);
                EditorSceneManager.MarkSceneDirty(_strip.gameObject.scene);
                Selection.activeGameObject = _strip.gameObject;
                Debug.Log("[Background] Prefab 循环布景已生成。请保存场景并验证接缝及镜头覆盖范围；一次 Undo 可恢复旧布景。", _strip);
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
