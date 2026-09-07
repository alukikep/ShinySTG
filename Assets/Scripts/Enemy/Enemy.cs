using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 敌人总控。协调 Health / ShooterEnemy / Hitbox,作为公共访问入口。
    ///
    /// 参照 Player.cs 的设计:不做具体逻辑,只把子组件串起来,
    /// 给外部系统(EnemySpawner / 子弹碰撞 / 计分)一个稳定的访问点。
    /// 不用 Singleton —— 敌人可能很多,Scene 里同时存在 N 个 Enemy 不该共享 Instance。
    ///
    /// 生命周期的处理:
    ///   - 行为流跑完时(SelfDestructAction)= ShooterEnemy 自己 Destroy 自己(沿用旧行为)
    ///   - HP 打空时(EnemyHealth.OnDeath)= 本组件统一处理:停行为流 + Destroy
    ///
    /// 后续如果要做"死亡动画播完再销毁 / 撒豆再销毁",
    /// 只需在本组件 HandleDeath 里加 DOTween / 协程 / Invoke,不动 Health 不动 ShooterEnemy。
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(ShooterEnemy))]
    [RequireComponent(typeof(EnemyHitbox))]
    public class Enemy : MonoBehaviour
    {
        public EnemyHealth  Health  { get; private set; }
        public ShooterEnemy Shooter { get; private set; }
        public EnemyHitbox  Hitbox  { get; private set; }

        bool _dead;

        void Awake()
        {
            Health  = GetComponent<EnemyHealth>();
            Shooter = GetComponent<ShooterEnemy>();
            Hitbox  = GetComponent<EnemyHitbox>();

            // 把 Hitbox 组件灌给 EnemyHealth(总控统一管理,EnemyHealth.Hitbox 不再需要 Inspector 手填)
            if (Hitbox != null) Health.Hitbox = Hitbox;
        }

        void OnEnable()
        {
            if (Health != null) Health.OnDeath += HandleDeath;
        }

        void OnDisable()
        {
            if (Health != null) Health.OnDeath -= HandleDeath;
        }

        void HandleDeath()
        {
            if (_dead) return;
            _dead = true;

            // 停掉行为流(强制退出当前 action,避免 BehaviorFlowRuntime 状态悬挂)
            if (Shooter != null) Shooter.Stop();

            // 兜底:本版本直接销毁。后续要做死亡动画/撒豆,在这里替换成协程即可。
            Destroy(gameObject);
        }
    }
}