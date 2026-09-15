using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.BulletCore; // BulletSignalBus

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 在 BehaviorFlow 时间轴上某个时间点发射一个具名信号。
    /// 用于「敌人在某个 Action 节奏上给子弹 modifier 发号施令」,把 BehaviorFlow 的 AI 节奏
    /// 和 BulletModifier 的激活时机打通,详见 ARCHITECTURE.md §2.8。
    ///
    /// <para>★ 与既有 Action 的协作边界:</para>
    /// <list type="bullet">
    ///   <item>自身不发射子弹、不移动敌人、不影响其他 Action —— 只是发一条全局信号。</item>
    ///   <item>与 <see cref="FireAction"/> 协作:Parallel 容器内同帧 <c>Fire + EmitSignal</c>,
    ///         所有「那批新生成的弹」立刻激活(例:Boss 喊「fire!」+ 全场开火 → 全部弹立即切换追踪 modifier)。</item>
    ///   <item>与 <see cref="SequenceAction"/> 协作:序列第 N 步 → EmitSignal("phase2_start") → 全部敌人级联切换 modifier。</item>
    /// </list>
    ///
    /// <para>★ 典型用法:</para>
    /// <list type="bullet">
    ///   <item>Boss 蓄力 Action 的 OnEnter 触发 "boss_charge_finished" → 子弹分裂 modifier 立即开火(替代「凑 Delay 时间」对位)。</item>
    ///   <item>残血阶段切换时:Phase2 的 OnEnter 触发 "boss_enrage" → 全场追踪弹切直线 + 染色红。</item>
    ///   <item>EveryTick 模式:持剑敌人挥剑动作期间每帧触发 "swinging" → 弹幕随剑势摆动。</item>
    /// </list>
    ///
    /// <para>★ 命名建议:</para>
    /// <para>
    /// 信号名 = string,大小写敏感。建议规范:&lt;场景/敌人类型&gt;_&lt;语义&gt;_&lt;时机&gt;,
    /// 如 <c>boss_yell_charge_done</c> / <c>player_fire</c> / <c>wave_3_start</c>。
    /// 策划自由命名,无须登记资产(对比 Timeline SignalReceiver 的反射枚举登记流程)。
    /// </para>
    /// </summary>
    [Serializable, SRName("Action/Emit Signal")]
    public class EmitSignalAction : EnemyAction
    {
        /// <summary>发射时机。</summary>
        public enum EmitMode
        {
            /// <summary>进入 Action 时发射一次。默认,最常用。</summary>
            OnEnterOnly,
            /// <summary>每帧发射(慎用,信号风暴)。用于「持续按住蓄力」语义。</summary>
            EveryTick,
            /// <summary>按 <see cref="EmitInterval"/> 秒发射一次(从 OnEnter 起)。</summary>
            OnInterval
        }

        [Tooltip("要发射的信号名。BulletSignalBus.Emit 时按 string 匹配(大小写敏感)。\n" +
                 "留空 = 不发射(调试用)。\n" +
                 "★ SignalBus.Emit 对 null/空 静默,这里留空不会崩溃 —— 只是没效果。\n" +
                 "命名建议:'<场景/敌人类型>_<语义>_<时机>',如 'boss_yell_charge_done'。")]
        public string SignalName;

        [Tooltip("发射时机:\n" +
                 "  OnEnterOnly(默认) = 进入 Action 时发射一次(最常用,大多数'喊话'场景);\n" +
                 "  EveryTick = 每帧发射(慎用,易产生信号风暴。仅'持续按住蓄力'语义);\n" +
                 "  OnInterval = 按 EmitInterval 秒发射(从 OnEnter 起),适合'持续呼吸/光效闪烁'语义。")]
        public EmitMode Mode = EmitMode.OnEnterOnly;

        [Tooltip("Mode=OnInterval 时,每隔多少秒发射一次。\n" +
                 "OnEnter 后立即发射第一次(不等满 Interval)。")]
        [Min(0.01f)] public float EmitInterval = 0.5f;

        [Tooltip("信号源位置偏移(相对敌人 transform 的局部坐标)。\n" +
                 "(0,0) = 敌人位置(默认);非 0 = 偏移后的位置,供 modifier 做距离判定(Trigger/On Signal 的 RequireInRange)。")]
        public Vector2 OriginLocalOffset = Vector2.zero;

        [Tooltip("是否把发射时刻记录到控制台(Editor 调试用)。\n" +
                 "PlayMode 下信号发射频繁时建议关,免得 Console 刷屏。")]
        public bool DebugLog = false;

        float _nextEmitTimer;

        public override void OnEnter(Transform enemy)
        {
            _nextEmitTimer = EmitInterval;
            // 默认模式 OnEnterOnly:进入就发射一次。
            if (Mode == EmitMode.OnEnterOnly) DoEmit(enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            switch (Mode)
            {
                case EmitMode.EveryTick:
                    DoEmit(enemy);
                    break;
                case EmitMode.OnInterval:
                    _nextEmitTimer -= dt;
                    if (_nextEmitTimer <= 0f)
                    {
                        DoEmit(enemy);
                        _nextEmitTimer = EmitInterval;
                    }
                    break;
                case EmitMode.OnEnterOnly:
                    // OnEnter 已经发过了,OnTick 不重复发。
                    break;
            }
        }

        /// <summary>
        /// 真正发射信号的位置。Origin = enemy.TransformPoint(OriginLocalOffset)。
        /// 敌人 transform 为 null 时(理论上不应发生,BulletPool.FireGroup 已做 null 防御),
        /// 退回到 OriginLocalOffset 自身(此时 Origin = (0,0) 也是合理的兜底)。
        /// </summary>
        void DoEmit(Transform enemy)
        {
            if (string.IsNullOrWhiteSpace(SignalName)) return;

            Vector2 origin;
            if (enemy != null)
            {
                // TransformPoint 把局部坐标转世界坐标 —— 与 FireAction 算 ownerHitbox 的位置同套路。
                Vector3 worldOffset = enemy.TransformPoint(
                    new Vector3(OriginLocalOffset.x, OriginLocalOffset.y, 0f));
                origin = new Vector2(worldOffset.x, worldOffset.y);
            }
            else
            {
                origin = OriginLocalOffset;
            }

            BulletSignalBus.Emit(SignalName, origin);

            if (DebugLog)
            {
                Debug.Log($"[EmitSignalAction] {SignalName} @ frame {Time.frameCount}, origin={origin}",
                          enemy);
            }
        }

        public override void OnExit(Transform enemy)
        {
            // EmitSignalAction 不持跨帧状态(OnEnter 已 reset _nextEmitTimer),OnExit 无需清理。
        }
    }
}
