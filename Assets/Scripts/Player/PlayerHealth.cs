using System;
using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 玩家残机 + 复活无敌 + 火力级。
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

        public bool IsInvincible => InvincibleRemaining > 0f;
        public bool IsDead       => Lives <= 0;

        // ─── 事件(供 UI / 动画 / 子机响应)────────────────────
        public event Action OnLifeLost;       // 死亡瞬间(扣命前)
        public event Action OnRevive;         // 复活瞬间
        public event Action OnAllLivesLost;   // 全部耗尽(没复活)
        public event Action<int> OnPowerUp;    // 火力提升(int = 新等级)
        public event Action OnInvincibleStart;
        public event Action OnInvincibleEnd;

        void Awake()
        {
            Lives = Mathf.Max(0, InitialLives);
            PowerLevel = Mathf.Clamp(InitialPower, 0, MaxPower);
            InvincibleRemaining = SpawnInvincibleDuration;
            if (InvincibleRemaining > 0f) OnInvincibleStart?.Invoke();
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

        /// 被敌弹 / 敌人命中时调用。无敌时直接吞掉。
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

        /// 吃火力道具时调用。clamp 到 [0, MaxPower]。
        public void PowerUp(int delta = 1)
        {
            int next = Mathf.Clamp(PowerLevel + delta, 0, MaxPower);
            if (next == PowerLevel) return;
            PowerLevel = next;
            OnPowerUp?.Invoke(PowerLevel);
        }

        /// 强制复活 / 加命(给续命道具用)。
        public void AddLife(int delta = 1)
        {
            Lives = Mathf.Max(0, Lives + delta);
        }
    }
}