using ShinySTG.Level;
using ShinySTG.Level.Editor;
using ShinySTG.Level.SpawnEntries;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Drawers
{
    /// <summary>
    /// 每种 SpawnEntry 在编辑器里的"画法"基类。
    /// 关键扩展点:加新画法 = 新建子类继承本类,放 Drawers/ 目录,自动被 Registry 发现。
    ///
    /// 对齐运行时约定:
    ///   - 运行时:加新 SpawnEntry 子类 = 自动在 Inspector 下拉里出现(走 [SRName])
    ///   - 编辑器:加新 Drawer 子类 = 自动接管该类型的画法(走反射)
    ///   - 默认实现 DefaultSpawnEntryDrawer 兜底所有未覆盖类型(本身 = 全部走基类默认)
    ///
    /// 实现约定:
    ///   - 重写 Handles 返回 true 时表示该抽屉负责此 entry 类型;Registry 按首次匹配原则分派。
    ///   - 重写 DrawTimelineBlock 可在 rect 区域内绘制背景色 + 标签;也可加图标 / VU表 / 动画预览等。
    ///   - 重写 DrawSceneGizmo 可改画箭头 / 范围圈 / 路径预览等。
    /// </summary>
    public abstract class ISpawnEntryDrawer
    {
        /// <summary>该抽屉是否处理这种 entry 类型(Registry 按这个分发)。</summary>
        public virtual bool Handles(SpawnEntry entry) => entry != null;

        /// <summary>时间轴 block 的标签(如 "Simple @ (0.0, 4.0)")。</summary>
        public virtual string GetLabel(SpawnEntry entry)
        {
            if (entry == null) return "Null";
            return $"{entry.GetType().Name} @ {entry.TriggerTime:F1}s";
        }

        /// <summary>时间轴 block 的颜色。默认按 LevelEditorStyles.GetTypeColor 走。</summary>
        public virtual Color GetColor(SpawnEntry entry) => LevelEditorStyles.GetTypeColor(entry);

        /// <summary>时间轴 block 绘制。ctx 可能为 null(抽屉单独使用时)。</summary>
        public virtual void DrawTimelineBlock(Rect rect, SpawnEntry entry, bool selected, LevelEditorContext ctx)
        {
            if (entry == null) return;

            float duration = GetDuration(entry);
            bool isSustained = duration > 0f;

            var prev = GUI.color;

            if (isSustained)
            {
                var solidColor = selected ? LevelEditorStyles.ColorSelected : GetColor(entry);
                var bodyColor = solidColor;
                bodyColor.a = selected ? 0.55f : 0.40f;

                GUI.color = bodyColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                var solidRect = new Rect(rect.x, rect.y,
                                         LevelEditorStyles.BlockSustainSolidWidth, rect.height);
                GUI.color = solidColor;
                GUI.DrawTexture(solidRect, Texture2D.whiteTexture);
                GUI.color = prev;

                DrawOutline(rect, selected ? 2f : 1f,
                            selected ? Color.yellow : new Color(0, 0, 0, 0.4f));

                var edgeRect = new Rect(rect.xMax - 2f, rect.y, 2f, rect.height);
                GUI.color = new Color(0, 0, 0, selected ? 0.6f : 0.25f);
                GUI.DrawTexture(edgeRect, Texture2D.whiteTexture);
                GUI.color = prev;

                if (rect.width > 30f)
                {
                    var labelRect = new Rect(
                        rect.x + LevelEditorStyles.BlockSustainSolidWidth + 4,
                        rect.y,
                        rect.width - LevelEditorStyles.BlockSustainSolidWidth - 8,
                        rect.height);
                    GUI.Label(labelRect, GetLabel(entry),
                              selected ? LevelEditorStyles.BlockSelected : LevelEditorStyles.BlockNormal);
                }
                return;
            }

            GUI.color = selected ? LevelEditorStyles.ColorSelected : GetColor(entry);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;

            DrawOutline(rect, selected ? 2f : 1f,
                        selected ? Color.yellow : new Color(0, 0, 0, 0.5f));

            if (rect.width > 30f)
            {
                var labelRect = new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height);
                GUI.Label(labelRect, GetLabel(entry),
                          selected ? LevelEditorStyles.BlockSelected : LevelEditorStyles.BlockNormal);
            }
        }

        /// <summary>Scene 视图 Gizmo 绘制。ctx 可能为 null。</summary>
        public virtual void DrawSceneGizmo(SpawnEntry entry, LevelEditorContext ctx)
        {
            if (entry == null) return;
            Vector3 pos = Vector3.zero;

            switch (entry)
            {
                case SimpleSpawnEntry s: pos = s.SpawnPosition; break;
                case WaveSpawnEntry w:   pos = w.CenterPosition; break;
                case BossSpawnEntry b:   pos = b.SpawnPosition; break;
                default: return;
            }

            var color = GetColor(entry);
            var prev = UnityEditor.Handles.color;
            UnityEditor.Handles.color = color;

            UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, 0.12f);
            UnityEditor.Handles.Label(pos + new Vector3(0.18f, 0.18f, 0f),
                          $"{entry.TriggerTime:F1}s · {entry.GetType().Name}");

            UnityEditor.Handles.color = prev;
        }

        /// <summary>列表视图每条 entry 的副标题(可空)。</summary>
        public virtual string GetSubLabel(SpawnEntry entry)
        {
            if (entry == null) return "";
            switch (entry)
            {
                case SimpleSpawnEntry s:
                    var sName = s.EnemyPrefab != null ? s.EnemyPrefab.name : "<no prefab>";
                    return $"{sName} @ {s.SpawnPosition}";
                case WaveSpawnEntry w:
                    int n = 0;
                    if (w.Prefabs != null)
                        for (int i = 0; i < w.Prefabs.Length; i++)
                            if (w.Prefabs[i] != null) n++;
                    return $"x{n} @ {w.CenterPosition}";
                case BossSpawnEntry b:
                    var bName = b.BossPrefab != null ? b.BossPrefab.name : "<no prefab>";
                    return $"{bName} @ {b.SpawnPosition}";
                default:
                    return GetLabel(entry);
            }
        }

        /// <summary>
        /// 时间轴 block 持续宽度(秒)。
        /// 返回 0 或负数 = 瞬时点 block(默认宽度,140px);
        /// 返回 >0 = 持续型 block(宽度 = value × 像素/秒)。
        /// </summary>
        public virtual float GetDuration(SpawnEntry entry) => entry != null ? entry.Duration : 0f;

        protected static void DrawOutline(Rect rect, float thickness, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
