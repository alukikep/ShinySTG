using System.Collections.Generic;
using ShinySTG.Level;
using ShinySTG.Level.Editor.Drawers;
using ShinySTG.Level.Editor.Views.Preview;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.Level.Editor.Views
{
    /// <summary>
    /// 时间轴主视图(单轨 + 堆叠 lane):
    ///   - 横向:时间(秒 → 像素)
    ///   - 纵向:重叠的 entry 自动堆叠到不同 lane(避免遮挡),单条 lane 内按 entry 时间排序
    ///
    /// Block 宽度策略:
    ///   - 瞬时点(Duration<=0):固定 BlockPointWidth (140px),即便很多也看得见文字
    ///   - 持续型(Duration>0):按 Duration × px/s 计算,自动堆叠避免重叠遮挡
    ///   - 最小宽度 BlockMinWidth (16px):保证再短的 entry 也有可见 + 可点击的命中区
    ///
    /// 交互:
    ///   - 拖动 block 中部 → 改 TriggerTime(走 Undo + SetDirty)
    ///   - 拖动 block 右边缘 (BlockEdgeGrabWidth = 6px) → 改 Duration
    ///   - Ctrl+滚轮 → 缩放(以鼠标位置为中心)
    ///   - 滚轮(无 Ctrl)→ 横向滚动
    ///   - 双击 block → Scene 视图聚焦
    ///   - 右键 block → 复制 / 删除 / 跳到
    ///   - Preview 期间显示游标(竖线 + 时间标签)
    /// </summary>
    public class LevelTimelineView
    {
        readonly LevelEditorContext _ctx;
        readonly ILevelEditorPreview _preview;
        readonly LevelEditorWindow _ownerWindow;  // 仅用于 Delete 后调 Repaint/SceneView.RepaintAll

        float _pxPerSec;
        float _scrollX;

        // 拖动状态(同时支持拖 TriggerTime 和拖 Duration,用 _dragKind 区分)
        enum DragKind { None, MoveTime, ResizeDuration }
        DragKind _dragKind = DragKind.None;
        float    _dragStartMouseX;
        float    _dragStartEntryTime;
        float    _dragStartEntryDuration;
        SpawnEntry _dragEntry;

        // 缓存:每帧算的 lane 分配(避免每次重算)
        readonly Dictionary<SpawnEntry, int> _laneOf = new();

        public LevelTimelineView(LevelEditorContext ctx, ILevelEditorPreview preview, LevelEditorWindow ownerWindow = null)
        {
            _ctx          = ctx;
            _preview      = preview;
            _ownerWindow  = ownerWindow;
            _pxPerSec     = LevelEditorPrefs.PixelsPerSecond;
            _scrollX      = LevelEditorPrefs.ScrollX;
        }

        public void PersistPrefs()
        {
            LevelEditorPrefs.PixelsPerSecond = _pxPerSec;
            LevelEditorPrefs.ScrollX         = _scrollX;
        }

        public void OnGUI(Rect rect)
        {
            // 1. 背景
            EditorGUI.DrawRect(rect, LevelEditorStyles.ColorTimelineBg);

            // 2. 标尺
            var rulerRect = new Rect(rect.x, rect.y, rect.width, LevelEditorStyles.RulerHeight);
            DrawRuler(rulerRect);

            // 3. 时间轴主体
            var trackRect = new Rect(rect.x, rect.y + LevelEditorStyles.RulerHeight,
                                     rect.width, rect.height - LevelEditorStyles.RulerHeight);
            DrawTrack(trackRect);

            // 4. Preview 游标(只在 IsPlaying 或 CurrentTime>0 时画)
            if (_preview != null && (_preview.IsPlaying || _preview.CurrentTime > 0f))
                LevelTimelineCursor.Draw(trackRect, _preview.CurrentTime, _pxPerSec, _scrollX);

            // 5. 事件
            HandleEvents(rulerRect, trackRect);
        }

        void DrawRuler(Rect rect)
        {
            EditorGUI.DrawRect(rect, LevelEditorStyles.ColorRuler);

            float[] steps = { 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };
            float step = 60f;
            foreach (var s in steps)
                if (s * _pxPerSec >= 60f) { step = s; break; }

            float firstTick = Mathf.Ceil((_scrollX / _pxPerSec) / step) * step;
            for (float t = firstTick; ; t += step)
            {
                float x = t * _pxPerSec - _scrollX + rect.x;
                if (x > rect.xMax) break;
                GUI.Label(new Rect(x + 2, rect.y + 2, 50, 18), $"{t:F1}s", LevelEditorStyles.RulerLabel);
            }
        }

        /// <summary>
        /// block 命中信息:Hover/Hit 时返回 entry + 命中区域(中部 / 右边缘)。
        /// HitArea.RightEdge = true 表示鼠标在右 6px 边缘上 → 应该拖动 Duration。
        /// </summary>
        struct BlockHit
        {
            public SpawnEntry Entry;
            public bool IsRightEdge;
        }

        /// <summary>
        /// 给定 entry 算出它在屏幕上的 Rect,同时按 trackRect 视口裁剪
        ///(不裁掉滚出左侧但还有部分可见的 block)。
        /// 返回的 Rect.IsValid(用 false 检测)表示完全滚出视口 → 应跳过绘制 / 命中测试。
        /// </summary>
        bool ComputeBlockRect(Rect trackRect, SpawnEntry entry, int lane, out Rect rect)
        {
            rect = default;
            var drawer = LevelEditorDrawerRegistry.Resolve(entry);
            if (drawer == null) return false;

            float duration = drawer.GetDuration(entry);
            bool isSustained = duration > 0f;

            // 1. 算出"无裁剪"的 block 屏幕位置 + 宽度
            float rawX = entry.TriggerTime * _pxPerSec - _scrollX + trackRect.x;
            float w;
            if (isSustained)
            {
                w = Mathf.Max(LevelEditorStyles.BlockMinWidth, duration * _pxPerSec);
            }
            else
            {
                w = LevelEditorStyles.BlockPointWidth;
            }

            // 2. 视口裁剪:
            //    - 完全在视口右侧外 → 不绘制
            //    - 完全在视口左侧外 → 不绘制
            //    - 部分滚出(任一侧) → 裁到视口边,保留可见部分
            float right = rawX + w;
            if (right <= trackRect.x) return false;   // 整体在左侧外
            if (rawX >= trackRect.xMax) return false;  // 整体在右侧外

            float x = Mathf.Max(trackRect.x, rawX);
            float xMax = Mathf.Min(trackRect.xMax, right);
            float visW = xMax - x;
            if (visW < LevelEditorStyles.BlockMinWidth) return false;  // 可见部分 < 最小宽度 → 跳过

            float laneY = trackRect.y + LevelEditorStyles.TrackTopPadding +
                          lane * (LevelEditorStyles.BlockHeight + LevelEditorStyles.TrackLaneGap);

            rect = new Rect(x, laneY, visW, LevelEditorStyles.BlockHeight);
            return true;
        }

        void DrawTrack(Rect trackRect)
        {
            var entries = _ctx.Definition?.Entries;
            if (entries == null) return;

            // 1. 算每个 entry 的矩形 + 宽度(预先算好,后面 draw / hit test 复用)
            _laneOf.Clear();
            AssignLanes(entries);  // 给重叠的 entry 分配不同 lane

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;

                int lane = _laneOf.TryGetValue(entry, out var l) ? l : 0;
                if (!ComputeBlockRect(trackRect, entry, lane, out var blockRect)) continue;

                var drawer = LevelEditorDrawerRegistry.Resolve(entry);
                drawer.DrawTimelineBlock(blockRect, entry, entry == _ctx.Selected, _ctx);
            }
        }

        /// <summary>
        /// 给重叠的 entry 分配 lane(贪心):
        ///   - 第一个 entry 占 lane 0
        ///   - 后续 entry 找"最左边位置 ≤ 当前 entry.TriggerTime"的 lane(允许新开 lane)
        ///   - 不重叠的 entry 共享 lane 0(节省纵向空间)
        /// </summary>
        void AssignLanes(SpawnEntry[] entries)
        {
            // 每个 lane 当前的"最后结束时间"(瞬时点 = TriggerTime)
            var laneEndTime = new List<float>();
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                float entryEnd = entry.TriggerTime + Mathf.Max(0f, entry.Duration);

                int assigned = -1;
                for (int i = 0; i < laneEndTime.Count; i++)
                {
                    if (laneEndTime[i] <= entry.TriggerTime - 0.001f)
                    {
                        assigned = i;
                        laneEndTime[i] = entryEnd;
                        break;
                    }
                }
                if (assigned < 0)
                {
                    assigned = laneEndTime.Count;
                    laneEndTime.Add(entryEnd);
                }
                _laneOf[entry] = assigned;
            }
        }

        BlockHit HitTestBlock(Rect trackRect, Vector2 mouse)
        {
            var entries = _ctx.Definition?.Entries;
            if (entries == null) return default;

            // HitTestBlock 可能独立于 DrawTrack 被调用(鼠标 hover 时),所以这里也确保 lane 分配就绪
            if (_laneOf.Count == 0) AssignLanes(entries);

            // 倒序:后画的(更上面的 lane / 更高索引)优先命中
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry == null) continue;

                int lane = _laneOf.TryGetValue(entry, out var l) ? l : 0;
                if (!ComputeBlockRect(trackRect, entry, lane, out var rect)) continue;  // 视口外,不参与命中
                if (!rect.Contains(mouse)) continue;

                // 在边缘 6px 内 → 改 Duration;否则改 TriggerTime
                // 注:rect 已被裁剪过,可能右侧就贴在 trackRect.xMax,此时仍允许拖右边缘改 Duration
                var drawer = LevelEditorDrawerRegistry.Resolve(entry);
                bool isSustained = drawer.GetDuration(entry) > 0f;
                bool isEdge = isSustained &&
                               mouse.x >= rect.xMax - LevelEditorStyles.BlockEdgeGrabWidth &&
                               mouse.x <= rect.xMax;

                return new BlockHit { Entry = entry, IsRightEdge = isEdge };
            }
            return default;
        }

        void HandleEvents(Rect rulerRect, Rect trackRect)
        {
            var e = Event.current;
            int controlId = GUIUtility.GetControlID("LevelTimeline".GetHashCode(), FocusType.Passive);

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (trackRect.Contains(e.mousePosition))
                    {
                        var hit = HitTestBlock(trackRect, e.mousePosition);
                        if (hit.Entry != null)
                        {
                            _ctx.Selected = hit.Entry;
                            _dragEntry    = hit.Entry;
                            _dragStartMouseX = e.mousePosition.x;
                            _dragStartEntryTime     = hit.Entry.TriggerTime;
                            _dragStartEntryDuration = hit.Entry.Duration;

                            _dragKind = hit.IsRightEdge ? DragKind.ResizeDuration : DragKind.MoveTime;
                            GUIUtility.hotControl = controlId;
                        }
                        else
                        {
                            _ctx.Selected = null;
                        }
                        e.Use();
                    }
                    else if (rulerRect.Contains(e.mousePosition))
                    {
                        float t = (e.mousePosition.x + _scrollX - rulerRect.x) / _pxPerSec;
                        _scrollX = Mathf.Max(0, t * _pxPerSec - rulerRect.width * 0.1f);
                        GUI.changed = true;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_dragKind != DragKind.None && GUIUtility.hotControl == controlId && _dragEntry != null)
                    {
                        float dx = e.mousePosition.x - _dragStartMouseX;
                        float dt = dx / _pxPerSec;

                        if (_dragKind == DragKind.MoveTime)
                        {
                            Undo.RecordObject(_ctx.Definition, "Move Spawn Entry");
                            _dragEntry.TriggerTime = Mathf.Max(0f, _dragStartEntryTime + dt);
                        }
                        else if (_dragKind == DragKind.ResizeDuration)
                        {
                            Undo.RecordObject(_ctx.Definition, "Resize Spawn Entry Duration");
                            float newDuration = Mathf.Max(0f, _dragStartEntryDuration + dt);
                            _dragEntry.Duration = newDuration;
                        }
                        EditorUtility.SetDirty(_ctx.Definition);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_dragKind != DragKind.None && GUIUtility.hotControl == controlId)
                    {
                        _dragKind  = DragKind.None;
                        _dragEntry = null;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;

                case EventType.MouseMove:
                    // Hover 在 block 边缘时改鼠标样式(提示可拖)
                    if (trackRect.Contains(e.mousePosition))
                    {
                        var hit = HitTestBlock(trackRect, e.mousePosition);
                        if (hit.IsRightEdge)
                            EditorGUIUtility.AddCursorRect(trackRect, MouseCursor.ResizeHorizontal);
                        else if (hit.Entry != null)
                            EditorGUIUtility.AddCursorRect(trackRect, MouseCursor.Link);
                    }
                    break;

                case EventType.ScrollWheel:
                    if (trackRect.Contains(e.mousePosition) || rulerRect.Contains(e.mousePosition))
                    {
                        if (e.control)
                        {
                            float oldPx = _pxPerSec;
                            _pxPerSec = Mathf.Clamp(_pxPerSec * (1f + e.delta.y * 0.05f),
                                                    LevelEditorStyles.PixelsPerSecondMin,
                                                    LevelEditorStyles.PixelsPerSecondMax);
                            float mouseTime = (e.mousePosition.x + _scrollX - rulerRect.x) / oldPx;
                            _scrollX = mouseTime * _pxPerSec - (e.mousePosition.x - rulerRect.x);
                            _scrollX = Mathf.Max(0, _scrollX);
                        }
                        else
                        {
                            _scrollX = Mathf.Max(0, _scrollX - e.delta.y * 20f);
                        }
                        e.Use();
                    }
                    break;

                case EventType.ContextClick:
                    if (trackRect.Contains(e.mousePosition))
                    {
                        var hit = HitTestBlock(trackRect, e.mousePosition);
                        if (hit.Entry != null)
                        {
                            _ctx.Selected = hit.Entry;
                            ShowContextMenu(hit.Entry);
                            e.Use();
                        }
                    }
                    break;

                case EventType.KeyDown:
                    // Delete / Backspace → 删除当前选中(避免焦点在输入框时误删)
                    if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                        && GUIUtility.keyboardControl == 0
                        && GUIUtility.hotControl == 0)
                    {
                        if (HandleDeleteShortcut())
                            e.Use();
                    }
                    break;
            }
        }

        void ShowContextMenu(SpawnEntry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Focus in Scene"), false, () => LevelEditorCommands.FocusSceneView(entry));
            menu.AddItem(new GUIContent("Duplicate"), false, () => OnDuplicate(entry));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => OnDelete(entry));
            menu.ShowAsContext();
        }

        // 选中 → Delete / Backspace 按键删除:走公共命令后请求 window 重画
        bool HandleDeleteShortcut()
        {
            if (_ctx?.Selected == null) return false;
            if (LevelEditorCommands.Delete(_ctx))
            {
                LevelEditorCommands.RefreshViews(_ownerWindow);
                return true;
            }
            return false;
        }

        void OnDuplicate(SpawnEntry entry)
        {
            LevelEditorCommands.Duplicate(_ctx, entry);
            LevelEditorCommands.RefreshViews(_ownerWindow);
        }

        void OnDelete(SpawnEntry entry)
        {
            if (LevelEditorCommands.Delete(_ctx, entry))
                LevelEditorCommands.RefreshViews(_ownerWindow);
        }
    }
}
