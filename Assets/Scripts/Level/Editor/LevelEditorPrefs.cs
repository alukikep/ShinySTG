using ShinySTG.Level;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor
{
    /// <summary>
    /// 关卡编辑器的持久化偏好(窗口几何 / 上次打开的 asset / 缩放等)。
    /// EditorPrefs key 加 "ShinySTG.LevelEditor." 前缀避免与其他插件冲突。
    /// </summary>
    internal static class LevelEditorPrefs
    {
        const string Prefix = "ShinySTG.LevelEditor.";

        // ─── 窗口几何 ───────────────────────────────────────────────
        public static float PixelsPerSecond
        {
            get => Mathf.Clamp(EditorPrefs.GetFloat(Prefix + "PxPerSec",
                                                    LevelEditorStyles.DefaultPixelsPerSecond),
                               LevelEditorStyles.PixelsPerSecondMin,
                               LevelEditorStyles.PixelsPerSecondMax);
            set => EditorPrefs.SetFloat(Prefix + "PxPerSec", value);
        }

        public static float ScrollX
        {
            get => EditorPrefs.GetFloat(Prefix + "ScrollX", 0f);
            set => EditorPrefs.SetFloat(Prefix + "ScrollX", value);
        }

        public static int LastSelectedEntryIndex
        {
            get => EditorPrefs.GetInt(Prefix + "SelectedIdx", -1);
            set => EditorPrefs.SetInt(Prefix + "SelectedIdx", value);
        }

        // ─── 右栏比例(详情面板宽度) ──────────────────────────────
        public static float RightPanelWidth
        {
            get => Mathf.Clamp(EditorPrefs.GetFloat(Prefix + "RightPanelW", 320f), 200f, 600f);
            set => EditorPrefs.SetFloat(Prefix + "RightPanelW", value);
        }
    }
}