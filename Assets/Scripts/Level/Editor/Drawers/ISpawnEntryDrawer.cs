using ShinySTG.Level;
using ShinySTG.Level.Editor;
using ShinySTG.Level.SpawnEntries;
using ShinySTG.Level.SpawnEntries.PositionStrategies;
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
            // 仅 Sustain 用:非零 = 需要画"偏移轨迹"短线 + 终点小圆。
            // 放到 switch 之外共享绘制区,避免内嵌 {} 块与下方 prev 重名(CS0136)。
            Vector3 offsetDir = Vector3.zero;

            switch (entry)
            {
                case SimpleSpawnEntry s: pos = s.SpawnPosition; break;
                case WaveSpawnEntry w:   pos = w.CenterPosition; break;
                case BossSpawnEntry b:   pos = b.SpawnPosition; break;
                case SustainSpawnEntry st:
                    pos = st.SpawnPosition;
                    // 偏移轨迹 / 范围框由 strategy 类型决定:
                    //   - FixedSpawnPositionStrategy → 朝 Offset 方向的短线 + 终点小圆
                    //   - RandomSpawnPositionStrategy → 以 SpawnPosition 为中心的方框
                    //   - 其它 / null → 不画额外 gizmo
                    if (st.SpawnPositionStrategy is FixedSpawnPositionStrategy fixedStrat
                        && fixedStrat.Offset != Vector2.zero)
                    {
                        offsetDir = new Vector3(fixedStrat.Offset.x, fixedStrat.Offset.y, 0f);
                    }
                    break;
                default: return;
            }

            var color = GetColor(entry);
            var prev = UnityEditor.Handles.color;
            UnityEditor.Handles.color = color;

            UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, 0.12f);
            UnityEditor.Handles.Label(pos + new Vector3(0.18f, 0.18f, 0f),
                          $"{entry.TriggerTime:F1}s · {entry.GetType().Name}");

            // Sustain Fixed 策略:偏移方向短线 + 终点小圆。长度上限 2 单位,避免大数值飞太远。
            if (offsetDir != Vector3.zero)
            {
                var dirNorm = offsetDir.normalized;
                var drawTo = pos + dirNorm * Mathf.Min(offsetDir.magnitude, 2f);
                UnityEditor.Handles.DrawAAPolyLine(2f, pos, drawTo);
                UnityEditor.Handles.DrawSolidDisc(drawTo, Vector3.forward, 0.06f);
            }

            // Sustain Random 策略:画一个 4 边的方框表示随机范围(SpawnPosition ± Range)。
            if (entry is SustainSpawnEntry sust
                && sust.SpawnPositionStrategy is RandomSpawnPositionStrategy randStrat
                && randStrat.Range != Vector2.zero)
            {
                var r = randStrat.Range;
                Vector3 c = sust.SpawnPosition;
                Vector3 p0 = new Vector3(c.x - r.x, c.y - r.y, c.z);
                Vector3 p1 = new Vector3(c.x + r.x, c.y - r.y, c.z);
                Vector3 p2 = new Vector3(c.x + r.x, c.y + r.y, c.z);
                Vector3 p3 = new Vector3(c.x - r.x, c.y + r.y, c.z);
                UnityEditor.Handles.DrawAAPolyLine(2f, p0, p1, p2, p3, p0);
                // 4 个角各画一个小圆点,直观看到"每只敌人会落在框内任意位置"
                UnityEditor.Handles.DrawSolidDisc(p0, Vector3.forward, 0.04f);
                UnityEditor.Handles.DrawSolidDisc(p1, Vector3.forward, 0.04f);
                UnityEditor.Handles.DrawSolidDisc(p2, Vector3.forward, 0.04f);
                UnityEditor.Handles.DrawSolidDisc(p3, Vector3.forward, 0.04f);
            }

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
                case SustainSpawnEntry st:
                    var sName2 = st.EnemyPrefab != null ? st.EnemyPrefab.name : "<no prefab>";
                    string stratHint = st.SpawnPositionStrategy switch
                    {
                        FixedSpawnPositionStrategy f when f.Offset != Vector2.zero
                            => $" ·Fixed Δ{f.Offset}",
                        RandomSpawnPositionStrategy r when r.Range != Vector2.zero
                            => $" ·Random ±{r.Range}",
                        _ => ""
                    };
                    return $"{sName2} @ {st.SpawnPosition}{stratHint}";
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
