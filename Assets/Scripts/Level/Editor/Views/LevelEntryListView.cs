using ShinySTG.Level;
using ShinySTG.Level.Editor.Drawers;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Views
{
    /// <summary>
    /// 左侧条目列表视图:同步显示时间轴上的所有 entry,点击 = 选中。
    /// 主窗口分配固定宽度,从 ctx.Definition 读数据。
    /// </summary>
    public class LevelEntryListView
    {
        readonly LevelEditorContext _ctx;

        public LevelEntryListView(LevelEditorContext ctx) { _ctx = ctx; }

        public void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f));

            // 头部
            var headerRect = new Rect(rect.x, rect.y, rect.width, 20f);
            GUI.Label(headerRect, $"Entries ({_ctx.Definition?.Entries?.Length ?? 0})", EditorStyles.boldLabel);

            // 列表
            var listRect = new Rect(rect.x, rect.y + 22f, rect.width, rect.height - 22f);
            GUILayout.BeginArea(listRect);
            DrawList();
            GUILayout.EndArea();
        }

        void DrawList()
        {
            if (_ctx.Definition == null) return;
            var entries = _ctx.Definition.Entries;
            if (entries == null || entries.Length == 0)
            {
                GUILayout.Label("(空)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // 每行右键 → 弹同样的菜单(让列表也支持 Delete / Duplicate)
            // 整行在 BeginArea 坐标系里是绝对像素,但 mousePosition 在 EditorWindow 全局坐标,
            // 需要把 listRect 也记下来 + 转换。直接在每行 rowRect 上判命中更简单。
            var e = Event.current;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    using (new GUILayout.HorizontalScope(GUILayout.Height(22f)))
                    {
                        var nullRowRect = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
                        GUI.Label(new Rect(nullRowRect.x + 18f, nullRowRect.y, nullRowRect.width - 18f, nullRowRect.height),
                                  "(空槽)", EditorStyles.centeredGreyMiniLabel);
                    }
                    continue;
                }

                var drawer = LevelEditorDrawerRegistry.Resolve(entry);
                if (drawer == null) continue;
                bool selected = entry == _ctx.Selected;

                Rect rowRect;
                using (new GUILayout.HorizontalScope(GUILayout.Height(22f)))
                {
                    rowRect = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));

                    if (selected)
                    {
                        EditorGUI.DrawRect(rowRect, new Color(0.35f, 0.55f, 0.85f, 0.35f));
                        EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 2f, rowRect.height),
                                           new Color(0.45f, 0.70f, 1f));
                    }

                    if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                        Select(entry);

                    var dotRect = new Rect(rowRect.x + 4f, rowRect.y + 6f, 8f, 8f);
                    var prev = GUI.color;
                    GUI.color = drawer.GetColor(entry);
                    GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                    GUI.color = prev;

                    var labelRect = new Rect(rowRect.x + 18f, rowRect.y, rowRect.width - 18f, rowRect.height);
                    var labelStyle = selected ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                    string durHint = entry.HasDuration ? $"  [{entry.Duration:F1}s]" : "";
                    GUI.Label(labelRect,
                              $"{entry.TriggerTime:F1}s{durHint}  {drawer.GetSubLabel(entry)}",
                              labelStyle);
                }

                // 右键命中本行 → 选中 + 弹菜单(与时间轴一致)
                if (e.type == EventType.ContextClick && rowRect.Contains(e.mousePosition))
                {
                    _ctx.Selected = entry;
                    ShowContextMenu(entry);
                    e.Use();
                }
            }

            // Delete / Backspace 键:避免焦点在 TextField 里误删(其它控件也会设 keyboardControl > 0)
            if (e.type == EventType.KeyDown
                && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                && GUIUtility.keyboardControl == 0
                && GUIUtility.hotControl == 0
                && _ctx?.Selected != null)
            {
                if (LevelEditorCommands.Delete(_ctx))
                {
                    LevelEditorCommands.RefreshViews(LevelEditorWindow.CurrentWindow);
                    e.Use();
                }
            }
        }

        void Select(SpawnEntry entry)
        {
            _ctx.Selected = entry;
            LevelEditorPrefs.LastSelectedEntryIndex = _ctx.IndexOf(entry);
            GUI.FocusControl(null);
        }

        void ShowContextMenu(SpawnEntry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Focus in Scene"), false, () =>
                LevelEditorCommands.FocusSceneView(entry));
            menu.AddItem(new GUIContent("Duplicate"), false, () => OnDuplicate(entry));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => OnDelete(entry));
            menu.ShowAsContext();
        }

        void OnDuplicate(SpawnEntry entry)
        {
            LevelEditorCommands.Duplicate(_ctx, entry);
            LevelEditorCommands.RefreshViews(LevelEditorWindow.CurrentWindow);
        }

        void OnDelete(SpawnEntry entry)
        {
            if (LevelEditorCommands.Delete(_ctx, entry))
                LevelEditorCommands.RefreshViews(LevelEditorWindow.CurrentWindow);
        }
    }
}
