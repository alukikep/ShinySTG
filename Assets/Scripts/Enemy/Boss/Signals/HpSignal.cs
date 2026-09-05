using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 读取 BossController.Health 的 HP 百分比。
    /// </summary>
    [Serializable, SRName("Signal/HP %")]
    public class HpSignal : BossSignal
    {
        public override float CurrentValue =>
            _health != null ? _health.HpPercent : 100f;

        BossHealth _health;

        public override void OnAttach(BossController boss)
        {
            _health = boss != null ? boss.Health : null;
        }

    }
}
