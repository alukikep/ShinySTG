using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 持续按 FireRate 发射 FirePattern 指定的子弹组。
    /// 行为持续 Duration 秒后停止,自动进入下一条。
    ///
    /// <para>★ OneShot 模式(vX 起):</para>
    /// <para>
    /// OneShot=true 时,忽略 FireRate,仅在 <see cref="OnEnter"/> 触发时调用一次
    /// <see cref="BulletPool.FireGroup"/>,剩余时间由 DurationConfig 占用,不再重复开火。
    /// 典型用法:Boss 一次性放 24 颗散弹后停下喘气 2 秒 → OneShot=true + Duration=2。
    /// 默认 OneShot=false,行为与历史 100% 等价。
    /// </para>
    /// </summary>
    [Serializable, SRName("Action/Fire")]
    public class FireAction : EnemyAction
    {
        [Tooltip("拖拽一个 FirePattern 资产(Ring/Line/Arc/Composite/...)。")]
        public FirePattern Pattern;

        [Tooltip("每秒发射次数。< =0 不会发射。\n" +
                 "OneShot=true 时本字段被忽略,只在 OnEnter 触发一次。")]
        public float FireRate = 2f;

        [Header("Fire Mode")]
        [Tooltip("true = 进入该 Action 时只开火一次(FireRate 被忽略),剩余时间由 DurationConfig 占用,不重复开火。\n" +
                 "false = 按 FireRate 持续节流开火(默认,与历史行为一致)。\n" +
                 "典型用法:Boss 一次性放 24 颗散弹后停下喘气 2 秒,OneShot=true + Duration=2。\n" +
                 "Loop=true 时,每次循环回到本 Action 会再次 OnEnter → OneShot=true 也会再次只开一次火。")]
        public bool OneShot = false;

        [Tooltip("相对 Pattern.BaseAngle 的额外偏移(度)。0 = 完全交给 Pattern 的 BaseAngle。")]
        public float AimOffsetDeg = 0f;

        [Header("Extra Modifiers (在 Pattern 默认 modifier 之上额外追加)")]
        [Tooltip("这个 Action 触发时,会在 Pattern.ModifierPrefabs 之外再附加这些 modifier。\n" +
                 "适用场景:同一 Pattern 在不同 Action/阶段切换成追踪弹/加速弹(不改 SO 资产)。\n" +
                 "留空 = 只用 Pattern 自带的 modifier。\n" +
                 "下拉选 modifier 类型(走 SerializeReference + SRName),直接编辑字段。")]
        [SerializeReference, SR]
        public BulletModifier[] ExtraModifierPrefabs;

        float _timer;
        bool _running;
        FirePatternRuntimeState _runtimeState;

        public override void OnEnter(Transform enemy)
        {
            _timer = 0f;
            _running = true;
            _runtimeState ??= new FirePatternRuntimeState();
            _runtimeState.Reset();

            // OneShot 模式:进入时立刻开一次火,剩余时间由 Duration 占用(由 BehaviorFlowRuntime 计时)
            if (OneShot) FireOnce(enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (!_running) return;
            // OneShot 已在 OnEnter 开过一次 → 整段 Action 期间不再触发 FireGroup
            if (OneShot) return;
            if (Pattern == null || BulletPool.Instance == null) return;

            int bursts = FireCadence.Tick(ref _timer, FireRate, dt);
            for (int i = 0; i < bursts; i++) FireOnce(enemy);
        }

        public override void OnExit(Transform enemy)
        {
            _running = false;
            _runtimeState?.Reset();
        }

        /// <summary>
        /// 抽取出来的"单次开火"实现,被 OnEnter(OneShot 路径)与 OnTick(节流路径)共用,
        /// 保证两条路径在 Pattern / AimOffset / ExtraModifier / 阵营透传上行为 100% 一致。
        /// </summary>
        void FireOnce(Transform enemy)
        {
            if (enemy == null) return;
            if (Pattern == null || BulletPool.Instance == null) return;

            float rotationRad = AimOffsetDeg * Mathf.Deg2Rad;
            // 读 enemy 上的 EnemyHitbox 作为 ownerHitbox(透传给子弹阵营)
            // 没挂时传 null → 子弹阵营 = Neutral(不参与碰撞,安全兜底)
            var ownerHitbox = enemy != null
                ? enemy.GetComponent<ShinySTG.Hitbox.HitboxComponent>()
                : null;
            BulletPool.Instance.FireGroup(Pattern, enemy.position, rotationRad, ownerHitbox, ExtraModifierPrefabs, _runtimeState);
        }
    }
}
