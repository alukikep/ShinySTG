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
        [Tooltip("该阶段的退出条件。任意一条满足即切到下一阶段。每个 PhaseTrigger 通过 SignalIndex 引用 BossController.Signals 数组。")]
        public PhaseTrigger[] ExitTriggers;

        /// <summary>
        /// 判定本阶段是否该退出。需要 BossController 上下文,因为 PhaseTrigger 持有的是 SignalIndex,
        /// 要从 BossController.Signals 数组里取对应的 Signal 实例。
        ///
        /// 设计动机:PhaseTrigger 不能再持有 BossSignal 实例(SerializeReference 独立实例导致 _health 没绑),
        /// 改成 SignalIndex 后,ShouldExit 必须有 BossController 上下文才能正确判定。
        /// </summary>
        public bool ShouldExit(BossController ctx)
        {
            if (ExitTriggers == null || ctx == null) return false;
            for (int i = 0; i < ExitTriggers.Length; i++)
            {
                var t = ExitTriggers[i];
                if (t == null) continue;
                var sig = ctx.GetSignal(t.SignalIndex);
                if (sig != null && t.IsSatisfied(sig)) return true;
            }
            return false;
        }

        // 兼容旧 API:外部代码可能调无参 ShouldExit()。返回 false 让阶段永远不切,逼着改代码。
        public bool ShouldExit()
        {
            Debug.LogWarning("[BossPhase] 调了无参 ShouldExit(),请改用 ShouldExit(BossController ctx)。当前回退到 false(阶段不会切换)。");
            return false;
        }

        public abstract void OnEnter(Transform boss);
        public abstract void OnTick (Transform boss, float dt);
        public abstract void OnExit (Transform boss);
    }
}
