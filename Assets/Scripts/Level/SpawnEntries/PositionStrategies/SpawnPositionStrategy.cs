using System;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries.PositionStrategies
{
    /// <summary>
    /// "Sustain 每次生成的位置怎么算"—— 抽象策略基类。
    ///
    /// 设计动机:
    ///   SustainSpawnEntry 在指定点位持续刷敌,每次生成的位置 = SpawnPosition + 偏移。
    ///   偏移的"算法"在不同关卡设计里有不同需求:
    ///     - 固定累加(每次往同方向走 N 单位,拉一条"小怪行军线")
    ///     - 范围随机(每次在方框内独立抽位置,营造"散开"效果)
    ///     - 未来:贝塞尔轨迹 / 围绕某点公转 / 按时间正弦 ...
    ///
    /// 抽象成 [SerializeReference] 多态对象后:
    ///   - Inspector 里下拉选 strategy 类型,字段跟着换(对齐 SfxRule / FireExtension 套路)
    ///   - 加新 strategy = 新建子类 + [SRName("PositionStrategy/<名字>")],无需改 SustainSpawnEntry
    ///   - 老 strategy 类不动,符合开闭原则
    ///
    /// 实现约定:
    ///   - GetOffset 必须为单次调用返回"本次生成要叠加的偏移"。
    ///   - 内部状态(如 FixedSpawnPositionStrategy 的累加器)由子类自己维护。
    ///   - 每次"持续型 entry 被触发"会调 Reset() 重置内部状态。
    /// </summary>
    [Serializable]
    public abstract class SpawnPositionStrategy
    {
        /// <summary>
        /// 计算本次生成的偏移量。返回后本次调用结束,下次调用拿下一只的偏移。
        /// t:从触发起已过时间(秒, 范围 [0..Duration]),子类可读用于时间相关轨迹。
        /// </summary>
        public abstract Vector2 GetOffset(float t);

        /// <summary>
        /// 重置内部状态(累加器 / 历史位置等)。SustainSpawnEntry.OnTrigger 会调一次,
        /// 保证同一条 entry 多次触发时从"原点"重新开始。
        /// 默认无操作;有状态的 strategy 子类需 override。
        /// </summary>
        public virtual void Reset() { }
    }
}