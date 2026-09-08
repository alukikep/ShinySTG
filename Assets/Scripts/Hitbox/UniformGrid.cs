using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Hitbox
{
    /// <summary>
    /// 2D 均匀网格空间索引。直接存 HitboxComponent 引用,无反查。
    ///
    /// 用法(CollisionService 内部使用,追踪 modifier 也可读):
    ///   var grid = new UniformGrid(cellSize: 4f, worldMin: (-12,-22), worldMax: (12,22));
    ///   grid.Clear();
    ///   foreach (var hb in hitboxes) grid.Insert(hb);  // 用 hb._cachedBounds.center 算 cell
    ///   var hits = grid.Query3x3(point);               // 中心点 3×3 范围内的 hitbox(去重)
    ///
    /// 重要约定(单 cell 插入):
    ///   - STG 所有 hitbox 尺寸(玩家 0.1 / 敌人 0.5 / 子弹 0.08)都远小于 CellSize(默认 4),
    ///     单 cell 插入不会跨 cell 漏查。
    ///   - Insert 用 hitbox 中心点定位一个 cell;Query3x3 覆盖 9 个 cell,3×3 范围内必然包含中心点所在 cell
    ///     及 8 邻 cell,跨 cell 边界物体也能查到。
    ///   - 本类存 HitboxComponent 引用(不是 InstanceID),直接读 .Team / ._cachedBounds,零反查。
    ///
    /// 适用场景:活跃子弹 > ~500 颗,或敌人 > ~50 只。
    /// 弹量小时也建议走网格(实现简单,常数开销可忽略)。
    ///
    /// 注意:本类非线程安全;只用于 LateUpdate 单线程。
    /// </summary>
    public class UniformGrid
    {
        readonly float _cellSize;
        readonly int _cols;
        readonly int _rows;
        readonly float _originX;
        readonly float _originY;
        readonly Dictionary<int, List<HitboxComponent>> _cells = new();

        // 复用缓冲,避免每次 Query 时 new List
        readonly List<HitboxComponent> _queryResult = new();

        public UniformGrid(float cellSize, Vector2 worldMin, Vector2 worldMax)
        {
            _cellSize = Mathf.Max(0.01f, cellSize);
            _originX = worldMin.x;
            _originY = worldMin.y;
            _cols = Mathf.Max(1, Mathf.CeilToInt((worldMax.x - worldMin.x) / _cellSize));
            _rows = Mathf.Max(1, Mathf.CeilToInt((worldMax.y - worldMin.y) / _cellSize));
        }

        public void Clear()
        {
            // 把每个 cell 的 list 清空但保留 List 实例,减少 GC
            foreach (var kv in _cells)
                kv.Value.Clear();
        }

        /// <summary>
        /// 把 hitbox 插入其中心点所在 cell。
        /// 调用前应已 RefreshCachedBounds()(由 CollisionService.LateUpdate 统一负责)。
        /// hitbox 为 null 时直接跳过(Unity 伪 null 也算 null)。
        /// </summary>
        public void Insert(HitboxComponent hb)
        {
            if (hb == null) return;
            Vector2 center = hb._cachedBounds.center;
            int x = WorldToCellX(center.x);
            int y = WorldToCellY(center.y);
            int key = CellKey(x, y);
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<HitboxComponent>(8);
                _cells[key] = list;
            }
            list.Add(hb);
        }

        /// <summary>
        /// 把中心点所在 cell + 8 邻 cell 内的所有 hitbox 写入 _queryResult(复用缓冲)。
        /// 返回的 List 不可长期持有 —— 下次 Query 会清空。
        /// </summary>
        public List<HitboxComponent> Query3x3(Vector2 center)
            => QueryRadius(center, _cellSize);

        /// <summary>
        /// 把中心点所在 cell + 外扩 r 圈覆盖的所有 cell 内的 hitbox 写入 _queryResult(复用缓冲)。
        /// 用途:追踪 modifier 一次性拿到半径 r 内的所有候选,再按距离过滤选最近。
        /// 单 cell 插入保证同一 hitbox 只出现一次,无需 HashSet 去重。
        /// 返回的 List 不可长期持有 —— 下次 Query 会清空。
        /// </summary>
        public List<HitboxComponent> QueryRadius(Vector2 center, float radius)
        {
            _queryResult.Clear();

            // 圈数 = ceil(radius / cellSize);extent = 圈数(0=仅中心 cell,1=3×3,2=5×5,...)
            int rings = Mathf.CeilToInt(radius / _cellSize);
            int extent = rings;
            int cx = WorldToCellX(center.x);
            int cy = WorldToCellY(center.y);

            int xMin = Mathf.Max(0, cx - extent);
            int xMax = Mathf.Min(_cols - 1, cx + extent);
            int yMin = Mathf.Max(0, cy - extent);
            int yMax = Mathf.Min(_rows - 1, cy + extent);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (_cells.TryGetValue(CellKey(x, y), out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            // 同一 hitbox 只可能出现一次(单 cell 插入),
                            // 这里不再用 HashSet 去重(简化逻辑)。
                            _queryResult.Add(list[i]);
                        }
                    }
                }
            }
            return _queryResult;
        }

        int WorldToCellX(float worldX) => Mathf.Clamp(Mathf.FloorToInt((worldX - _originX) / _cellSize), 0, _cols - 1);
        int WorldToCellY(float worldY) => Mathf.Clamp(Mathf.FloorToInt((worldY - _originY) / _cellSize), 0, _rows - 1);
        int CellKey(int x, int y) => y * _cols + x;
    }
}