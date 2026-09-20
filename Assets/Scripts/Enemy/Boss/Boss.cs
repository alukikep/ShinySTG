using UnityEngine;
using ShinySTG.Level;
using ShinySTG.Level.Encounter;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>Boss 实体入口。场景放置时自行驱动遭遇，关卡生成时由关卡驱动。</summary>
    [RequireComponent(typeof(BossHealth))]
    [RequireComponent(typeof(BossHitbox))]
    [RequireComponent(typeof(BossController))]
    public class Boss : MonoBehaviour
    {
        public BossHealth      Health    { get; private set; }
        public BossHitbox      Hitbox    { get; private set; }
        public BossController  Controller{ get; private set; }

        [Tooltip("直接放入场景时使用的遭遇配置；关卡生成时使用 Entry 指定的配置。")]
        public BossEncounterDefinition Encounter;
        BossEncounterRuntime _encounterRuntime;
        public bool HasEncounter => _encounterRuntime != null;

        public void BindEncounter(BossEncounterRuntime runtime, BossEncounterDefinition definition)
        {
            if (_encounterRuntime != null) throw new System.InvalidOperationException("Boss 已绑定遭遇。");
            _encounterRuntime = runtime;
            Encounter = definition;
        }

        void Start()
        {
            if (_encounterRuntime != null) return;
            if (Encounter == null)
            {
                Debug.LogError("[Boss] 请指定 Encounter 配置，或通过 BossEncounterEntry 生成。", this);
                return;
            }
            _encounterRuntime = new BossEncounterRuntime(Encounter, gameObject, false);
            var host = new GameObject("Boss Encounter Runtime").AddComponent<BossEncounterHost>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host.gameObject, gameObject.scene);
            host.Initialize(_encounterRuntime);
            LevelController.Instance?.NotifyBossSpawned(gameObject);
        }

        bool _dead;
        public bool RetainForDefeatActions { get; set; }

        void Awake()
        {
            Health     = GetComponent<BossHealth>();
            Hitbox     = GetComponent<BossHitbox>();
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

            // __BOSSDEBUG__ #8:Boss 死亡路径
            Debug.Log($"[__BOSSDEBUG__] Boss.HandleDeath @ t={Time.time:F2} name={name}", this);

            // 1. 停阶段(走当前 phase.OnExit + 广播 Defeated,语义对齐 ShooterEnemy.Stop)
            if (Controller != null) Controller.Stop();

            // Encounter 负责击破演出和实体销毁。
            if (!RetainForDefeatActions) Destroy(gameObject);
        }
    }
}
