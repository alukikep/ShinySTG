using System;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 全局信号。每帧 Tick,产出 CurrentValue,供 PhaseTrigger 读取。
    /// 新增 transition = 新建一个 BossSignal 子类,加 [Serializable, SRName("Signal/<你的名字>")]。
    /// </summary>
    [Serializable]
    public abstract class BossSignal
    {
        /// <summary>BossController.Start 时调用,可挂外部订阅。</summary>
        public virtual void OnAttach(BossController boss) { }

        /// <summary>每帧调用,用于累加内部状态。</summary>
        public virtual void Tick(BossController boss, float dt) { }

        /// <summary>当前信号值(HpSignal: HP%; ShotsFiredSignal: 累计; PhaseTimeSignal: 秒数)。</summary>
        public abstract float CurrentValue { get; }
    }
}
