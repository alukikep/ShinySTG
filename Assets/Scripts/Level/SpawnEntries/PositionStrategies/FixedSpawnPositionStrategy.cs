using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries.PositionStrategies
{
    /// <summary>
    /// 固定累加策略:每次生成后位置累加一份 Offset。
    /// 第 N 只的位置 = SpawnPosition + (N-1) × Offset。
    ///
    /// 典型场景:从某个边缘持续刷怪往对面推进("行军线")。
    /// Offset = (0, 0) 时退化为"始终在 SpawnPosition"—— 等价于不带偏移的旧版 Sustain。
    /// </summary>
    [Serializable, SRName("Fixed")]
    public class FixedSpawnPositionStrategy : SpawnPositionStrategy
    {
        [Tooltip("每次生成后位置累加这个偏移(Unity 世界单位)。\n" +
                 "例如 (1, 0) = 每次往 X 方向平移 1 单位;\n" +
                 "(0, 0) = 无偏移,所有生成都在 SpawnPosition。")]
        public Vector2 Offset = Vector2.zero;

        Vector2 _current;

        public override Vector2 GetOffset(float t)
        {
            // 返回当前累加值,然后把偏移累加一份给下一次
            var result = _current;
            _current += Offset;
            return result;
        }

        public override void Reset()
        {
            // 持续型 entry 重新触发时从原点开始,避免跨次持续状态污染
            _current = Vector2.zero;
        }
    }
}