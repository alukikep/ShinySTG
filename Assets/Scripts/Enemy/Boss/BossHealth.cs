using System;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss
{
    /// <summary>
    /// Boss HP 组件,支持"多管血"(东方式多阶段血量)。
    /// 每管血打空后自动切到下一管;每管可有独立的 MaxHp。
    /// 暴露给各 Signal 读取:
    ///   - HpPercent           : 单管剩余百分比(兼容旧 HpSignal)
    ///   - CurrentBarPercent   : 当前管剩余百分比
    ///   - CurrentBarIndex     : 当前管编号(0/1/2/...)
    ///   - TotalHpPercent      : 所有管加权累计剩余百分比
    ///   - MaxHp / CurrentHp   : 单管兼容属性(Bars 为空时退化)
    /// </summary>
    public class BossHealth : MonoBehaviour
    {
        [Serializable]
        public class HealthBar
        {
            [Tooltip("便于 Inspector 辨认,如 'Bar 1 (符卡 A)'。")]
            public string Name = "Bar";

            [Tooltip("该管血量上限。")]
            public float MaxHp = 1000f;

            [HideInInspector] public float CurrentHp;

            [Tooltip("该管被打空时是否触发 OnBarDepleted 事件。")]
            public bool TriggerOnEmpty = true;
        }

        [Tooltip("Boss 血量管列。每管打空自动切到下一管,全部打空则死亡。")]
        public HealthBar[] Bars = new HealthBar[]
        {
            new HealthBar { Name = "Bar 1", MaxHp = 1000f },
            new HealthBar { Name = "Bar 2", MaxHp = 800f  },
            new HealthBar { Name = "Bar 3", MaxHp = 500f  },
        };

        [Tooltip("兼容字段:Bars 为空时把 MaxHp 当单管血。")]
        public float LegacyMaxHp = 1000f;

        [HideInInspector] public int   CurrentBarIndex;
        [HideInInspector] public float LegacyCurrentHp;

        /// <summary>某管被打空事件(int = 被清空的 BarIndex)。</summary>
        public event Action<int> OnBarDepleted;

        void Awake()
        {
            InitBars();
        }

        void InitBars()
        {
            if (Bars == null || Bars.Length == 0)
            {
                // 兼容老配置:单管血模式
                LegacyCurrentHp = LegacyMaxHp;
                return;
            }
            for (int i = 0; i < Bars.Length; i++)
                if (Bars[i] != null) Bars[i].CurrentHp = Bars[i].MaxHp;
            CurrentBarIndex = 0;
        }

        // ─── 兼容属性(老 HpSignal 仍可用)──────────────────
        public float MaxHp =>
            (Bars != null && Bars.Length > 0 && Bars[CurrentBarIndex] != null)
                ? Bars[CurrentBarIndex].MaxHp : LegacyMaxHp;

        public float CurrentHp =>
            (Bars != null && Bars.Length > 0 && Bars[CurrentBarIndex] != null)
                ? Bars[CurrentBarIndex].CurrentHp : LegacyCurrentHp;

        public float HpPercent =>
            MaxHp > 0 ? Mathf.Clamp01(CurrentHp / MaxHp) * 100f : 0f;

        // ─── 新属性(供新 Signal 读)─────────────────────
        public float CurrentBarPercent =>
            (Bars != null && Bars.Length > 0 && Bars[CurrentBarIndex] != null && Bars[CurrentBarIndex].MaxHp > 0)
                ? Mathf.Clamp01(Bars[CurrentBarIndex].CurrentHp / Bars[CurrentBarIndex].MaxHp) * 100f
                : 0f;

        public float TotalHpPercent
        {
            get
            {
                if (Bars == null || Bars.Length == 0)
                    return LegacyMaxHp > 0 ? Mathf.Clamp01(LegacyCurrentHp / LegacyMaxHp) * 100f : 0f;

                float sumMax = 0f, sumCur = 0f;
                for (int i = 0; i < Bars.Length; i++)
                {
                    var b = Bars[i];
                    if (b == null || b.MaxHp <= 0) continue;
                    // 已经打空的管按 0 计入
                    sumMax += b.MaxHp;
                    if (i < CurrentBarIndex) sumCur += 0f;
                    else if (i == CurrentBarIndex) sumCur += Mathf.Max(0f, b.CurrentHp);
                    else sumCur += b.MaxHp; // 未来的管满血计入
                }
                return sumMax > 0 ? Mathf.Clamp01(sumCur / sumMax) * 100f : 0f;
            }
        }

        public bool IsDead => Bars != null && Bars.Length > 0
            ? CurrentBarIndex >= Bars.Length
            : LegacyCurrentHp <= 0f;

        /// <summary>
        /// 受到伤害(给玩家子弹的逻辑调用)。
        /// 自动处理"扣穿管"、"切到下一管"、"打空事件"、"全部死亡"。
        /// </summary>
        public void TakeDamage(float dmg)
        {
            if (dmg <= 0f || IsDead) return;

            if (Bars == null || Bars.Length == 0)
            {
                // 兼容单管模式
                LegacyCurrentHp = Mathf.Max(0f, LegacyCurrentHp - dmg);
                return;
            }

            float remaining = dmg;
            while (remaining > 0f && CurrentBarIndex < Bars.Length)
            {
                var bar = Bars[CurrentBarIndex];
                if (bar == null) { CurrentBarIndex++; continue; }

                bar.CurrentHp -= remaining;
                if (bar.CurrentHp <= 0f)
                {
                    remaining = -bar.CurrentHp; // 溢出伤害继续扣下一管
                    bar.CurrentHp = 0f;
                    if (bar.TriggerOnEmpty)
                        OnBarDepleted?.Invoke(CurrentBarIndex);
                    CurrentBarIndex++;
                }
                else
                {
                    remaining = 0f;
                }
            }
        }
    }
}
