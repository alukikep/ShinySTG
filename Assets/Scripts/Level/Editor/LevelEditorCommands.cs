using System;
using ShinySTG.Level;
using ShinySTG.Level.SpawnEntries;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor
{
    /// <summary>
    /// 关卡编辑器共享命令:把"对选中 entry 的操作"集中在这里,
    /// 让时间轴 / 列表 / Toolbar 都走同一份逻辑,避免行为发散。
    ///
    /// 所有写操作都走标准编辑器流程:
    ///   - Undo.RecordObject(进 Undo 栈,Ctrl+Z 可撤销)
    ///   - EditorUtility.SetDirty(标记 dirty,下次保存会写盘)
    ///   - RebuildViews + Repaint(让视图重画 + 通知 EditorWindow 重画)
    ///
    /// 公共入口:
    ///   - FocusSceneView(entry)     滚到 Scene 视图对应位置
    ///   - Duplicate(ctx, entry)     复制 entry(瞬时点 +0.5s / 持续 +Duration),插到原 entry 之后
    ///   - Delete(ctx, window)       删除当前选中 entry(走 ctx.Selected,无参版本)
    ///                                 + 把 LastSelectedEntryIndex 清掉,避免下次开窗选中错位
    /// </summary>
    public static class LevelEditorCommands
    {
        /// <summary>Scene 视图聚焦到该 entry 的 SpawnPosition / CenterPosition。</summary>
        public static void FocusSceneView(SpawnEntry entry)
        {
            if (entry == null) return;
            Vector3 pos = Vector3.zero;
            switch (entry)
            {
                case SimpleSpawnEntry s: pos = s.SpawnPosition; break;
                case WaveSpawnEntry   w: pos = w.CenterPosition; break;
                case BossSpawnEntry   b: pos = b.SpawnPosition; break;
            }
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(pos, Quaternion.identity, 3f, false);
                SceneView.RepaintAll();
            }
        }

        /// <summary>复制 entry:浅 clone(JSON)+ 偏移 TriggerTime,插到原 entry 之后。</summary>
        public static void Duplicate(LevelEditorContext ctx, SpawnEntry entry)
        {
            if (ctx?.Definition == null || entry == null) return;
            int idx = ctx.IndexOf(entry);
            if (idx < 0) return;

            Undo.RecordObject(ctx.Definition, "Duplicate Spawn Entry");

            var json  = JsonUtility.ToJson(entry);
            var clone = (SpawnEntry)JsonUtility.FromJson(json, entry.GetType());
            // 瞬时点偏移 0.5s;持续型偏移 Duration(避免和原 entry 撞在同一时间点导致完全重叠)
            float offset = entry.Duration > 0f ? entry.Duration : 0.5f;
            clone.TriggerTime = entry.TriggerTime + offset;

            var arr    = ctx.Definition.Entries;
            var newArr = new SpawnEntry[arr.Length + 1];
            // 0..idx 复制原样,idx+1 放 clone,idx+1..end 后移一位
            Array.Copy(arr, 0, newArr, 0, idx + 1);
            newArr[idx + 1] = clone;
            Array.Copy(arr, idx + 1, newArr, idx + 2, arr.Length - idx - 1);
            ctx.Definition.Entries = newArr;

            ctx.Selected = clone;
            LevelEditorPrefs.LastSelectedEntryIndex = ctx.IndexOf(clone);

            EditorUtility.SetDirty(ctx.Definition);
        }

        /// <summary>
        /// 删除当前选中 entry;无选中则 no-op。
        /// 返回 true 表示实际发生了删除(用于快捷键判断"是否消费了事件")。
        /// </summary>
        public static bool Delete(LevelEditorContext ctx)
        {
            if (ctx == null) return false;
            return Delete(ctx, ctx.Selected);
        }

        /// <summary>删除指定 entry。返回 true 表示实际删除。</summary>
        public static bool Delete(LevelEditorContext ctx, SpawnEntry entry)
        {
            if (ctx?.Definition == null || entry == null) return false;
            int idx = ctx.IndexOf(entry);
            if (idx < 0) return false;

            Undo.RecordObject(ctx.Definition, "Delete Spawn Entry");

            var arr    = ctx.Definition.Entries;
            var newArr = new SpawnEntry[arr.Length - 1];
            // 跳过 idx,把后面的往前挪一位
            Array.Copy(arr, 0, newArr, 0, idx);
            Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
            ctx.Definition.Entries = newArr;

            ctx.Selected = null;
            // 清掉"上次选中索引" —— 否则下次开窗还按这个 idx 选中,但数组已变,
            // 会指向别的 entry 或越界(取决于新数组长度)
            LevelEditorPrefs.LastSelectedEntryIndex = -1;

            EditorUtility.SetDirty(ctx.Definition);
            return true;
        }

        /// <summary>
        /// 便利方法:写操作完成后让窗口视图重画。
        /// 由调用方(Toolbar / 时间轴 / 列表)在调完 Duplicate/Delete 后触发。
        /// 之所以不放进 Duplicate/Delete 内部 —— 避免形成 ctx→window→ctx 的循环依赖。
        /// </summary>
        public static void RefreshViews(LevelEditorWindow window)
        {
            if (window == null) return;
            window.Repaint();
            SceneView.RepaintAll();
        }
    }
}
