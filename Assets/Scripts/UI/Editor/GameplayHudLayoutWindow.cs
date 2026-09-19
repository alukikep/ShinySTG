using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace ShinySTG.UI.Editor
{
    /// <summary>在参考构图坐标中编辑矩形，不需要缩放 Scene 中的 Canvas。</summary>
    internal sealed class GameplayHudLayoutWindow : EditorWindow
    {
        [SerializeField] GameplayViewportLayout _layout;
        [SerializeField] RectTransform _target;
        [SerializeField] float _zoom = 1f;
        Vector2 _pan, _mouseStart;
        Rect _dragStart;
        int _handle = -1, _undoGroup = -1, _control;
        RectTransform _dragTarget;
        readonly Vector3[] _corners = new Vector3[4];

        [MenuItem("STG/UI/HUD Layout Editor")]
        public static void Open() => GetWindow<GameplayHudLayoutWindow>("HUD 布局");
        public static void Open(GameplayViewportLayout layout)
        {
            var window = GetWindow<GameplayHudLayoutWindow>("HUD 布局");
            window.Bind(layout);
        }
        void OnEnable()
        {
            minSize = new Vector2(620, 520);
            Undo.undoRedoPerformed += Refresh;
            Selection.selectionChanged += FollowSelection;
            FollowSelection();
        }
        void OnDisable()
        {
            EndDrag();
            Undo.undoRedoPerformed -= Refresh;
            Selection.selectionChanged -= FollowSelection;
        }
        void OnLostFocus() => EndDrag();
        void FollowSelection()
        {
            var selected = Selection.activeGameObject;
            var layout = selected != null ? selected.GetComponentInParent<GameplayViewportLayout>() : null;
            if (layout != null && layout != _layout) Bind(layout);
        }
        void Bind(GameplayViewportLayout layout)
        {
            EndDrag(); _layout = layout; _target = layout != null ? layout.Sidebar : null;
            _pan = Vector2.zero; _zoom = 1;
            Repaint();
        }
        void Refresh() { Repaint(); SceneView.RepaintAll(); }

        void OnGUI()
        {
            if (Application.isPlaying && _handle >= 0 && _handle != 9) EndDrag();
            var layout = (GameplayViewportLayout)EditorGUILayout.ObjectField("HUD", _layout, typeof(GameplayViewportLayout), true);
            if (layout != _layout) Bind(layout);
            if (_layout == null)
            {
                EditorGUILayout.HelpBox("选择场景或 Prefab 模式中的 GameplayHud，或将它拖入上方字段。", MessageType.Info);
                return;
            }
            if (EditorUtility.IsPersistent(_layout))
            {
                EditorGUILayout.HelpBox("请先双击打开 HUD prefab，再选择其中的根节点进行编辑。", MessageType.Info);
                return;
            }
            if (_layout.LayoutRoot == null || _layout.LayoutRoot.rect.width <= 0 || _layout.LayoutRoot.rect.height <= 0)
            {
                EndDrag();
                EditorGUILayout.HelpBox("请绑定有有效尺寸的 LayoutRoot。", MessageType.Warning);
                return;
            }
            var targetUI = (RectTransform)EditorGUILayout.ObjectField("编辑 UI", _target, typeof(RectTransform), true);
            if (targetUI != _target) { EndDrag(); _target = targetUI; }
            if (!CanEdit(_target))
            {
                EditorGUILayout.HelpBox("选择 LayoutRoot 下的面板、Row 或文本。编辑对象及其父级需无旋转且单位缩放。", MessageType.Info);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("聚焦所选 UI")) Focus(_target);
            if (GUILayout.Button("适应窗口")) { _zoom = 1; _pan = Vector2.zero; }
            _zoom = EditorGUILayout.Slider(_zoom, .25f, 3f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("紫色是所选 UI。内部拖动移动，白色手柄缩放；滚轮缩放，中键平移。数值使用 LayoutRoot 实际 UI 单位。请用相同 Game 分辨率比较编辑与 Play。", MessageType.None);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                RectTransform target = _target;
                Rect rect = ReadRect(target);
                EditorGUI.BeginChangeCheck();
                Rect edited = EditorGUILayout.RectField("位置与尺寸（左上原点）", rect);
                if (EditorGUI.EndChangeCheck()) Change(target, edited);
                Vector2 extent = _layout.LayoutRoot.rect.size;
                EditorGUI.BeginChangeCheck();
                Vector4 margins = EditorGUILayout.Vector4Field("边距：左 / 上 / 右 / 下", new Vector4(rect.xMin, rect.yMin, extent.x - rect.xMax, extent.y - rect.yMax));
                if (EditorGUI.EndChangeCheck()) Change(target, Rect.MinMaxRect(margins.x, margins.y, extent.x - margins.z, extent.y - margins.w));
            }
            Rect viewport = GUILayoutUtility.GetRect(100, 10000, 160, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreview(viewport);
        }

        bool CanEdit(RectTransform rect)
        {
            if (_layout.LayoutRoot == null || rect == null || rect == _layout.LayoutRoot || !rect.IsChildOf(_layout.LayoutRoot)) return false;
            for (Transform t = rect; t != _layout.LayoutRoot; t = t.parent)
                if (Quaternion.Angle(t.localRotation, Quaternion.identity) > .01f || (t.localScale - Vector3.one).sqrMagnitude > .0001f) return false;
            return true;
        }

        Rect ReadRect(RectTransform target)
        {
            target.GetWorldCorners(_corners);
            Rect parent = _layout.LayoutRoot.rect;
            Vector3 min = _layout.LayoutRoot.InverseTransformPoint(_corners[0]);
            Vector3 max = _layout.LayoutRoot.InverseTransformPoint(_corners[2]);
            Vector2 reference = _layout.LayoutRoot.rect.size;
            return new Rect((min.x - parent.xMin) / parent.width * reference.x,
                (parent.yMax - max.y) / parent.height * reference.y,
                (max.x - min.x) / parent.width * reference.x, (max.y - min.y) / parent.height * reference.y);
        }
        void Change(RectTransform target, Rect rect)
        {
            if (!Finite(rect.x) || !Finite(rect.y) || !Finite(rect.width) || !Finite(rect.height)) return;
            rect.width = Mathf.Max(1, rect.width); rect.height = Mathf.Max(1, rect.height);
            Undo.RecordObject(target, "Edit HUD UI");
            ApplyRect(_layout.LayoutRoot, target, rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            if (target.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Refresh();
        }
        internal static void ApplyRect(RectTransform root, RectTransform target, Rect rect)
        {
            var parent = (RectTransform)target.parent;
            Vector3 min = parent.InverseTransformPoint(root.TransformPoint(new Vector3(root.rect.xMin + rect.xMin, root.rect.yMax - rect.yMax, 0)));
            Vector3 max = parent.InverseTransformPoint(root.TransformPoint(new Vector3(root.rect.xMin + rect.xMax, root.rect.yMax - rect.yMin, 0)));
            target.offsetMin = (Vector2)min - parent.rect.min - target.anchorMin * parent.rect.size;
            target.offsetMax = (Vector2)max - parent.rect.min - target.anchorMax * parent.rect.size;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Focus(RectTransform rect)
        {
            Selection.activeGameObject = rect.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        void DrawPreview(Rect viewport)
        {
            EditorGUI.DrawRect(viewport, new Color(.09f, .10f, .13f));
            GUI.BeginGroup(viewport);
            Vector2 extent = _layout.LayoutRoot.rect.size;
            float scale = Mathf.Max(.001f, Mathf.Min((viewport.width - 40) / extent.x, (viewport.height - 40) / extent.y) * _zoom);
            Vector2 origin = (viewport.size - extent * scale) * .5f + _pan;
            Rect composition = new Rect(origin, extent * scale);
            EditorGUI.DrawRect(composition, new Color(.16f, .18f, .22f));
            Rect selected = Map(ReadRect(_target), origin, scale);
            DrawBox(selected, new Color(.65f, .35f, .8f, .5f), _target.name);
            for (int i = 0; i < 8; i++) EditorGUI.DrawRect(Grip(selected, i), Color.white);
            GUI.Label(new Rect(8, 8, 400, 20), $"参考构图 {extent.x:0} × {extent.y:0}   缩放 {_zoom:0.00}x", EditorStyles.whiteMiniLabel);
            HandleInput(viewport.size, selected, scale);
            GUI.EndGroup();
        }
        static Rect Map(Rect rect, Vector2 origin, float scale) => new Rect(origin + rect.position * scale, rect.size * scale);
        static void DrawBox(Rect rect, Color color, string label)
        {
            EditorGUI.DrawRect(rect, color);
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, 100, 24), label, EditorStyles.whiteLabel);
        }
        static Rect Grip(Rect rect, int index)
        {
            Vector2 point = new Vector2(index % 3 == 0 ? rect.xMin : index % 3 == 1 ? rect.center.x : rect.xMax,
                index < 3 ? rect.yMin : index < 5 ? rect.center.y : rect.yMax);
            // 顺序：左上、中上、右上、左中、右中、左下、中下、右下。
            if (index == 4) point.x = rect.xMax;
            if (index >= 5) point.x = index == 5 ? rect.xMin : index == 6 ? rect.center.x : rect.xMax;
            return new Rect(point - Vector2.one * 4, Vector2.one * 8);
        }
        void HandleInput(Vector2 available, Rect selected, float scale)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            bool inside = new Rect(Vector2.zero, available).Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && inside && _handle < 0)
            { _zoom = Mathf.Clamp(_zoom * Mathf.Exp(-e.delta.y * .06f), .25f, 3); e.Use(); Repaint(); }
            if (e.type == EventType.MouseDown && inside && (e.button == 2 || e.button == 0 && !Application.isPlaying))
            {
                _handle = e.button == 2 ? 9 : -1;
                if (_handle < 0)
                    for (int i = 0; i < 8; i++) if (Grip(selected, i).Contains(e.mousePosition)) { _handle = i; break; }
                if (_handle < 0)
                {
                    if (!selected.Contains(e.mousePosition)) return;
                    _handle = 8;
                }
                _dragTarget = _target;
                _dragStart = ReadRect(_dragTarget); _mouseStart = e.mousePosition;
                _control = id; GUIUtility.hotControl = id;
                Undo.IncrementCurrentGroup(); _undoGroup = Undo.GetCurrentGroup();
                e.Use(); Repaint();
            }
            if (_handle >= 0 && GUIUtility.hotControl == _control && e.type == EventType.MouseDrag)
            {
                if (_handle == 9) { _pan += e.delta; }
                else
                {
                    Vector2 delta = (e.mousePosition - _mouseStart) / scale;
                    Rect rect = _dragStart;
                    if (_handle == 8) rect.position += delta;
                    else
                    {
                        if (_handle == 0 || _handle == 3 || _handle == 5) rect.xMin = Mathf.Min(rect.xMax - 1, rect.xMin + delta.x);
                        if (_handle == 2 || _handle == 4 || _handle == 7) rect.xMax = Mathf.Max(rect.xMin + 1, rect.xMax + delta.x);
                        if (_handle < 3) rect.yMin = Mathf.Min(rect.yMax - 1, rect.yMin + delta.y);
                        if (_handle >= 5) rect.yMax = Mathf.Max(rect.yMin + 1, rect.yMax + delta.y);
                    }
                    Change(_dragTarget, rect);
                }
                e.Use(); Repaint();
            }
            if (_handle >= 0 && e.rawType == EventType.MouseUp) { EndDrag(); e.Use(); }
        }
        void EndDrag()
        {
            if (_undoGroup >= 0) { Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(_undoGroup); }
            if (_control != 0 && GUIUtility.hotControl == _control) GUIUtility.hotControl = 0;
            _handle = -1; _undoGroup = -1; _control = 0; _dragTarget = null;
        }
    }
}
