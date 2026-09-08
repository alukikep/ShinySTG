using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 持续按 FireRate 发射 FirePattern 指定的子弹组。
    /// 行为持续 Duration 秒后停止,自动进入下一条。
    /// </summary>
    [Serializable, SRName("Action/Fire")]
    public class FireAction : EnemyAction
    {
        [Tooltip("拖拽一个 FirePattern 资产(Ring/Line/Arc/Composite/...)。")]
        public FirePattern Pattern;

        [Tooltip("每秒发射次数。< =0 不会发射。")]
        public float FireRate = 2f;

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

        public override void OnEnter(Transform enemy)
        {
            _timer = 0f;
            _running = true;
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (!_running) return;
            if (Pattern == null || BulletPool.Instance == null) return;

            _timer -= dt;
            if (_timer > 0f) return;
            _timer = 1f / Mathf.Max(0.0001f, FireRate);

            float rotationRad = AimOffsetDeg * Mathf.Deg2Rad;
            // 读 enemy 上的 EnemyHitbox 作为 ownerHitbox(透传给子弹阵营)
            // 没挂时传 null → 子弹阵营 = Neutral(不参与碰撞,安全兜底)
            var ownerHitbox = enemy != null
                ? enemy.GetComponent<ShinySTG.Hitbox.HitboxComponent>()
                : null;
            BulletPool.Instance.FireGroup(Pattern, enemy.position, rotationRad, ownerHitbox, ExtraModifierPrefabs);
        }

        public override void OnExit(Transform enemy)
        {
            _running = false;
        }
    }
}
