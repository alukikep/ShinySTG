using System;
using System.Collections.Generic;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;

namespace ShinySTG.GameplayCommands
{
    /// <summary>让当前所有普通敌人走完整死亡路径，保留 Boss。</summary>
    [Serializable, SRName("Command/Kill Enemies")]
    public sealed class KillEnemiesCommand : GlobalCommand
    {
        public override void Execute(GlobalCommandContext context)
        {
            var snapshot = new List<EnemyHealth>(EnemyHealth.Alive);
            foreach (var health in snapshot)
            {
                if (health == null || health.GetComponentInParent<BossHealth>() != null)
                    continue;
                var enemy = health.GetComponent<Enemy>();
                if (enemy != null) enemy.KillWithRewards();
            }
        }
    }
}
