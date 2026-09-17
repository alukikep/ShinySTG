using System;
using SerializeReferenceEditor;
using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;
using ShinySTG.Player;
using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameplayCommands
{
    public enum InvincibilityOperation
    {
        Add,
        Remove
    }

    [Serializable, SRName("Command/Set Invincibility")]
    public sealed class SetInvincibilityCommand : GlobalCommand
    {
        [Tooltip("用于区分不同系统施加的无敌锁，例如 dialogue 或 boss_transition。")]
        public string SourceKey = "global_command";
        public InvincibilityOperation Operation = InvincibilityOperation.Add;
        public bool AffectPlayer = true;
        public bool AffectAllEnemies;
        public bool AffectAllBosses;
        [Tooltip("只影响执行指令的对象；对象上没有 Health 组件时无副作用。")]
        public bool AffectOwner;

        public override void Execute(GlobalCommandContext context)
        {
            if (string.IsNullOrWhiteSpace(SourceKey))
            {
                Debug.LogWarning("[GlobalCommand] SetInvincibilityCommand.SourceKey 为空，已忽略。", context.Owner);
                return;
            }

            if (AffectPlayer) Apply(PlayerController.Instance != null ? PlayerController.Instance.Health : null);

            if (AffectAllEnemies)
            {
                var alive = EnemyHealth.Alive;
                for (int i = 0; i < alive.Count; i++) Apply(alive[i]);
            }

            if (AffectAllBosses)
            {
                var alive = BossHealth.Alive;
                for (int i = 0; i < alive.Count; i++) Apply(alive[i]);
            }

            if (AffectOwner && context.Owner != null)
            {
                Apply(context.Owner.GetComponent<PlayerHealth>());
                Apply(context.Owner.GetComponent<EnemyHealth>());
                Apply(context.Owner.GetComponent<BossHealth>());
            }
        }

        void Apply(PlayerHealth health)
        {
            if (health == null) return;
            if (Operation == InvincibilityOperation.Add) health.AddInvincibility(SourceKey);
            else health.RemoveInvincibility(SourceKey);
        }

        void Apply(EnemyHealth health)
        {
            if (health == null) return;
            if (Operation == InvincibilityOperation.Add) health.AddInvincibility(SourceKey);
            else health.RemoveInvincibility(SourceKey);
        }

        void Apply(BossHealth health)
        {
            if (health == null) return;
            if (Operation == InvincibilityOperation.Add) health.AddInvincibility(SourceKey);
            else health.RemoveInvincibility(SourceKey);
        }
    }
}
