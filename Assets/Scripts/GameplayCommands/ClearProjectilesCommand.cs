using System;
using SerializeReferenceEditor;
using ShinySTG.Hitbox;
using UnityEngine;

namespace ShinySTG.GameplayCommands
{
    [Serializable, SRName("Command/Clear Projectiles")]
    public sealed class ClearProjectilesCommand : GlobalCommand
    {
        public bool ClearEnemyProjectiles = true;
        public bool ClearPlayerProjectiles;
        public bool ClearNeutralProjectiles;
        public bool IncludeLasers = true;
        [Tooltip("一级保留有消弹防御的子弹；二级消除两种子弹。此等级不影响激光。")]
        public BulletClearLevel ClearLevel = BulletClearLevel.Normal;
        public BulletClearPresentation Presentation = new BulletClearPresentation();

        public override void Execute(GlobalCommandContext context)
        {
            BulletPool.Instance?.ClearAll(ShouldClear, ClearLevel, Presentation);
            if (IncludeLasers)
                ShinySTG.Laser.LaserPool.Instance?.ReturnAll(ShouldClear);
        }

        bool ShouldClear(CollisionTeam team)
        {
            switch (team)
            {
                case CollisionTeam.Enemy: return ClearEnemyProjectiles;
                case CollisionTeam.Player: return ClearPlayerProjectiles;
                default: return ClearNeutralProjectiles;
            }
        }
    }
}
