using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 当前血管的剩余 HP 百分比(0~100)。
    /// 一管打空后由 BossHealth 自动切到下一管,CurrentBarPercent 跟着重置为下一管的百分比。
    /// </summary>
    [Serializable, SRName("Signal/Current Bar %")]
    public class CurrentBarPercentSignal : BossSignal
    {
        BossHealth _health;

        public override void OnAttach(BossController boss)
        {
            _health = boss != null ? boss.Health : null;
        }

        public override float CurrentValue =>
            _health != null ? _health.CurrentBarPercent : 100f;
    }
}
