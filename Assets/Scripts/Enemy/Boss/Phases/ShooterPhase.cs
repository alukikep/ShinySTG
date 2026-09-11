using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.EnemyAI;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 行为流阶段:持有一个 BehaviorFlow SO 资产,进入时自动跑、退出时清理。
    /// 不再依赖 ShooterEnemy 组件 —— boss prefab 上不需要挂多个 ShooterEnemy 了。
    /// </summary>
    [Serializable, SRName("Phase/Shooter")]
    public class ShooterPhase : BossPhase
    {
        [Tooltip("该阶段使用的行为流资产(右键 Project → Create → STG → Behavior Flow)。")]
        public BehaviorFlow Flow;

        [Tooltip("进入阶段时是否从行为流开头开始执行。推荐 true。")]
        public bool ResetOnEnter = true;

        BehaviorFlowRuntime _runtime;

        public override void OnEnter(Transform boss)
        {
            // __BOSSDEBUG__ #5:阶段进入时打印 Flow
            Debug.Log($"[__BOSSDEBUG__] ShooterPhase.OnEnter Flow={(Flow == null ? "NULL" : Flow.name)} reset={ResetOnEnter}", boss);
            if (Flow == null) return;
            _runtime = Flow.Instantiate();
            if (ResetOnEnter) _runtime.Reset();
        }

        public override void OnTick(Transform boss, float dt)
        {
            if (_runtime == null)
            {
                // __BOSSDEBUG__ #6:Runtime 已被清空(说明 Flow 跑完或 OnExit 调过了),不再 tick
                return;
            }
            _runtime.Tick(boss, dt);
        }

        public override void OnExit(Transform boss)
        {
            _runtime?.ForceExit(boss);
            _runtime = null;
        }
    }
}

