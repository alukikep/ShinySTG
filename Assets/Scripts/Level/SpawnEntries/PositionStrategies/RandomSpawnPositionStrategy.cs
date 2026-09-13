using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries.PositionStrategies
{
    /// <summary>
    /// 范围随机策略:每次生成时,在 [-Range, +Range] 的方框内独立随机抽偏移。
    /// 每只敌人位置独立(不累加,不连续)。
    ///
    /// 典型场景:在固定区域里"散开"刷一群小怪(不想要行军感)。
    /// Range = (0, 0) 时所有生成都在 SpawnPosition(等同 Fixed + Offset = (0,0))。
    /// </summary>
    [Serializable, SRName("Random")]
    public class RandomSpawnPositionStrategy : SpawnPositionStrategy
    {
        [Tooltip("X/Y 各自在 [-Range, +Range] 内独立随机(Unity 世界单位)。\n" +
                 "例如 (0.5, 0.5) = 在 1×1 单位的方框内随机抽位置;\n" +
                 "(0, 0) = 无随机,等同无偏移。")]
        public Vector2 Range = Vector2.zero;

        public override Vector2 GetOffset(float t)
        {
            // 每只独立重抽,无状态需要 Reset
            return new Vector2(
                UnityEngine.Random.Range(-Range.x, Range.x),
                UnityEngine.Random.Range(-Range.y, Range.y));
        }

        // 无需 override Reset —— 每次 GetOffset 都是独立抽样,没有跨次状态
    }
}