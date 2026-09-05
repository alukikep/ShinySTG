using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 所有血管累计剩余 HP 百分比(0~100)。
    /// 用途:残血触发(比如 Total &lt;= 30% 切到暴走 phase)。
    /// 注意:百分比是按所有 MaxHp 加权后的总剩余比例,不是简单平均。
    /// </summary>
    [Serializable, SRName("Signal/Total HP %")]
    public class TotalHpPercentSignal : BossSignal
    {
        BossHealth _health;

        public override void OnAttach(BossController boss)
        {
            _health = boss != null ? boss.Health : null;
        }

        public override float CurrentValue =>
            _health != null ? _health.TotalHpPercent : 100f;
    }
}
