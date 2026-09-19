using System;
using UnityEngine;
using SerializeReferenceEditor;
using ShinySTG.Level;
using ShinySTG.GameplayCommands;
using ShinySTG.GameActions;

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
    ///
    ///
    /// 死亡收尾流程:
    ///   1. BossHealth.OnDeath 事件触发
    ///   2. Boss 总控订阅 OnDeath → 调 BossController.Stop() 走阶段收尾
    ///   3. Stop() 内部:phase.OnExit + 广播 NotifyBossDefeated + 标 _defeated
    ///   4. Boss 总控 HandleDeath 收尾 → Destroy(gameObject)
    /// </summary>
    [RequireComponent(typeof(BossHealth))]
    [RequireComponent(typeof(BossHitbox))]
    public class BossController : MonoBehaviour
    {
        /// <summary>进入阶段后广播。表现层可监听，但 Boss 战斗逻辑不依赖任何表现系统。</summary>
        public event Action<int, BossPhase> OnPhaseEntered;

        /// <summary>离开阶段前广播。正常切换与 Stop 收尾都会触发。</summary>
        public event Action<int, BossPhase> OnPhaseExited;

        // Encounter 在 Start 前绑定；返回 null 表示不阻塞阶段推进。
        public Func<int, bool, GameActionHandle> PhaseActions { get; set; }
        public GameActionHandle StartGate { get; set; }
        public int CurrentPhaseIndex => _phaseIdx;
        GameActionHandle _phaseGate;
        int _pendingPhase = -1;
        bool _enterPrepared;
        bool _barTransitionRequested;
        public bool IsTransitioning => _pendingPhase >= 0;

        [Header("Required (RequireComponent 自动注入,不要手填)")]
        [HideInInspector]
        [Tooltip("Boss 的 HP 组件。HpSignal 会读它。由 [RequireComponent] 自动注入,不要在 Inspector 手填。")]
        public BossHealth Health;

        [Header("Signals (全局信号池)")]
        [SerializeReference, SR]
        [Tooltip("每帧 tick 的信号源,产出 CurrentValue。可下拉选:HpSignal / PhaseTimeSignal。")]
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

        void Awake()
        {
            Health = GetComponent<BossHealth>();
        }

        void OnEnable()
        {
            // 订阅 Bar 切管事件:让"伤害击穿"导致的 Bar 切换也能立刻检测 ExitTrigger,
            // 解决 Update 永远抓不到 CurrentBarPercent=0 瞬间的问题。
            // (Update 在 Bar 切管的同一帧已经跑过了,下一帧 CurrentBarPercent 已经跳到新 Bar 的满血值。)
            if (Health != null) Health.OnBarDepleted += OnBarDepletedHandler;
        }

        void OnDisable()
        {
            if (Health != null) Health.OnBarDepleted -= OnBarDepletedHandler;
        }

        // __BOSSDEBUG__ #10:Bar 切管立刻检查是否切阶段(治本修复击穿问题)
        void OnBarDepletedHandler(int barIdx)
        {
            if (_stopped || _current == null) return;
            Debug.Log($"[__BOSSDEBUG__] OnBarDepletedHandler idx={barIdx} phase={_phaseIdx} curHp%={(Health != null ? Health.CurrentBarPercent.ToString("F1") : "?")}", this);
            // 在 Update 处理，等待本次 TakeDamage 完成后再决定是否仍应进入下一阶段。
            if (_current.ShouldExit(this)) _barTransitionRequested = true;
        }

        void Start()
        {
            // __BOSSDEBUG__ #4:初始化结果
            Debug.Log($"[__BOSSDEBUG__] BossController.Start signals={Signals?.Length ?? 0} phases={Phases?.Length ?? 0}", this);
            if (Signals != null)
                for (int i = 0; i < Signals.Length; i++)
                {
                    var s = Signals[i];
                    Debug.Log($"[__BOSSDEBUG__]   Signals[{i}] = {(s == null ? "null" : s.GetType().Name)}", this);
                }
            if (Phases != null)
                for (int i = 0; i < Phases.Length; i++)
                {
                    var p = Phases[i];
                    string flowName = "(non-Shooter)";
                    int exitCount = 0;
                    if (p is ShooterPhase sp)
                    {
                        flowName = sp.Flow != null ? sp.Flow.name : "(NULL FLOW!)";
                    }
                    if (p != null && p.ExitTriggers != null) exitCount = p.ExitTriggers.Length;
                    Debug.Log($"[__BOSSDEBUG__]   Phases[{i}] = {p?.GetType().Name} flow={flowName} exits={exitCount}", this);
                }

            // Signals 注册
            if (Signals != null)
                foreach (var s in Signals) s?.OnAttach(this);

            // 进入第一个阶段
            if (!_stopped && Phases != null && Phases.Length > 0) RequestPhase(0);
            else Debug.LogError($"[__BOSSDEBUG__] Phases is empty! BossController will not enter any phase.", this);
        }

        void Update()
        {
            // 已 stopped → 不再跑 phase / signal tick,直到 GameObject 被 Boss 总控销毁。
            if (_stopped) return;
            if (Health != null && Health.IsDead) return;
            if (_barTransitionRequested)
            {
                _barTransitionRequested = false;
                NextPhase();
            }
            if (_stopped) return;
            if (_pendingPhase >= 0) { AdvanceTransition(); return; }

            // 1. tick 所有 signals(累加内部计数等)
            if (Signals != null)
                for (int i = 0; i < Signals.Length; i++)
                    if (Signals[i] != null) Signals[i].Tick(this, Time.deltaTime);

            // 2. tick 当前 phase
            if (_current == null) return;

            _current.OnTick(transform, Time.deltaTime);
            if (_stopped || _current == null) return;

            // 3. 判定是否该切走
            bool shouldExit = _current.ShouldExit(this);

            // __BOSSDEBUG__ #1:每帧打印 signal 当前值 + 是否触发切阶段
            if (Signals != null && Signals.Length > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[__BOSSDEBUG__] t={Time.time:F2} phase={_phaseIdx} curHp%=");
                if (Health != null) sb.Append(Health.CurrentBarPercent.ToString("F1"));
                sb.Append(" signals=[");
                for (int i = 0; i < Signals.Length; i++)
                {
                    var s = Signals[i];
                    if (s == null) { sb.Append("null,"); continue; }
                    sb.Append($"{s.GetType().Name}={s.CurrentValue:F2},");
                }
                sb.Append($"] shouldExit={shouldExit}");
                Debug.Log(sb.ToString(), this);
            }

            if (shouldExit) NextPhase();
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
            _pendingPhase = -1;
            _phaseGate?.Cancel();
            StartGate?.Cancel();

            // 1. 走当前 phase 收尾
            ExitCurrentPhase(Health != null && Health.IsDead ? CommandInvocation.BossDeath : CommandInvocation.Stopped);

            // 2. 一次性广播 Defeated(给"解锁下一关 / UI 提示"等订阅)
            if (!_defeated)
            {
                _defeated = true;
                LevelController.Instance?.NotifyBossDefeated(gameObject);
            }
            // 3. 销毁由 Boss 总控 HandleDeath 负责(语义对齐 Enemy 总控 Stop + Destroy 的两步式)。
        }

        /// <summary>
        /// 按索引查 Signal(Null-safe)。PhaseTrigger 在 Inspector 里直接持有 BossSignal 实例,
        /// 多数情况下不需要走这个 API;保留给"按数组下标动态查 signal"的外部代码用。
        /// </summary>
        public BossSignal GetSignal(int index) =>
            (Signals != null && index >= 0 && index < Signals.Length) ? Signals[index] : null;

        void EnterPhase(int idx)
        {
            _phaseIdx = idx;
            _current = Phases[idx];
            // __BOSSDEBUG__ #2:阶段进入
            Debug.Log($"[__BOSSDEBUG__] EnterPhase idx={idx} type={_current?.GetType().Name}", this);

            // PhaseTimeSignal 在进入新阶段时重置
            if (Signals != null)
                foreach (var s in Signals)
                    if (s is PhaseTimeSignal pts) pts.Reset();

            GlobalCommandExecutor.Execute(_current?.EnterCommands, new GlobalCommandContext(transform, this));
            _current?.OnEnter(transform);
            if (_stopped) return;
            OnPhaseEntered?.Invoke(_phaseIdx, _current);
        }

        void NextPhase()
        {
            if (_stopped) return;
            // __BOSSDEBUG__ #3:阶段切走(进 NextPhase 说明 ShouldExit 已为 true)
            Debug.Log($"[__BOSSDEBUG__] NextPhase from idx={_phaseIdx}", this);

            ExitCurrentPhase(CommandInvocation.PhaseCompleted);

            int next = _phaseIdx + 1;
            if (_stopped) return;
            if (next >= Phases.Length)
            {
                if (Loop && Phases.Length > 0) next = 0;
                else
                {
                    Debug.Log($"[__BOSSDEBUG__] All phases exhausted (idx={_phaseIdx})", this);
                    _current = null;
                    return;
                }
                // 注:Phases 全部跑完后 Boss 不会自动死亡,也不会广播 OnBossDefeated。
                // 需要"切完所有阶段强制死亡"的效果,要么在最后阶段配一个 HP 阈值 ExitTrigger,
                // 要么在 LevelController.OnLevelComplete 兜底处理。
            }
            if (next < Phases.Length) RequestPhase(next);
        }

        void ExitCurrentPhase(CommandInvocation invocation)
        {
            if (_current == null) return;
            var exiting = _current;
            _current = null;
            OnPhaseExited?.Invoke(_phaseIdx, exiting);
            exiting.OnExit(transform);
            GlobalCommandExecutor.Execute(exiting.ExitCommands, new GlobalCommandContext(transform, this, invocation));
            if (!_stopped || (Health != null && Health.IsDead))
                _phaseGate = PhaseActions?.Invoke(_phaseIdx, false);
        }

        void RequestPhase(int index)
        {
            _pendingPhase = index;
            _enterPrepared = false;
            AdvanceTransition();
        }

        void AdvanceTransition()
        {
            if (_stopped || _pendingPhase < 0) return;
            if (StartGate != null && !StartGate.IsComplete) return;
            if (_phaseGate != null && !_phaseGate.IsComplete) return;
            if (!_enterPrepared)
            {
                _enterPrepared = true;
                _phaseGate = PhaseActions?.Invoke(_pendingPhase, true);
                if (_stopped || (_phaseGate != null && !_phaseGate.IsComplete)) return;
            }
            int index = _pendingPhase;
            _pendingPhase = -1;
            EnterPhase(index);
        }
    }
}
