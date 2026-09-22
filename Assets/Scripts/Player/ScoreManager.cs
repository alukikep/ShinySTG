using UnityEngine;
using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;

namespace ShinySTG.Player
{
    /// <summary>将击杀和擦弹事件转换为当前玩家分数。</summary>
    [DisallowMultipleComponent]
    public sealed class ScoreManager : MonoBehaviour
    {
        [Min(0), SerializeField, Tooltip("每次擦弹获得的分数。")]
        int _grazeScore = 10;

        Player player;

        void Awake() => player = GetComponent<Player>();

        void OnEnable()
        {
            EnemyHealth.OnAnyDeath += HandleEnemyDeath;
            BossHealth.OnAnyDeath += HandleBossDeath;
            if (player != null && player.Health != null) player.Health.OnGraze += HandleGraze;
        }

        void OnDisable()
        {
            EnemyHealth.OnAnyDeath -= HandleEnemyDeath;
            BossHealth.OnAnyDeath -= HandleBossDeath;
            if (player != null && player.Health != null) player.Health.OnGraze -= HandleGraze;
        }

        void HandleEnemyDeath(EnemyHealth enemy)
        {
            if (enemy != null) Add(enemy.ScoreValue);
        }

        void HandleBossDeath(BossHealth boss)
        {
            if (boss != null) Add(boss.ScoreValue);
        }

        void HandleGraze(int _) => Add(_grazeScore);

        void Add(int amount)
        {
            if (amount > 0 && player != null && player.Resources != null)
                player.Resources.AddScore(amount);
        }
    }
}
