using System;
using SerializeReferenceEditor;
using UnityEngine;
using ShinySTG.Hitbox;
using ShinySTG.Laser;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 持续按 FireRate 触发 LaserPattern。
    /// 对齐 <see cref="FireAction"/> 的形态,但走 LaserPool.FireGroup 而非 BulletPool.FireGroup。
    /// Boss/敌人在 BehaviorFlow 时间轴上挂一条 FireLaserAction 即可批量放激光。
    ///
    /// 与 FireAction 的关系:
    ///   - 同级,平行运行,可同时挂进 BehaviorFlow.Actions(一个发子弹,一个发激光)。
    ///   - 字段几乎完全镜像(Pattern / FireRate / OneShot / AimOffsetDeg / ExtraModifierPrefabs),
    ///     设计师切换两种 Action 类型零学习成本。
    ///
    /// <para>★ OneShot 模式(vX 起):</para>
    /// <para>
    /// OneShot=true 时,忽略 FireRate,仅在 <see cref="OnEnter"/> 触发时调用一次
    /// <see cref="LaserPool.FireGroup"/>,剩余时间由 DurationConfig 占用,不再重复开火。
    /// 默认 OneShot=false,行为与历史 100% 等价。
    /// </para>
    /// </summary>
    [Serializable, SRName("Action/Fire Laser")]
    public class FireLaserAction : EnemyAction
    {
        [Tooltip("拖一个 LaserPattern 资产(Straight / Curved / 未来扩展)。\n" +
                 "右键 Project → Create → STG → Laser → Pattern/Straight 创建。")]
        public LaserPattern Pattern;

        [Tooltip("每秒发射次数。< =0 不会发射。\n" +
                 "OneShot=true 时本字段被忽略,只在 OnEnter 触发一次。")]
        public float FireRate = 1f;

        [Header("Fire Mode")]
        [Tooltip("true = 进入该 Action 时只放一次激光(FireRate 被忽略),剩余时间由 DurationConfig 占用,不重复开火。\n" +
                 "false = 按 FireRate 持续节流放激光(默认,与历史行为一致)。\n" +
                 "典型用法:Boss 蓄力后只放一道激光 + 停顿 2 秒,OneShot=true + Duration=2。\n" +
                 "Loop=true 时,每次循环回到本 Action 会再次 OnEnter → OneShot=true 也会再次只放一次。")]
        public bool OneShot = false;

        [Tooltip("相对激光中线方向的额外旋转(度)。\n" +
                 "0 = 完全交给 Pattern 的方向;90 = Pattern 方向再顺时针 90°。")]
        public float AimOffsetDeg = 0f;

        [Header("Extra Modifiers")]
        [Tooltip("在 Pattern.ModifierPrefabs 之外追加(对齐 FireAction.ExtraModifierPrefabs)。\n" +
                 "适用场景:同一 Pattern 在不同 Action 切换成'旋转激光 / 追踪 Boss 激光'(不改 SO 资产)。\n" +
                 "下拉选 LaserModifier 子类(走 SerializeReference + SRName)。")]
        [SerializeReference, SR]
        public LaserModifier[] ExtraModifierPrefabs;

        float _timer;
        bool _running;

        public override void OnEnter(Transform enemy)
        {
            _timer = 0f;
            _running = true;

            // OneShot 模式:进入时立刻放一次激光,剩余时间由 Duration 占用(由 BehaviorFlowRuntime 计时)
            if (OneShot) FireOnce(enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (!_running) return;
            // OneShot 已在 OnEnter 放过一次 → 整段 Action 期间不再触发 FireGroup
            if (OneShot) return;
            if (Pattern == null || LaserPool.Instance == null) return;

            int bursts = FireCadence.Tick(ref _timer, FireRate, dt);
            for (int i = 0; i < bursts; i++) FireOnce(enemy);
        }

        public override void OnExit(Transform enemy)
        {
            _running = false;
        }

        /// <summary>
        /// 抽取出来的"单次放激光"实现,被 OnEnter(OneShot 路径)与 OnTick(节流路径)共用,
        /// 保证两条路径在 Pattern / AimOffset / ExtraModifier / 阵营透传上行为 100% 一致。
        /// </summary>
        void FireOnce(Transform enemy)
        {
            if (enemy == null) return;
            if (Pattern == null || LaserPool.Instance == null) return;

            float angleRad = AimOffsetDeg * Mathf.Deg2Rad;
            // 读 enemy 上的 Hitbox 作为 ownerHitbox(透传给激光阵营)
            // 没挂时传 null → 阵营 = Neutral(不参与碰撞,安全兜底)
            var ownerHitbox = enemy != null
                ? enemy.GetComponent<HitboxComponent>()
                : null;
            LaserPool.Instance.FireGroup(Pattern, enemy.position, angleRad, ownerHitbox, ExtraModifierPrefabs);
        }
    }
}
