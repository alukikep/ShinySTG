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
        [Tooltip("Bomb 内置消弹强度：一级保留防御弹，二级消除普通弹和防御弹。")]
        public BulletClearLevel ClearLevel = BulletClearLevel.Strong;
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
