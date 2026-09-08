using ShinySTG.Level;
using UnityEngine;

namespace ShinySTG.Level.Editor.Views.Preview
{
    /// <summary>
    /// 时间轴上的 Preview 游标(竖线 + 时间标签)。
    /// 由 LevelTimelineView 调用 Draw() 绘制。
    /// </summary>
    internal static class LevelTimelineCursor
    {
        public static void Draw(Rect timelineRect, float currentTime, float pixelsPerSecond, float scrollX)
        {
            float x = currentTime * pixelsPerSecond - scrollX + timelineRect.x;
            if (x < timelineRect.x || x > timelineRect.xMax) return;

            // 竖线
            var prev = GUI.color;
            GUI.color = LevelEditorStyles.ColorCursor;
            GUI.DrawTexture(new Rect(x - 1f, timelineRect.y, 2f, timelineRect.height),
                            Texture2D.whiteTexture);
            GUI.color = prev;

            // 时间标签(顶部)
            var labelRect = new Rect(x + 4f, timelineRect.y, 50f, 18f);
            GUI.Label(labelRect, $"{currentTime:F2}s", LevelEditorStyles.RulerLabel);
        }
    }
}