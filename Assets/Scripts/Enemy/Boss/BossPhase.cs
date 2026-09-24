using System;
using ShinySTG.Audio;
using ShinySTG.GameActions;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.GameplayCommands;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 阶段抽象基类。ShooterPhase 是当前唯一内置实现。
    /// 扩展时新建 : BossPhase 子类,加 [Serializable, SRName("Phase/<你的名字>")] 即可被 Phases 数组识别。
    /// </summary>
    [Serializable]
    public abstract class BossPhase
    {
        public enum StateAdvanceMode { Time, HealthPercent }
        [Tooltip("管内下一状态的触发方式；最后一个状态保持到本管结束。") ]
        public StateAdvanceMode AdvanceMode;
        [Min(0.01f), Tooltip("从本状态正式开始计时，达到此秒数进入下一状态。") ]
        public float AdvanceAfterSeconds = 10f;
        [Range(0f, 100f), Tooltip("本管剩余血量不高于此百分比时进入下一状态。") ]
        public float AdvanceAtPercent = 50f;

        [Tooltip("阶段名称，用于配置辨认。")]
        public string DisplayName;
        public SfxCue EnterSfx;
        public SfxCue ExitSfx;
        [Tooltip("战斗启动前执行，可等待；随后执行 EnterCommands 和行为流。")]
        public ActionSequence EnterActions = new();
        [Tooltip("正常阶段退出指令之后执行；死亡时改走整场 DefeatActions。")]
        public ActionSequence ExitActions = new();

        [SerializeReference, SR]
        [Tooltip("进入该阶段、启动阶段行为流之前执行的全局指令。")]
        public GlobalCommand[] EnterCommands;

        [SerializeReference, SR]
        [Tooltip("阶段行为流退出之后执行的全局指令。正常切阶段与 Boss 死亡收尾都会执行。")]
        public GlobalCommand[] ExitCommands;

        [SerializeReference, SR]
        [Tooltip("该阶段的退出条件。任意一条满足即切到下一阶段。每个 PhaseTrigger 通过 SignalIndex 引用 BossController.Signals 数组。")]
        public PhaseTrigger[] ExitTriggers;

        [Tooltip("LegacyTriggers 保持旧条件行为；Conditions 仅使用新条件，不与旧条件混合。")]
        public PhaseExitMode ExitMode = PhaseExitMode.LegacyTriggers;

        [Tooltip("新条件的组合方式：任意满足或全部满足。空条件列表始终不自动退出。")]
        public PhaseConditionMatch ConditionMatch = PhaseConditionMatch.Any;

        [Tooltip("仅 Conditions 模式生效。血管条件通过稳定 ID 指定目标。")]
        public PhaseExitCondition[] ExitConditions;

        [Tooltip("开启后，指定血管耗尽时立即保护后续血管，持续到下一阶段入场完成。默认关闭。")]
        public bool ProtectBarTransition;

        [Tooltip("触发过渡保护的唯一血管 ID。仍须配置退出条件；保护不会自动跳过条件。")]
        public string ProtectedBarId = "";

        /// <summary>
        /// 判定本阶段是否该退出。需要 BossController 上下文,因为 PhaseTrigger 持有的是 SignalIndex,
        /// 要从 BossController.Signals 数组里取对应的 Signal 实例。
        ///
        /// 设计动机:PhaseTrigger 不能再持有 BossSignal 实例(SerializeReference 独立实例导致 _health 没绑),
        /// 改成 SignalIndex 后,ShouldExit 必须有 BossController 上下文才能正确判定。
        /// </summary>
        public bool ShouldExit(BossController ctx)
        {
            if (ctx == null) return false;
            if (ExitMode == PhaseExitMode.Conditions)
            {
                if (ExitConditions == null || ExitConditions.Length == 0) return false;
                if (ConditionMatch != PhaseConditionMatch.Any && ConditionMatch != PhaseConditionMatch.All) return false;
                foreach (var condition in ExitConditions)
                {
                    bool satisfied = condition != null && condition.IsSatisfied(ctx);
                    if (ConditionMatch == PhaseConditionMatch.Any && satisfied) return true;
                    if (ConditionMatch == PhaseConditionMatch.All && !satisfied) return false;
                }
                return ConditionMatch == PhaseConditionMatch.All;
            }
            if (ExitMode != PhaseExitMode.LegacyTriggers) return false;
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
