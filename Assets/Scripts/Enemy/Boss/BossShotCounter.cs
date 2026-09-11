using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 全局开火计数器。BulletPool.FireGroup 末尾会自动调用 OnBossFired。
    /// ShotsFiredSignal 读这个 Total。
    ///
    /// 挂载方式:场景里**单独挂一份**(推荐命名 "BossShotCounter")。
    /// 不再 [RequireComponent] 挂在 Boss prefab 上 —— 之前的全局静态 Instance + RequireComponent
    /// 组合在"同场景多 Boss" / "Boss 多次入场销毁" 场景下会把 Instance 误清成 null,
    /// 导致后续 Boss 的开火计数失效。改用 Singleton&lt;T&gt; 后,
    /// 重复挂载自动 Destroy(只留第一份),BulletPool 钩子读 Instance 永远稳。
    ///
    /// 协作边界:
    ///   - ShotsFiredSignal 不再读静态 Instance,而是通过 OnAttach(boss) 拿引用,
    ///     与其它 Signal 风格统一。
    ///   - BulletPool 的 BossShotCounter.Instance?.OnBossFired(pattern) 钩子保留
    ///     (场景里没挂时安全跳过,不影响普通敌人)。
    /// </summary>
    public class BossShotCounter : Singleton<BossShotCounter>
    {
        public int Total = 0;

        public void OnBossFired(FirePattern pattern)
        {
            if (pattern == null) return;
            Total += pattern.GetFireCount();
        }

        public void Reset() { Total = 0; }
    }
}
