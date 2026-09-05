using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 当前血管的编号(从 0 开始)。
    /// 用途:打完第 N 管切阶段。单调递增,不会被"一帧穿多阶段"。
    /// </summary>
    [Serializable, SRName("Signal/Current Bar Index")]
    public class CurrentBarIndexSignal : BossSignal
    {
        BossHealth _health;

        public override void OnAttach(BossController boss)
        {
            _health = boss != null ? boss.Health : null;
        }

        public override float CurrentValue =>
            _health != null ? _health.CurrentBarIndex : 0f;
    }
}
