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
            BulletPool.Instance.FireGroup(Pattern, enemy.position, rotationRad);
        }

        public override void OnExit(Transform enemy)
        {
            _running = false;
        }
    }
}
