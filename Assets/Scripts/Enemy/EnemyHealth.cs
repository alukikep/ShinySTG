using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 普通敌人 HP 组件。单管血,被打空触发 OnDeath 事件。
    ///
    /// 与 BossHealth 对齐:同样暴露 TakeDamage(float) / IsDead / HpPercent,语义一致;
    /// 与 BossHealth 的差异:不支持多管血、不自毁、不取消行为流
    ///   —— 这些由 Enemy 总控(Assets/Scripts/Enemy/Enemy.cs)在订阅 OnDeath 后处理,
    ///   保持"事件层与表现层分离",与 PlayerHealth 的写法同款。
    ///
    /// 全局静态事件 OnAnyDeath:
    ///   每个 EnemyHealth 在 OnEnable 加入 _alive,OnDisable 移除;
    ///   KillRewardSpawner / ScoreManager 等系统订阅一次就能拿到所有敌人死亡通知,
    ///   不需要每帧 FindObjectsOfType。
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        // ─── 全局 alive 池(供订阅者高效过滤,场景切换由 OnDisable 自动清理)──
        static readonly List<EnemyHealth> _alive = new();
        public static IReadOnlyList<EnemyHealth> Alive => _alive;

        /// <summary>任何敌人死亡时触发。订阅者自行 null-check 或按需过滤。</summary>
        public static event Action<EnemyHealth> OnAnyDeath;

        [Header("HP")]
        [Tooltip("最大血量。默认 1,普通小怪一击必杀。")]
        public float MaxHp = 1f;

        [Tooltip("运行时当前血量(Inspector 只读)。")]
        [SerializeField] float _currentHp;

        public float CurrentHp => _currentHp;
        public float HpPercent  => MaxHp > 0 ? Mathf.Clamp01(_currentHp / MaxHp) * 100f : 0f;
        public bool  IsDead     => _currentHp <= 0f;

        [Header("Hitbox (供碰撞层读位置)")]
        [Tooltip("由 Enemy 总控 Awake 自动注入,无需手填。\n" +
                 "空时回退到 transform.position。")]
        public ShinySTG.Hitbox.HitboxComponent Hitbox;

        public Vector2 Position =>
            Hitbox != null ? Hitbox.Position : (Vector2)transform.position;

        // ─── 实例事件(单个敌人订阅,如 Enemy 总控)──
        public event Action<float> OnDamaged; // 参数 = 本次实际扣血
        public event Action        OnDeath;

        void Awake()
        {
            _currentHp = MaxHp;
        }

        void OnEnable()
        {
            if (!_alive.Contains(this)) _alive.Add(this);
        }

        void OnDisable()
        {
            _alive.Remove(this);
        }

        /// <summary>
        /// 受伤入口。已死或 dmg<=0 直接吞掉。扣穿不会重复触发 OnDeath。
        /// </summary>
        public void TakeDamage(float dmg)
        {
            if (IsDead || dmg <= 0f) return;

            _currentHp = Mathf.Max(0f, _currentHp - dmg);
            OnDamaged?.Invoke(dmg);

            if (_currentHp <= 0f)
            {
                OnDeath?.Invoke();
                OnAnyDeath?.Invoke(this);
            }
        }
    }
}