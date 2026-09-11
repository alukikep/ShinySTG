using System;
using SerializeReferenceEditor;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// 读 BossShotCounter.Total(boss 全局开火累计)。
    ///
    /// 通过 BossController.OnAttach 时缓存 BossController.ShotCount 引用,
    /// 与其它 Signal(HpSignal / BarSignal / ...)的"挂载时拿引用"风格统一。
    /// 场景里没挂 BossShotCounter 时 ShotCount == null,CurrentValue 返回 0 不报错。
    ///
    /// 延迟发现:BossShotCounter 是场景单例,如果 BossController.Awake 跑得比 BossShotCounter.Awake 早,
    /// OnAttach 拿到的 ShotCount 是 null;Tick() 时若发现仍为 null,会再次回头去读 BossController.ShotCount
    /// 重新绑定(场景级 Awake 顺序并不保证)。
    /// </summary>
    [Serializable, SRName("Signal/Shots Fired")]
    public class ShotsFiredSignal : BossSignal
    {
        BossController _boss;
        BossShotCounter _shotCount;

        public override void OnAttach(BossController boss)
        {
            _boss = boss;
            _shotCount = boss != null ? boss.ShotCount : null;
        }

        public override void Tick(BossController boss, float dt)
        {
            // 场景单例可能晚于本 BossController Awake;每帧重新探一次,拿到就绑定。
            if (_shotCount == null && _boss != null) _shotCount = _boss.ShotCount;
        }

        public override float CurrentValue => _shotCount != null ? _shotCount.Total : 0f;
    }
}
