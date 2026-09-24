using System;
using SerializeReferenceEditor;
using ShinySTG.Hitbox;

namespace ShinySTG.GameplayCommands
{
    [Serializable, SRName("Command/Clear Projectiles")]
    public sealed class ClearProjectilesCommand : GlobalCommand
    {
        public bool ClearEnemyProjectiles = true;
        public bool ClearPlayerProjectiles;
        public bool ClearNeutralProjectiles;
        public bool IncludeLasers = true;
        public BulletClearPresentation Presentation = new BulletClearPresentation();

        public override void Execute(GlobalCommandContext context)
        {
            BulletPool.Instance?.ReturnAll(ShouldClear, Presentation);
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
