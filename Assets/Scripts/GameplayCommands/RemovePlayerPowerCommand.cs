using System;
using SerializeReferenceEditor;
using ShinySTG.Player;
using UnityEngine;
using PlayerController = ShinySTG.Player.Player;

namespace ShinySTG.GameplayCommands
{
    [Serializable, SRName("Command/Remove Player Power")]
    public sealed class RemovePlayerPowerCommand : GlobalCommand
    {
        [Tooltip("要扣除的火力值。1.0 = 一整级，0.01 = 一个 Power 单位。")]
        [Min(0f)]
        public float Power = 1f;

        public override void Execute(GlobalCommandContext context)
        {
            var player = PlayerController.Instance;
            if (player == null || player.Health == null || Power <= 0f) return;

            int units = Mathf.Max(1, Mathf.RoundToInt(Power * 100f));
            player.Health.RemovePowerUnits(units);
        }
    }
}
