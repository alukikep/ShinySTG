using ShinySTG.Level;
using ShinySTG.Level.SpawnEntries;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Drawers
{
    /// <summary>
    /// PlaySfxSpawnEntry 的时间轴画法:青色标识,label 显示 cue 名 + 是否带位置。
    ///
    /// 对齐 ISpawnEntryDrawer 默认实现约定:
    ///   - Handles 必须 override,只接管 PlaySfxSpawnEntry 类型,其他类型走 DefaultSpawnEntryDrawer
    ///   - GetColor / GetLabel / GetSubLabel 可选 override(继承基类默认会回退到 LevelEditorStyles.GetTypeColor)
    ///   - DrawTimelineBlock 继承基类默认(瞬时点 block,140px 宽,色块 + 标签)
    ///   - DrawSceneGizmo override:仅在 UsePosition=true 时画位置标记,UsePosition=false 时不画(避免污染 Scene 视图)
    /// </summary>
    public class PlaySfxSpawnEntryDrawer : ISpawnEntryDrawer
    {
        public override bool Handles(SpawnEntry entry) => entry is PlaySfxSpawnEntry;

        public override string GetLabel(SpawnEntry entry)
        {
            var s = (PlaySfxSpawnEntry)entry;
            string cueName = s.Cue != null ? s.Cue.name : "<no cue>";
            return $"🔊 {cueName}";
        }

        public override string GetSubLabel(SpawnEntry entry)
        {
            var s = (PlaySfxSpawnEntry)entry;
            string cueName = s.Cue != null ? s.Cue.name : "SFX";
            string loc = s.UsePosition ? $"@ {s.Position}" : "(2D)";
            return $"🔊 {cueName}  {loc}";
        }

        public override void DrawSceneGizmo(SpawnEntry entry, LevelEditorContext ctx)
        {
            var s = (PlaySfxSpawnEntry)entry;
            if (s == null) return;

            // 2D 监听型 SFX 没有"位置"概念,不在 Scene 视图画任何东西,避免误导
            if (!s.UsePosition) return;

            // 用 Vector3 兼容 Handles 签名(UnityEditor.Handles.DrawSolidDisc 第一个参数是 Vector3)
            Vector3 pos = s.Position;
            var color = GetColor(entry);
            var prev = UnityEditor.Handles.color;
            UnityEditor.Handles.color = color;

            // 同 SimpleSpawnEntry 的画法:小圆 + 标签,加个外环暗示"声波扩散"
            UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, 0.12f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, 0.30f);
            UnityEditor.Handles.color = new Color(color.r, color.g, color.b, 0.5f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, 0.50f);

            UnityEditor.Handles.Label(pos + new Vector3(0.18f, 0.18f, 0f),
                          $"🔊 {s.Cue?.name ?? "<no cue>"} · {s.TriggerTime:F1}s");

            UnityEditor.Handles.color = prev;
        }
    }
}