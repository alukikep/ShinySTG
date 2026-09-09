using System.Collections.Generic;
using UnityEngine;
using SerializeReferenceEditor;
using ShinySTG.Level;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 协调器:负责阶段调度 + Signal 维护。
    ///
    /// 职责拆分(对齐 Enemy 子树的"协调器 vs 总控"分工):
    ///   - 本组件 = 协调器:只管阶段 / Signal / OnBossDefeated 广播
    ///   - Boss 总控(Assets/Scripts/Enemy/Boss/Boss.cs) = 总控:订阅 OnDeath → 调 Stop() + Destroy
    ///   - 阶段收尾(走当前 phase.OnExit)由 Stop() 负责,不直接 Destroy
    ///   - 关卡级事件广播 NotifyBossDefeated 仍由本组件在 Stop() 内触发(广播属于协调层语义)
    ///
    /// [RequireComponent] 强制依赖(BossHitbox 替换原 EnemyHitbox 的位置,同 Team=Enemy):
    ///   - BossHealth:HP + 多管血
    ///   - BossHitbox:碰撞盒
    ///   - BossShotCounter:全局开火计数(ShotsFiredSignal 读)
    ///
    /// 死亡收尾流程:
    ///   1. BossHealth.OnDeath 事件触发
    ///   2. Boss 总控订阅 OnDeath → 调 BossController.Stop() 走阶段收尾
    ///   3. Stop() 内部:phase.OnExit + 广播 NotifyBossDefeated + 标 _defeated
    ///   4. Boss 总控 HandleDeath 收尾 → Destroy(gameObject)
    /// </summary>
    [RequireComponent(typeof(BossHealth))]
    [RequireComponent(typeof(BossHitbox))]
    [RequireComponent(typeof(BossShotCounter))]
    public class BossController : MonoBehaviour
    {
        [Header("Required (RequireComponent 自动注入)")]
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

        int _phaseIdx = -1;
        BossPhase _current;
        bool _stopped;       // 防 Stop() 重复调用 + Update 跳过 phase tick
        bool _defeated;      // 防 NotifyBossDefeated 重复广播
        readonly Dictionary<BossSignal, int> _signalToIndex = new();

        void Awake()
        {
            BuildSignalLookup();
            Health = GetComponent<BossHealth>();
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
            // 已 stopped → 不再跑 phase / signal tick,直到 GameObject 被 Boss 总控销毁。
            if (_stopped) return;

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

        /// <summary>
        /// 公开入口:由 Boss 总控在订阅 BossHealth.OnDeath 后调用。
        /// 行为:
        ///   1. 走当前 phase.OnExit 收尾
        ///   2. 标 _stopped = true(让 Update 跳过 phase tick,避免 runtime 悬挂)
        ///   3. 一次性广播 NotifyBossDefeated(关卡级订阅入口)
        ///   4. **不直接 Destroy**(由 Boss 总控决定销毁时机,语义对齐 Enemy.HandleDeath)
        /// </summary>
        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;

            // 1. 走当前 phase 收尾
            if (_current != null)
            {
                _current.OnExit(transform);
                _current = null;
            }

            // 2. 一次性广播 Defeated(给"解锁下一关 / UI 提示"等订阅)
            if (!_defeated)
            {
                _defeated = true;
                LevelController.Instance?.NotifyBossDefeated(gameObject);
            }
            // 3. 销毁由 Boss 总控 HandleDeath 负责(语义对齐 Enemy 总控 Stop + Destroy 的两步式)。
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
            _current = Phases[idx];

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
