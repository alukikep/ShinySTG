using ShinySTG.EnemyAI;
using ShinySTG.EnemyAI.Boss;
using ShinySTG.Items;
using ShinySTG.Laser;
using ShinySTG.Level.Encounter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinySTG.Level
{
    /// <summary>仅在碰撞遍历之外调用；取消战斗来源后回池，不结算击杀或拾取奖励。</summary>
    public static class BattleCleanup
    {
        public static void Clear(Scene scene, LevelRuntime runtime, BulletPool poolOverride = null)
        {
            using (BattleRestriction.Acquire())
            {
                runtime?.CancelTimelineProcesses();
                foreach (var host in Object.FindObjectsOfType<BossEncounterHost>(true))
                    if (host.gameObject.scene == scene) host.Cancel();
                foreach (var boss in Object.FindObjectsOfType<Boss>(true))
                    if (boss.gameObject.scene == scene) boss.Despawn();
                foreach (var enemy in Object.FindObjectsOfType<Enemy>(true))
                    if (enemy.gameObject.scene == scene) enemy.SelfDestruct();
                runtime?.ClearBattleState();

                foreach (var pool in Object.FindObjectsOfType<BulletPool>(true))
                    if (pool.gameObject.scene == scene || pool == poolOverride) pool.ReturnAll();
                foreach (var pool in Object.FindObjectsOfType<LaserPool>(true))
                    if (pool.gameObject.scene == scene) pool.ReturnAll();
                foreach (var items in Object.FindObjectsOfType<ItemDropService>(true))
                    if (items.gameObject.scene == scene) items.ReturnAll();
            }
        }
    }
}
