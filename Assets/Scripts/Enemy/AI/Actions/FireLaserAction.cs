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
    ///   - 字段几乎完全镜像(Pattern / FireRate / AimOffsetDeg / ExtraModifierPrefabs),
    ///     设计师切换两种 Action 类型零学习成本。
    /// </summary>
    [Serializable, SRName("Action/Fire Laser")]
    public class FireLaserAction : EnemyAction
    {
        [Tooltip("拖一个 LaserPattern 资产(Straight / Curved / 未来扩展)。\n" +
                 "右键 Project → Create → STG → Laser → Pattern/Straight 创建。")]
        public LaserPattern Pattern;

        [Tooltip("每秒发射次数。< =0 不会发射。")]
        public float FireRate = 1f;

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
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (!_running) return;
            if (Pattern == null || LaserPool.Instance == null) return;

            _timer -= dt;
            if (_timer > 0f) return;
            _timer = 1f / Mathf.Max(0.0001f, FireRate);

            float angleRad = AimOffsetDeg * Mathf.Deg2Rad;
            // 读 enemy 上的 Hitbox 作为 ownerHitbox(透传给激光阵营)
            // 没挂时传 null → 阵营 = Neutral(不参与碰撞,安全兜底)
            var ownerHitbox = enemy != null
                ? enemy.GetComponent<HitboxComponent>()
                : null;
            LaserPool.Instance.FireGroup(Pattern, enemy.position, angleRad, ownerHitbox, ExtraModifierPrefabs);
        }

        public override void OnExit(Transform enemy)
        {
            _running = false;
        }
    }
}
