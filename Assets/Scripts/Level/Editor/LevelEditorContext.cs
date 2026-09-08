using ShinySTG.Level;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor
{
    /// <summary>
    /// 关卡编辑器上下文:抽屉 / 视图之间共享的"非持久化数据"。
    /// 由 LevelEditorWindow 创建并注入到各视图 / 抽屉,避免每处单独持字段。
    /// </summary>
    public class LevelEditorContext
    {
        public LevelDefinition Definition { get; }
        public SerializedObject SerializedObject { get; }
        public SpawnEntry Selected { get; set; }

        public LevelEditorContext(LevelDefinition def)
        {
            Definition = def;
            SerializedObject = def != null ? new SerializedObject(def) : null;
        }

        public void Refresh()
        {
            SerializedObject?.Update();
        }

        public void Apply()
        {
            if (SerializedObject != null && SerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(SerializedObject.targetObject);
            }
        }

        public int IndexOf(SpawnEntry entry)
        {
            if (Definition?.Entries == null || entry == null) return -1;
            return System.Array.IndexOf(Definition.Entries, entry);
        }
    }
}