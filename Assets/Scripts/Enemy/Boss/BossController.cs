using System.Collections.Generic;
using UnityEngine;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 主驱动:挂 boss GameObject 上,负责阶段调度。
    /// 每个阶段(BossPhase)内部用 ShooterEnemy 复用现有 EnemyAction 体系。
    /// 阶段切换通过 Signals + PhaseTriggers 判定,完全 Inspector 配置。
    /// </summary>
    public class BossController : MonoBehaviour
    {
        [Header("Required")]
        [Tooltip("Boss 的 HP 组件。HpSignal 会读它。")]
        public BossHealth Health;

        [Header("Signals (全局信号池)")]
        [SerializeReference, SR]
        [Tooltip("每帧 tick 的信号源,产出 CurrentValue。可下拉选:HpSignal / ShotsFiredSignal / PhaseTimeSignal。")]
        public BossSignal[] Signals;

        [Header("Phases")]
        [SerializeReference, SR]
        [Tooltip("boss 阶段序列。按顺序执行,每个阶段带 ExitTriggers 决定何时切下一阶段。")]
        public BossPhase[] Phases;

        [Header("Loop")]
        [Tooltip("最后阶段结束后是否从头循环(留口子,默认 false)。")]
        public bool Loop = false;

        int   _phaseIdx  = -1;
        BossPhase _current;
        readonly Dictionary<BossSignal, int> _signalToIndex = new();

        void Awake()
        {
            BuildSignalLookup();
        }

        void Start()
        {
            // Signals 注册
            if (Signals != null)
                foreach (var s in Signals) s?.OnAttach(this);

            // 进入第一个阶段
            if (Phases != null && Phases.Length > 0) EnterPhase(0);
        }

        void Update()
        {
            // 1. tick 所有 signals(累加内部计数等)
            if (Signals != null)
                for (int i = 0; i < Signals.Length; i++)
                    if (Signals[i] != null) Signals[i].Tick(this, Time.deltaTime);

            // 2. tick 当前 phase
            if (_current == null) return;

            _current.OnTick(transform, Time.deltaTime);

            // 3. 判定是否该切走
            if (_current.ShouldExit()) NextPhase();
        }

        public BossSignal GetSignal(int index) =>
            (Signals != null && index >= 0 && index < Signals.Length) ? Signals[index] : null;

        void BuildSignalLookup()
        {
            _signalToIndex.Clear();
            if (Signals == null) return;
            for (int i = 0; i < Signals.Length; i++)
                if (Signals[i] != null) _signalToIndex[Signals[i]] = i;
        }

        void EnterPhase(int idx)
        {
            _phaseIdx = idx;
            _current  = Phases[idx];

            // PhaseTimeSignal 在进入新阶段时重置
            if (Signals != null)
                foreach (var s in Signals)
                    if (s is PhaseTimeSignal pts) pts.Reset();

            _current?.OnEnter(transform);
        }

        void NextPhase()
        {
            _current?.OnExit(transform);

            int next = _phaseIdx + 1;
            if (next >= Phases.Length)
            {
                if (Loop && Phases.Length > 0) next = 0;
                else { _current = null; return; } // 序列结束
            }
            EnterPhase(next);
        }
    }
}
