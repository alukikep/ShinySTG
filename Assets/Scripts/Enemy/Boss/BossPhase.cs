using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 阶段抽象基类。ShooterPhase 是当前唯一内置实现。
    /// 扩展时新建 : BossPhase 子类,加 [Serializable, SRName("Phase/<你的名字>")] 即可被 Phases 数组识别。
    /// </summary>
    [Serializable]
    public abstract class BossPhase
    {
        [SerializeReference, SR]
        [Tooltip("该阶段的退出条件。任意一条满足即切到下一阶段。")]
        public PhaseTrigger[] ExitTriggers;

        public bool ShouldExit()
        {
            if (ExitTriggers == null) return false;
            for (int i = 0; i < ExitTriggers.Length; i++)
                if (ExitTriggers[i] != null && ExitTriggers[i].IsSatisfied())
                    return true;
            return false;
        }

        public abstract void OnEnter(Transform boss);
        public abstract void OnTick (Transform boss, float dt);
        public abstract void OnExit (Transform boss);
    }
}
