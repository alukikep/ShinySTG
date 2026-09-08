using ShinySTG.Level;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Views
{
    /// <summary>
    /// 右侧详情面板:完全复用 SREditor(走 SerializedObject + PropertyField)。
    /// 0 行自定义 IMGUI 字段 —— SREditor 自动接管 [SerializeReference, SR] 下拉 + 折叠 + missing type 检测。
    /// </summary>
    public class LevelEntryDetailView
    {
        readonly LevelEditorContext _ctx;

        public LevelEntryDetailView(LevelEditorContext ctx) { _ctx = ctx; }

        public void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.20f, 0.20f, 0.20f));

            if (_ctx.Selected == null)
            {
                GUI.Label(rect, "选中条目以编辑", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // 1. 头部:类型名 + 索引
            var headerRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 22f);
            int idx = _ctx.IndexOf(_ctx.Selected);
            GUI.Label(headerRect,
                      $"#{idx}  {_ctx.Selected.GetType().Name}",
                      LevelEditorStyles.DetailTitle);

            // 2. SREditor 渲染(自动接管 SerializeReference)
            var entriesProp = _ctx.SerializedObject.FindProperty("Entries");
            if (entriesProp == null || idx < 0 || idx >= entriesProp.arraySize) return;

            _ctx.Refresh();
            var entryProp = entriesProp.GetArrayElementAtIndex(idx);

            var detailRect = new Rect(rect.x + 4, rect.y + 30, rect.width - 8, rect.height - 34);
            GUILayout.BeginArea(detailRect);
            EditorGUILayout.PropertyField(entryProp, true);
            _ctx.Apply();
            GUILayout.EndArea();
        }
    }
}