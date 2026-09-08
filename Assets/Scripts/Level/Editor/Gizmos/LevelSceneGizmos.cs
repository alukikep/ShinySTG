using ShinySTG.Level;
using ShinySTG.Level.Editor.Drawers;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Gizmos
{
    /// <summary>
    /// Scene 视图辅助绘制:把当前 LevelEditorWindow 编辑的关卡,所有 entry 的 SpawnPosition 画到 Scene 里。
    /// 选中 entry 时高亮(画 X + 大圆),未选中画小圆。
    /// </summary>
    [InitializeOnLoad]
    internal static class LevelSceneGizmos
    {
        static LevelSceneGizmos()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static LevelDefinition CurrentDefinition;

        static void OnSceneGUI(SceneView sceneView)
        {
            var def = CurrentDefinition;
            if (def?.Entries == null) return;

            var selected = LevelEditorWindow.CurrentSelected;

            for (int i = 0; i < def.Entries.Length; i++)
            {
                var entry = def.Entries[i];
                if (entry == null) continue;

                var drawer = LevelEditorDrawerRegistry.Resolve(entry);
                bool isSelected = entry == selected;
                Vector3 pos = ResolvePos(entry);

                var prev = UnityEditor.Handles.color;
                UnityEditor.Handles.color = isSelected ? Color.yellow : drawer.GetColor(entry);

                if (isSelected)
                {
                    UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, 0.3f);
                    UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, 0.5f);
                }
                else
                {
                    UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, 0.12f);
                }

                UnityEditor.Handles.Label(pos + new Vector3(0.2f, 0.2f, 0f),
                              $"{entry.TriggerTime:F1}s · {entry.GetType().Name}");
                UnityEditor.Handles.color = prev;
            }
        }

        static Vector3 ResolvePos(SpawnEntry entry)
        {
            switch (entry)
            {
                case SpawnEntries.SimpleSpawnEntry s: return s.SpawnPosition;
                case SpawnEntries.WaveSpawnEntry w:   return w.CenterPosition;
                case SpawnEntries.BossSpawnEntry b:   return b.SpawnPosition;
                default:                              return Vector3.zero;
            }
        }
    }
}