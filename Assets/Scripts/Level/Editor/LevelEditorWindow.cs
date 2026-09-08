using ShinySTG.Level;
using System;
using SerializeReferenceEditor;
using ShinySTG.Level.Editor.Gizmos;
using ShinySTG.Level.Editor.Views;
using ShinySTG.Level.Editor.Views.Preview;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor
{
    /// <summary>
    /// 关卡编辑器主窗口:菜单 STG → Level Editor 打开。
    /// 装配三个视图 + Preview + Gizmos + Toolbar + StatusBar。
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        [SerializeField] LevelDefinition _definition;

        LevelEditorContext     _ctx;
        LevelEntryListView     _listView;
        LevelTimelineView      _timelineView;
        LevelEntryDetailView   _detailView;
        ILevelEditorPreview    _preview;

        public static LevelDefinition CurrentDefinition;
        public static SpawnEntry      CurrentSelected;
        // 最后一个被打开的关卡编辑器实例(供非 EditorWindow 上下文里调 Repaint)
        public static LevelEditorWindow CurrentWindow { get; private set; }

        double _lastEditorTime;

        [MenuItem("STG/Level Editor")]
        public static void Open()
        {
            var win = GetWindow<LevelEditorWindow>("Level Editor");
            win.minSize = new Vector2(900, 400);
            CurrentWindow = win;
            win.Show();
        }

        public static void OpenWith(LevelDefinition def)
        {
            var win = GetWindow<LevelEditorWindow>("Level Editor");
            CurrentWindow = win;
            win.LoadDefinition(def);
            win.Show();
        }

        // ─── 生命周期 ─────────────────────────────────────────
        void OnEnable()
        {
            _preview = new LevelEditorPlayer();
            RebuildViews();
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _preview?.Stop();
            _timelineView?.PersistPrefs();
            LevelSceneGizmos.CurrentDefinition = null;
            if (CurrentWindow == this) CurrentWindow = null;
            CurrentDefinition = null;
        }

        void OnHierarchyChange()
        {
            if (_definition != null) RebuildViews();
        }

        void OnEditorUpdate()
        {
            if (_preview == null || !_preview.IsPlaying)
            {
                _lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double dt  = now - _lastEditorTime;
            _lastEditorTime = now;
            if (dt > 1.0) dt = 1.0;  // Editor 暂停/编译后,防止补帧

            _preview.Tick(dt);
            Repaint();
            SceneView.RepaintAll();
        }

        public void LoadDefinition(LevelDefinition def)
        {
            _definition = def;
            LevelSceneGizmos.CurrentDefinition = def;
            RebuildViews();
            Repaint();
        }

        void RebuildViews()
        {
            if (_definition == null) return;
            _ctx = new LevelEditorContext(_definition);
            _listView    = new LevelEntryListView(_ctx);
            _timelineView = new LevelTimelineView(_ctx, _preview, this);
            _detailView  = new LevelEntryDetailView(_ctx);

            int idx = LevelEditorPrefs.LastSelectedEntryIndex;
            if (_definition.Entries != null && idx >= 0 && idx < _definition.Entries.Length)
                _ctx.Selected = _definition.Entries[idx];

            CurrentDefinition = _definition;
            CurrentSelected   = _ctx?.Selected;
        }

        // ─── OnGUI ─────────────────────────────────────────────
        void OnGUI()
        {
            if (_definition == null) { DrawEmptyState(); return; }
            if (_ctx == null) RebuildViews();

            DrawToolbar();
            DrawMainArea();
            DrawStatusBar();

            CurrentSelected = _ctx.Selected;
        }

        void DrawEmptyState()
        {
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Level Editor", EditorStyles.largeLabel);
            GUILayout.Label("拖一份 LevelDefinition.asset 到此窗口,或:", EditorStyles.miniLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("选择 .asset…", GUILayout.Height(28)))
            {
                var path = EditorUtility.OpenFilePanel("Select LevelDefinition", "Assets", "asset");
                if (!string.IsNullOrEmpty(path))
                {
                    var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(ToRelativePath(path));
                    if (def != null) LoadDefinition(def);
                    else EditorUtility.DisplayDialog("Error", "Not a LevelDefinition asset", "OK");
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var path = EditorUtility.OpenFilePanel("Select LevelDefinition", "Assets", "asset");
                if (!string.IsNullOrEmpty(path))
                {
                    var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(ToRelativePath(path));
                    if (def != null) LoadDefinition(def);
                }
            }

            if (GUILayout.Button("+ Add ▾", EditorStyles.toolbarButton, GUILayout.Width(70)))
                ShowAddMenu();

            // 选中操作(无选中时 disabled,避免误操作)
            using (new EditorGUI.DisabledScope(_ctx?.Selected == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    ToolbarDuplicate();
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    ToolbarDelete();
            }

            GUILayout.Space(8);
            DrawPreviewControls();

            GUILayout.FlexibleSpace();
            GUILayout.Label(_definition.name, EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        void DrawPreviewControls()
        {
            var playing = _preview?.IsPlaying ?? false;
            var prev = GUI.color;
            if (playing) GUI.color = new Color(0.6f, 1f, 0.6f);

            string label = playing ? "■ Stop" : "▶ Play";
            if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (playing) _preview.Stop();
                else         _preview.Start(_definition);
                Repaint();
            }

            GUI.color = prev;

            if (GUILayout.Button("⏮ Reset", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                // 调 Stop() 而不是 SetTime(0f):
                //   - Stop() 已完整清理 _spawned(销毁 GO)+ _previewRoot + _sustained + _fired
                //   - SetTime(0f) 语义是"跳到时间轴 0 点并重新触发" —— 会立刻再喷一波单位,
                //     与"⏮ 重置"按钮的语义不符(用户期望画面清干净,等按 Play 再触发)。
                _preview?.Stop();
                Repaint();
            }

            float t = _preview?.CurrentTime ?? 0f;
            EditorGUI.BeginChangeCheck();
            float newT = EditorGUILayout.Slider(t, 0f, Mathf.Max(_definition.Duration, 60f),
                                                 GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                _preview?.SetTime(newT);
                Repaint();
            }
        }

        void ShowAddMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<SpawnEntry>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                var attr = (SRNameAttribute)Attribute.GetCustomAttribute(type, typeof(SRNameAttribute));
                string label = attr?.FullName ?? type.Name;
                menu.AddItem(new GUIContent(label), false, () => AddEntry(type));
            }
            menu.ShowAsContext();
        }

        void AddEntry(Type type)
        {
            Undo.RecordObject(_definition, "Add Spawn Entry");
            var entry = (SpawnEntry)Activator.CreateInstance(type);
            entry.TriggerTime = _definition.Entries != null && _definition.Entries.Length > 0
                ? _definition.Entries[^1].TriggerTime + 1f
                : 0f;

            var arr    = _definition.Entries ?? new SpawnEntry[0];
            var newArr = new SpawnEntry[arr.Length + 1];
            Array.Copy(arr, newArr, arr.Length);
            newArr[arr.Length] = entry;
            _definition.Entries = newArr;

            _ctx.Selected = entry;
            EditorUtility.SetDirty(_definition);
            RebuildViews();
            Repaint();
        }

        // ─── Toolbar 操作(走公共命令) ─────────────────────────
        void ToolbarDuplicate()
        {
            if (_ctx?.Selected == null) return;
            LevelEditorCommands.Duplicate(_ctx, _ctx.Selected);
            LevelEditorCommands.RefreshViews(this);
        }

        void ToolbarDelete()
        {
            if (_ctx?.Selected == null) return;
            if (LevelEditorCommands.Delete(_ctx))
                LevelEditorCommands.RefreshViews(this);
        }

        void DrawMainArea()
        {
            float listW     = 220f;
            float detailW   = LevelEditorPrefs.RightPanelWidth;
            float timelineW = Mathf.Max(200f, position.width - listW - detailW);

            float top    = EditorGUIUtility.singleLineHeight * 2 + 4;
            float bottom = position.height - top - 20;

            var listRect     = new Rect(0,                top, listW,     bottom);
            var timelineRect = new Rect(listW,            top, timelineW, bottom);
            var detailRect   = new Rect(listW + timelineW, top, detailW,   bottom);

            _listView.OnGUI(listRect);
            _timelineView.OnGUI(timelineRect);
            _detailView.OnGUI(detailRect);
        }

        void DrawStatusBar()
        {
            var rect = new Rect(0, position.height - 20, position.width, 20);
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            int count = _definition.Entries?.Length ?? 0;
            string sel = _ctx.Selected != null
                ? $"selected: #{_ctx.IndexOf(_ctx.Selected)} {_ctx.Selected.GetType().Name}"
                : "selected: none";
            GUI.Label(new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height),
                      $"{count} entries · duration {_definition.Duration:F0}s · {sel}",
                      LevelEditorStyles.RulerLabel);
        }

        static string ToRelativePath(string absolute)
        {
            int idx = absolute.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? absolute : absolute.Substring(idx).Replace('\\', '/');
        }
    }
}
