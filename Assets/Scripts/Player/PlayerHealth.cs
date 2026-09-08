using System;
using UnityEngine;
using ShinySTG.Hitbox;  // CollisionService / Bullet (擦弹事件订阅)

namespace ShinySTG.Player
{
    /// <summary>
    /// 玩家残机 + 复活无敌 + 火力级 + 擦弹计数。
    ///
    /// 火力级 (PowerLevel 0..MaxPower):
    ///   - 影响 PlayerShooting 喷射形态
    ///   - 影响 PlayerOptions 解锁多少子机(每 +1 火力解锁下一档)
    /// 残机 (Lives):
    ///   - 0 = 死透了,触发 OnAllLivesLost,默认让 PlayerShooting / PlayerMovement 停摆
    ///   - >0 = 死亡时扣 1,触发 OnLifeLost;若 Lives > 0 自动触发 OnRevive 倒计时复活无敌
    ///
    /// 无敌阶段 (Invincibility):
    ///   - 默认从出生 / 复活开始给一段无敌,撞弹不扣命
    ///   - 期间玩家闪白(可视化留给外部 Renderer,本类只暴露事件)
    ///
    /// 擦弹 (GrazeCount):
    ///   - 由 CollisionService.OnPlayerGrazeByEnemyBullet 事件累加
    ///   - 不影响残机 / 火力 / 无敌,仅作为 STG 经典的高分元素与成就统计
    ///   - 提供 DebugTriggerGraze() 调试入口(跳过真实碰撞直接累加,供 UI 测试用)
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Lives")]
        [Tooltip("初始残机数(含本体,例如 3 = 玩家 + 2 续命)。")]
        public int InitialLives = 3;

        [Tooltip("0 = 无残机,死了不复活,只触发 OnAllLivesLost。")]
        public int Lives { get; private set; }

        [Header("Power")]
        [Range(0, 4)]
        [Tooltip("初始火力级(0..4)。")]
        public int InitialPower = 1;

        [Range(1, 4)]
        [Tooltip("最大火力级。")]
        public int MaxPower = 4;

        [Tooltip("运行时当前火力级(0..MaxPower)。由外部 PowerUp() 提升。")]
        public int PowerLevel { get; private set; }

        [Header("Invincibility")]
        [Tooltip("出生时的无敌时长(秒)。")]
        public float SpawnInvincibleDuration = 3f;

        [Tooltip("复活后的无敌时长(秒)。")]
        public float ReviveInvincibleDuration = 3f;

        [Tooltip("当前无敌剩余时间(秒)。<=0 = 可受伤。")]
        public float InvincibleRemaining { get; private set; }

        [field: Header("Graze")]
        [field: Tooltip("累计擦弹数(被敌人弹擦过判定外圈的次数)。由 CollisionService.OnPlayerGrazeByEnemyBullet 自动累加。\n经典 STG 用法:高分元素、徽章成就、UI 飘字。")]
        [field: SerializeField] public int GrazeCount { get; private set; }

        public bool IsInvincible => InvincibleRemaining > 0f;
        public bool IsDead       => Lives <= 0;

        // ---- 事件(供 UI / 动画 / 子机响应)----
        public event Action OnLifeLost;       // 死亡瞬间(扣命前)
        public event Action OnRevive;         // 复活瞬间
        public event Action OnAllLivesLost;   // 全部耗尽(没复活)
        public event Action<int> OnPowerUp;    // 火力提升(int = 新等级)
        public event Action OnInvincibleStart;
        public event Action OnInvincibleEnd;
        /// <summary>擦弹 +1 触发(int = 累加后的新值)。供 UI / 计分 / 音效订阅。</summary>
        public event Action<int> OnGraze;

        void Awake()
        {
            Lives = Mathf.Max(0, InitialLives);
            PowerLevel = Mathf.Clamp(InitialPower, 0, MaxPower);
            InvincibleRemaining = SpawnInvincibleDuration;
            if (InvincibleRemaining > 0f) OnInvincibleStart?.Invoke();
        }

        void Start()
        {
            // 订阅全局碰撞服务的擦弹事件。CollisionService 在场景里手动挂;
            // 还没初始化时给出警告(LateUpdate 不会跑,Lives 也不会被命中)。
            // 与 CollisionService.Instance 共生命周期:场景切换时 OnDestroy 会自动置 Instance = null,
            // 本组件 OnDisable 会跟着解订,无需手动 null 守卫。
            if (CollisionService.Instance != null)
            {
                CollisionService.Instance.OnPlayerGrazeByEnemyBullet += HandleGraze;
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] CollisionService.Instance 为 null,擦弹事件无法订阅。请确认场景里挂了 CollisionService 组件。");
            }
        }

        void OnDisable()
        {
            // 与 Start 对称解订,避免组件被禁用 / 销毁时残留回调。
            if (CollisionService.Instance != null)
            {
                CollisionService.Instance.OnPlayerGrazeByEnemyBullet -= HandleGraze;
            }
        }

        /// <summary>CollisionService 触发时回调:累加 GrazeCount + 广播事件。</summary>
        void HandleGraze(Bullet bullet, PlayerHealth player)
        {
            // 参数 bullet / player 当前不读 —— 这里只关心"擦弹发生了"这一信号;
            // 后续若需要按弹类型 / 玩家状态做差异化(例如对追踪弹擦弹额外加分),可在此扩展。
            GrazeCount++;
            OnGraze?.Invoke(GrazeCount);
        }

        /// <summary>
        /// 调试入口:跳过真实碰撞,直接累加一次擦弹。供 UI 测试 / Play Mode 调试用,
        /// 生产代码不应调本方法(正常的擦弹走 CollisionService 事件)。
        /// </summary>
        public void DebugTriggerGraze()
        {
            HandleGraze(null, this);
        }

        void Update()
        {
            // 无敌倒计时
            if (InvincibleRemaining > 0f)
            {
                InvincibleRemaining -= Time.deltaTime;
                if (InvincibleRemaining <= 0f)
                {
                    InvincibleRemaining = 0f;
                    OnInvincibleEnd?.Invoke();
                }
            }
        }

        /// <summary>被敌弹 / 敌人命中时调用。无敌时直接吞掉。</summary>
        public void TakeHit(float damage = 1f)
        {
            if (IsInvincible) return;
            if (damage <= 0f) return;
            if (Lives <= 0) return;

            OnLifeLost?.Invoke();

            Lives -= 1;
            if (Lives <= 0)
            {
                Lives = 0;
                OnAllLivesLost?.Invoke();
                return;
            }

            // 复活:开启无敌 + 触发事件
            InvincibleRemaining = ReviveInvincibleDuration;
            OnInvincibleStart?.Invoke();
            OnRevive?.Invoke();
        }

        /// <summary>吃火力道具时调用。clamp 到 [0, MaxPower]。</summary>
        public void PowerUp(int delta = 1)
        {
            int next = Mathf.Clamp(PowerLevel + delta, 0, MaxPower);
            if (next == PowerLevel) return;
            PowerLevel = next;
            OnPowerUp?.Invoke(PowerLevel);
        }

        /// <summary>强制复活 / 加命(给续命道具用)。</summary>
        public void AddLife(int delta = 1)
        {
            Lives = Mathf.Max(0, Lives + delta);
        }
    }
}
