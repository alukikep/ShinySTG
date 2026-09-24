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
    ///   - 自毁 / 出界 / 清场指令走 SelfDestruct，不触发死亡奖励
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
        bool _enteredCullingArea;
        float _outsideAge;
        [SerializeField, Tooltip("出界时自毁，不触发死亡掉落。边界使用 BoundsService.CullingArea。")]
        bool _selfDestructOutOfBounds = true;
        [SerializeField, Min(0f), Tooltip("从边界外生成时允许入场的秒数；进入边界后不再提供宽限。")]
        float _entryGraceSeconds = 5f;
        [SerializeField, Tooltip("被击杀时的掉落；离场自毁不触发。留空不掉落。")]
        ShinySTG.Items.DropProfile _deathDrops;
        [SerializeField, Tooltip("击杀或启用死亡特效的清除指令使用的独立特效；留空不播放。")]
        GameObject _deathEffectPrefab;

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
            _enteredCullingArea = IsInsideCullingArea();
            _outsideAge = 0f;
            if (Health != null) Health.OnDeath += HandleDeath;
        }

        void LateUpdate()
        {
            if (_dead || !_selfDestructOutOfBounds) return;
            if (IsInsideCullingArea())
            {
                _enteredCullingArea = true;
                return;
            }
            _outsideAge += Time.deltaTime;
            if (_enteredCullingArea || _outsideAge >= Mathf.Max(0f, _entryGraceSeconds))
                SelfDestruct();
        }

        bool IsInsideCullingArea()
        {
            var bounds = ShinySTG.Stage.BoundsService.Instance;
            Vector2 position = transform.position;
            return bounds != null ? bounds.ContainsCulling(position)
                : Mathf.Abs(position.x) <= 10f && Mathf.Abs(position.y) <= 20f;
        }

        /// <summary>无奖励离场；立即撤下碰撞和活跃登记，帧末销毁，不受无敌影响。</summary>
        public void SelfDestruct() => SelfDestruct(false);

        public void SelfDestruct(bool playDeathEffect)
        {
            if (_dead) return;
            _dead = true;
            if (playDeathEffect)
                ShinySTG.Effects.EffectPool.Play(_deathEffectPrefab, transform.position, gameObject.scene);
            gameObject.SetActive(false);
            if (Shooter != null) Shooter.Stop();
            Destroy(gameObject);
        }

        void OnDisable()
        {
            if (Health != null) Health.OnDeath -= HandleDeath;
        }

        /// <summary>强制击杀，不受无敌影响；复用 Health 的死亡通知与奖励结算。</summary>
        public void KillWithRewards()
        {
            if (_dead || !isActiveAndEnabled || Health == null) return;
            Health.Kill();
        }

        void HandleDeath()
        {
            if (_dead) return;
            _dead = true;
            Vector2 dropPosition = transform.position;
            Vector3 deathPosition = transform.position;

            // 立即撤下碰撞和活跃登记，避免帧末销毁前再次成为清场目标。
            gameObject.SetActive(false);

            // 停掉行为流(强制退出当前 action,避免 BehaviorFlowRuntime 状态悬挂)
            if (Shooter != null) Shooter.Stop();
            ShinySTG.Items.ItemDropService.Spawn(_deathDrops, dropPosition);
            ShinySTG.Effects.EffectPool.Play(_deathEffectPrefab, deathPosition, gameObject.scene);

            // 特效独立运行，不延长敌人的战斗生命周期。
            Destroy(gameObject);
        }
    }
}
