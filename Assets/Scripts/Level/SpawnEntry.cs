using System;
using UnityEngine;

namespace ShinySTG.Level
{
    /// <summary>
    /// 关卡时间轴的"一条条目"多态抽象。对齐 BossPhase / EnemyAction / MoveBehaviour 的扩展套路:
    /// 加新触发条件(玩家到位 / 敌人全清 / 周期性循环 / ...) = 新建 SpawnEntry 子类 + 加 [SRName],
    /// 无需修改 LevelController / LevelDefinition。
    ///
    /// 与 BossPhase 的对齐点:
    ///   - 都有"触发条件判定(ShouldTrigger)"+"触发动作(OnTrigger)"两个虚方法,
    ///     由宿主 LevelController 统一每帧轮询,不关心具体子类。
    ///   - 通过 [Serializable] + [SerializeReference, SRName] 在 Inspector 下拉选具体类型。
    ///
    /// 与 EnemyAction 的差异:
    ///   - 没有 Duration 字段(条目是时间点触发,不是时间轴动作);
    ///   - 没有 OnTick(关卡条目是"事件型",每条触发一次;若要周期触发,新建 RepeatSpawnEntry 即可)。
    /// </summary>
    [Serializable]
    public abstract class SpawnEntry
    {
        [Tooltip("在关卡第几秒触发。\n" +
                 "多个条目时间相同时按数组顺序依次触发。")]
        public float TriggerTime = 0f;

        [Tooltip("true = 触发后即作废(默认)。\n" +
                 "false = 留着,留给 RepeatSpawnEntry / 条件型条目复用。")]
        public bool OneShot = true;

        [Tooltip("持续时间(秒)。<=0 = 触发一次即结束(等价于普通点)。\n" +
                 ">0 = 从 TriggerTime 起持续 N 秒,期间子类的 OnTick 会每帧调用,\n" +
                 "     适合\"在该点位持续刷敌 / 周期生成 / 等玩家离开\"等需要时间窗口的条目。\n" +
                 "编辑器中:此字段控制时间轴 block 的宽度。")]
        public float Duration = 0f;

        /// <summary>是否持续型条目(Duration > 0)。运行时 + 编辑器都用来判断走"瞬时触发"还是"持续 Tick"。</summary>
        public bool HasDuration => Duration > 0f;

        /// <summary>
        /// 持续型条目的每帧 tick(由 LevelRuntime / Editor Preview 调度)。
        /// 默认无操作;持续型 SpawnEntry 子类 override 此方法。
        ///
        /// 参数:
        ///   - t:从触发起已过时间(秒, 范围 [0..Duration])
        ///   - dt:本帧 deltaTime
        /// </summary>
        public virtual void OnTick(LevelRuntime runtime, float t, float dt) { }

        /// <summary>
        /// 持续条目结束(达到 Duration)时调用一次。子类可释放资源 / 收尾。
        /// 默认无操作。
        /// </summary>
        public virtual void OnEnd(LevelRuntime runtime) { }

        /// <summary>
        /// LevelController 每帧调用,问"现在该不该跳 OnTrigger"。
        /// 默认实现:OneShot 模式下看 alreadyFired;非 OneShot 模式(留给 Repeat)总是可触发。
        /// 子类可以覆盖(例如 ConditionalSpawnEntry 看玩家位置)。
        /// </summary>
        public virtual bool ShouldTrigger(bool alreadyFired) =>
            OneShot ? !alreadyFired : true;

        /// <summary>
        /// 触发时的实际操作:实例化 prefab / 调用 BossController.Start() / ...
        /// runtime 提供 Track 辅助(把生成的单位登记到活跃列表),便于将来关卡清理 / 失败判定。
        /// </summary>
        public abstract void OnTrigger(LevelRuntime runtime, LevelDefinition def);
    }
}