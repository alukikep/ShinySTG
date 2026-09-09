using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss 总控。参照 Enemy.cs 设计:不做具体逻辑,只把子组件串起来,
    /// 给外部系统(关卡 / 子弹碰撞 / UI)一个稳定的访问点。
    ///
    /// 预 prefab 组件构成(对齐 Enemy 子树):
    ///   Boss 总控(本组件)
    ///     ├─ BossHealth       (HP + 多管血 + IsDead + OnDeath 事件)
    ///     ├─ BossHitbox       (HitboxComponent 子类,Team=Enemy)
    ///     ├─ BossShotCounter  (全局开火计数)
    ///     └─ BossController   (阶段协调器 + Signal)
    ///
    /// 协作边界(对齐 Enemy):
    ///   - Boss 总控是公共访问入口:外部系统通过 boss.Health / boss.Hitbox / boss.Controller 访问
    ///   - 总控 Awake 自动注入 Health.Hitbox = Hitbox(无需 Inspector 手填)
    ///   - 总控订阅 Health.OnDeath 统一收尾(Stop + Destroy),语义对齐 Enemy.HandleDeath
    ///   - BossController 自己不订阅 OnDeath,只暴露 Stop() 由总控在收尾时调用
    ///
    /// 死亡收尾流程:
    ///   1. 玩家弹打空 BossHealth 所有血管 → BossHealth 触发 OnDeath
    ///   2. 本组件订阅后 HandleDeath():调 Controller.Stop() 走阶段收尾 + Destroy(gameObject)
    ///   3. Controller.Stop() 内部:phase.OnExit + 广播 LevelController.NotifyBossDefeated + 标 stopped
    ///   4. Boss 总控 HandleDeath 调 Destroy — GameObject 销毁,所有组件随之销毁
    /// </summary>
    [RequireComponent(typeof(BossHealth))]
    [RequireComponent(typeof(BossHitbox))]
    [RequireComponent(typeof(BossShotCounter))]
    [RequireComponent(typeof(BossController))]
    public class Boss : MonoBehaviour
    {
        public BossHealth      Health    { get; private set; }
        public BossHitbox      Hitbox    { get; private set; }
        public BossShotCounter ShotCount { get; private set; }
        public BossController  Controller{ get; private set; }

        bool _dead;

        void Awake()
        {
            Health     = GetComponent<BossHealth>();
            Hitbox     = GetComponent<BossHitbox>();
            ShotCount  = GetComponent<BossShotCounter>();
            Controller = GetComponent<BossController>();

            // 把 Hitbox 组件灌给 BossHealth(总控统一管理,BossHealth.Hitbox 不再需要 Inspector 手填)
            if (Health != null && Hitbox != null) Health.Hitbox = Hitbox;
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

            // 1. 停阶段(走当前 phase.OnExit + 广播 Defeated,语义对齐 ShooterEnemy.Stop)
            if (Controller != null) Controller.Stop();

            // 2. 兜底:本版本直接销毁。后续要做死亡动画/撒豆,在这里替换成协程即可。
            Destroy(gameObject);
        }
    }
}