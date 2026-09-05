using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 读 BossShotCounter.Total(boss 全局开火累计)。
    /// </summary>
    [Serializable, SRName("Signal/Shots Fired")]
    public class ShotsFiredSignal : BossSignal
    {
        public override float CurrentValue => BossShotCounter.Instance != null ? BossShotCounter.Instance.Total : 0;
    }
}
