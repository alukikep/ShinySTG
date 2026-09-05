using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 全局开火计数器。BulletPool.FireGroup 末尾会自动调用 OnBossFired。
    /// ShotsFiredSignal 读这个 Total。挂在 boss GameObject(或任意常驻对象)上即可。
    /// 不存在时 BulletPool 的 ?. 安全跳过。
    /// </summary>
    public class BossShotCounter : MonoBehaviour
    {
        public static BossShotCounter Instance;

        public int Total = 0;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void OnBossFired(FirePattern pattern)
        {
            if (pattern == null) return;
            Total += pattern.GetFireCount();
        }

        public void Reset() { Total = 0; }
    }
}
