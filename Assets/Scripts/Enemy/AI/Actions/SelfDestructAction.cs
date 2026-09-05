using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 到达 Duration 后销毁敌人自身。常作为序列的最后一条。
    /// (注:OnExit 在 ShooterEnemy 检测到时间到时立刻调用,等价于该行为"结束的瞬间"自毁。)
    /// </summary>
    [Serializable, SRName("Action/Self Destruct")]
    public class SelfDestructAction : EnemyAction
    {
        public override void OnExit(Transform enemy)
        {
            if (enemy != null) UnityEngine.Object.Destroy(enemy.gameObject);
        }
    }
}
