using System;
using System.Collections.Generic;
using UnityEngine;
// IHomingTarget 在全局命名空间(与 Bullet/BulletModifier/BulletPool 同款),无需 using。

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
    ///
    /// 与 EnemyHealth 对齐(参考同名 EnemyHealth 字段):
    ///   - Hitbox:HitboxComponent 引用(由 Boss 总控 Awake 自动注入)
    ///   - Position:读 Hitbox 优先,fallback 到 transform.position
    ///   - static Alive:IReadOnlyList<BossHealth> 全局池(供 CollisionService 拉取)
    ///   - static OnAnyDeath 事件(全局死亡广播)
    ///   - 实例 OnDeath 事件(供 Boss 总控订阅,统一收尾)
    ///
    /// 实现 IHomingTarget:追踪弹统一目标接口。Position / IsDead 已存在,签名兼容。
    /// </summary>
    public class BossHealth : MonoBehaviour, IHomingTarget
    {
        // ─── 全局 alive 池(供 CollisionService / 全局统计拉取)──
        static readonly List<BossHealth> _alive = new();
        public static IReadOnlyList<BossHealth> Alive => _alive;

        /// <summary>任何 Boss 死亡时触发。订阅者自行 null-check 或按需过滤。</summary>
        public static event Action<BossHealth> OnAnyDeath;
        [Serializable]
        public class HealthBar
        {
            [Tooltip("稳定引用 ID，同一 Boss 内唯一；例如 spell-a。被阶段引用后不要修改，重排和改名不影响引用。旧血管可留空。")]
            public string Id = "";

            [Tooltip("便于 Inspector 辨认,如 'Bar 1 (符卡 A)'。")]
            public string Name = "Bar";

            [Tooltip("该管血量上限。")]
            public float MaxHp = 1000f;

            [NonSerialized] public float CurrentHp;

            [Tooltip("该管被打空时是否触发 OnBarDepleted 事件。")]
            public bool TriggerOnEmpty = true;

            [Tooltip("打空本管时丢弃本次攻击的剩余伤害；不阻止后续攻击。默认关闭以兼容旧配置。")]
            public bool DiscardOverflow;
        }

        [NonSerialized] public HealthBar[] Bars;
        [NonSerialized] public float LegacyMaxHp;

        public void Initialize(HealthBar[] bars)
        {
            Bars = bars;
            LegacyMaxHp = LegacyCurrentHp = 0f;
            CurrentBarIndex = 0;
            _deathFired = _applyingDamage = false;
            _invincibilityLocks.Clear();
            _transitionProtectionOwners.Clear();
            InitBars();
        }

        [Header("Audio (optional — 留空则不播放)")]
        [Tooltip("Boss 受击时播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _hitSfx;
        [Tooltip("某管血被打空时播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _barDepletedSfx;
        [Tooltip("Boss 死亡时播放的 SFX cue(留空 = 不播)。")]
        [SerializeField] ShinySTG.Audio.SfxCue _deathSfx;

        [Header("Hitbox (供碰撞层读位置)")]
        [Tooltip("由 Boss 总控 Awake 自动注入,无需手填。\n" +
                 "空时回退到 transform.position。")]
        public ShinySTG.Hitbox.HitboxComponent Hitbox;

        [NonSerialized] public int CurrentBarIndex;
        [NonSerialized] public float LegacyCurrentHp;

        public Vector2 Position =>
            Hitbox != null ? Hitbox.Position : (Vector2)transform.position;

        // OnDeath 防重入:TakeDamage 每次进入只触发一次。
        bool _deathFired;
        bool _applyingDamage;
        readonly HashSet<string> _invincibilityLocks = new();
        readonly HashSet<object> _transitionProtectionOwners = new();
        public bool IsInvincible => _invincibilityLocks.Count > 0 || _transitionProtectionOwners.Count > 0;

        // 内部事实通知，不受表现/旧阶段通知开关 TriggerOnEmpty 影响。
        internal event Action<int> BarEmptied;
        internal void AcquireTransitionProtection(object owner) => _transitionProtectionOwners.Add(owner);
        internal void ReleaseTransitionProtection(object owner) => _transitionProtectionOwners.Remove(owner);

        /// <summary>某管被打空事件(int = 被清空的 BarIndex)。</summary>
        public event Action<int> OnBarDepleted;

        /// <summary>整个 Boss 被打空(所有管清零 / LegacyCurrentHp 归零)时触发一次。由 BossController 订阅做收尾。</summary>
        public event Action OnDeath;

        /// <summary>一次伤害完整结算（包括死亡通知）后触发，不受 TriggerOnEmpty 控制。</summary>
        public event Action OnHealthChanged;

        void OnEnable()
        {
            if (!_alive.Contains(this)) _alive.Add(this);

        }

        void OnDisable()
        {
            _alive.Remove(this);
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
            SkipInvalidBars();
        }

        bool HasBars => Bars != null && Bars.Length > 0;
        HealthBar CurrentBar => HasBars && CurrentBarIndex >= 0 && CurrentBarIndex < Bars.Length
            ? Bars[CurrentBarIndex] : null;

        public float MaxHp => HasBars ? Mathf.Max(0f, CurrentBar?.MaxHp ?? 0f) : Mathf.Max(0f, LegacyMaxHp);
        public float CurrentHp => HasBars ? Mathf.Max(0f, CurrentBar?.CurrentHp ?? 0f) : Mathf.Max(0f, LegacyCurrentHp);
        public float CurrentHpNormalized => MaxHp > 0f ? Mathf.Clamp01(CurrentHp / MaxHp) : 0f;
        public float HpPercent => CurrentHpNormalized * 100f;
        public float CurrentBarPercent => HpPercent;

        /// <summary>按稳定 ID 查询指定管。缺失、重复或无效配置不回退到当前管。</summary>
        public bool TryGetBarPercent(string id, out float percent)
        {
            percent = 0f;
            if (string.IsNullOrWhiteSpace(id) || !HasBars) return false;
            int found = -1;
            for (int i = 0; i < Bars.Length; i++)
            {
                if (Bars[i] == null || !string.Equals(Bars[i].Id, id, StringComparison.Ordinal)) continue;
                if (found >= 0) return false;
                found = i;
            }
            if (found < 0) return false;
            var bar = Bars[found];
            if (bar.MaxHp <= 0f || float.IsNaN(bar.MaxHp) || float.IsInfinity(bar.MaxHp)) return false;
            if (found < CurrentBarIndex) return true;
            if (found > CurrentBarIndex) { percent = 100f; return true; }
            if (float.IsNaN(bar.CurrentHp) || float.IsInfinity(bar.CurrentHp)) return false;
            percent = Mathf.Clamp01(bar.CurrentHp / bar.MaxHp) * 100f;
            return true;
        }

        /// <summary>只计非空且上限为正的管；空数组使用 Legacy 单管。</summary>
        public int TotalBarCount => HasBars ? CountBars(0) : (LegacyMaxHp > 0f ? 1 : 0);
        /// <summary>包含当前正在消耗的血管；死亡时为零。</summary>
        public int RemainingBarCount => IsDead ? 0 : HasBars ? CountBars(CurrentBarIndex) : TotalBarCount;

        int CountBars(int start)
        {
            int count = 0;
            for (int i = Mathf.Max(0, start); i < Bars.Length; i++)
                if (Bars[i] != null && Bars[i].MaxHp > 0f) count++;
            return count;
        }

        void SkipInvalidBars()
        {
            while (HasBars && CurrentBarIndex < Bars.Length &&
                   (Bars[CurrentBarIndex] == null || Bars[CurrentBarIndex].MaxHp <= 0f))
                CurrentBarIndex++;
        }

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
        /// 死亡时触发一次 OnDeath 事件(防重入),由 BossController 订阅做收尾。
        /// </summary>
        public void TakeDamage(float dmg)
        {
            if (dmg <= 0f || float.IsNaN(dmg) || float.IsInfinity(dmg) || IsDead || IsInvincible || _applyingDamage) return;
            _applyingDamage = true;
            try { ApplyDamage(dmg); }
            finally { _applyingDamage = false; }
        }

        void ApplyDamage(float dmg)
        {
            if (Bars == null || Bars.Length == 0)
            {
                // 兼容单管模式
                LegacyCurrentHp = Mathf.Max(0f, LegacyCurrentHp - dmg);
            }
            else
            {
                float remaining = dmg;
                while (remaining > 0f && CurrentBarIndex < Bars.Length)
                {
                    var bar = Bars[CurrentBarIndex];
                    if (bar == null || bar.MaxHp <= 0f) { CurrentBarIndex++; continue; }

                    int oldBarIdx = CurrentBarIndex;
                    bar.CurrentHp -= remaining;
                    if (bar.CurrentHp <= 0f)
                    {
                        remaining = -bar.CurrentHp; // 溢出伤害继续扣下一管
                        bar.CurrentHp = 0f;
                        BarEmptied?.Invoke(oldBarIdx);
                        // __BOSSDEBUG__ #7:每管打空都打印
                        Debug.Log($"[__BOSSDEBUG__] Bar[{oldBarIdx}] depleted, remaining={remaining:F1} → switch to next", this);
                        if (bar.TriggerOnEmpty)
                        {
                            if (_barDepletedSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_barDepletedSfx, position: Position);
                            OnBarDepleted?.Invoke(oldBarIdx);
                        }
                        CurrentBarIndex++;
                        SkipInvalidBars();
                        if (bar.DiscardOverflow || _transitionProtectionOwners.Count > 0) remaining = 0f;
                    }
                    else
                    {
                        // 受击音(每次扣血都播;若觉得太密,在 cue 上设 Cooldown / MaxVoices 节流)
                        if (_hitSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_hitSfx, position: Position);
                        remaining = 0f;
                    }
                }
            }

            // 死亡判定(覆盖多管全清 + Legacy 单管归零 两条路径)。
            // IsDead 已在函数开头早返,所以此处只在首次真正死亡时进入。
            if (IsDead && !_deathFired)
            {
                _deathFired = true;
                // __BOSSDEBUG__ #7b:最终死亡
                Debug.Log($"[__BOSSDEBUG__] BossHealth.IsDead → OnDeath @ t={Time.time:F2}", this);
                if (_deathSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_deathSfx, position: Position);
                OnDeath?.Invoke();        // 实例事件:供 Boss 总控订阅做收尾
                OnAnyDeath?.Invoke(this); // 静态事件:供全局订阅
            }
            OnHealthChanged?.Invoke();
        }

        public void AddInvincibility(string sourceKey)
        {
            if (!string.IsNullOrWhiteSpace(sourceKey)) _invincibilityLocks.Add(sourceKey);
        }

        public void RemoveInvincibility(string sourceKey)
        {
            if (!string.IsNullOrWhiteSpace(sourceKey)) _invincibilityLocks.Remove(sourceKey);
        }
    }
}
