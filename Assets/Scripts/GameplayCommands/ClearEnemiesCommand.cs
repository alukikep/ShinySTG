using System;
using System.Collections.Generic;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;

namespace ShinySTG.GameplayCommands
{
    /// <summary>让当前所有普通敌人无奖励自毁，保留 Boss 和已发射的弹幕。</summary>
    [Serializable, SRName("Command/Clear Enemies")]
    public sealed class ClearEnemiesCommand : GlobalCommand
    {
        public override void Execute(GlobalCommandContext context)
        {
            // 自毁会立即移除 Alive 登记，使用快照避免漏掉相邻敌人。
            var snapshot = new List<EnemyHealth>(EnemyHealth.Alive);
            foreach (var health in snapshot)
            {
                if (health == null || health.GetComponentInParent<BossHealth>() != null)
                    continue;
                var enemy = health.GetComponent<Enemy>();
                if (enemy != null) enemy.SelfDestruct();
            }
        }
    }
}
