using UnityEngine;
using ShinySTG.GameActions;

namespace ShinySTG.Player
{
    [CreateAssetMenu(menuName = "ShinySTG/Player/Bomb Definition", fileName = "BombDefinition")]
    public sealed class BombDefinition : ScriptableObject
    {
        [Min(0f)] public float Duration = 2f;
        [Min(0f)] public float InvincibilityDuration = 2f;
        public bool ClearEnemyProjectiles = true;
        public bool IncludeLasers = true;
        public bool DamageAllEnemies;
        [Min(0f)] public float EnemyDamage;
        [Min(0f)] public float DamageInterval;
        public ShinySTG.Audio.SfxCue BombSfx;
        public bool ReturnSpawnedBulletsOnEnd = true;
        [Min(0f)] public float DeathbombWindow = 0.1f;
        public ActionSequence StartActions;
        public ActionSequence ActiveActions;
        public ActionSequence EndActions;
    }
}
