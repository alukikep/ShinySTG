using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Hitbox
{
    /// <summary>
    /// 2D 均匀网格空间哈希。用于把"碰撞检测"从 O(N×M) 降到 O(N + k),k 是同 cell 数。
    ///
    /// 用法(CollisionService 内部使用,外部一般不直接调):
    ///   var grid = new UniformGrid(cellSize: 4f, worldMin: (-10,-20), worldMax: (10,20));
    ///   grid.Clear();
    ///   foreach (var hb in hitboxes) grid.Insert(hb._cachedBounds, hb.GetInstanceID());
    ///   grid.Query(bulletBounds, ids);
    ///
    /// 适用场景:活跃子弹 > ~500 颗,或敌人 > ~50 只时开启。
    /// 弹量小时朴素遍历更便宜(网格本身有常数开销)。
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
        readonly Dictionary<int, List<int>> _cells = new();

        // 复用缓冲,避免每次 Query 时 new List
        readonly HashSet<int> _querySet = new();
        readonly List<int> _queryResult = new();

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

        public void Insert(Rect bounds, int id)
        {
            int xMin = WorldToCellX(bounds.xMin);
            int xMax = WorldToCellX(bounds.xMax);
            int yMin = WorldToCellY(bounds.yMin);
            int yMax = WorldToCellY(bounds.yMax);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    int key = CellKey(x, y);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<int>(8);
                        _cells[key] = list;
                    }
                    list.Add(id);
                }
            }
        }

        /// <summary>把 bounds 覆盖的所有 cell 里的 id 去重写入 _queryResult(复用缓冲)。</summary>
        public List<int> Query(Rect bounds)
        {
            _querySet.Clear();
            _queryResult.Clear();

            int xMin = WorldToCellX(bounds.xMin);
            int xMax = WorldToCellX(bounds.xMax);
            int yMin = WorldToCellY(bounds.yMin);
            int yMax = WorldToCellY(bounds.yMax);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (_cells.TryGetValue(CellKey(x, y), out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            int id = list[i];
                            if (_querySet.Add(id))
                                _queryResult.Add(id);
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