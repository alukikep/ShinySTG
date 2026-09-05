using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 当前 boss 阶段已经持续的秒数。
    /// BossController 在 EnterPhase 时自动调用 Reset(),故只需 Op = GreaterOrEqual + Threshold = N。
    /// </summary>
    [Serializable, SRName("Signal/Phase Time")]
    public class PhaseTimeSignal : BossSignal
    {
        public float _elapsed;
        public override float CurrentValue => _elapsed;
        public override void Tick(BossController boss, float dt) { _elapsed += dt; }
        public void Reset() { _elapsed = 0f; }
    }
}
