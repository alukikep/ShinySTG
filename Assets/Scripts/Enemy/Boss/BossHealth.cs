using System;
using System.Collections.Generic;
using UnityEngine;
using SerializeReferenceEditor;
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

            [Tooltip("按顺序消耗的分段；为空时保持原有整管配置。")]
            public BossHealthSegment[] Segments;

            public bool HasSegments => Segments != null && Segments.Length > 0;
            public float EffectiveMaxHp
            {
                get
                {
                    if (!HasSegments) return MaxHp;
                    double total = 0;
                    foreach (var segment in Segments) total += segment?.MaxHp ?? 0f;
                    return (float)total;
                }
            }

            [NonSerialized] public float CurrentHp;

            [Tooltip("该管被打空时是否触发 OnBarDepleted 事件。")]
            public bool TriggerOnEmpty = true;

            [Tooltip("打空本管时丢弃本次攻击的剩余伤害；不阻止后续攻击。默认关闭以兼容旧配置。")]
            public bool DiscardOverflow;

            [Tooltip("打空或超时后进入的下一管血；留空结束战斗。")]
            public string NextBarId = "";

            [Tooltip("启用后超时换管并显示倒计时；关闭时仅打空换管，适用于非符。") ]
            public bool HasTimeLimit = true;

            [Min(0.01f), Tooltip("启用时限时必须为有限正数，单位为秒。") ]
            public float TimeLimit = 60f;

            [SerializeReference, SR]
            [Tooltip("本管血内按顺序执行的行为状态。一个血管可以包含多个状态。")]
            public BossPhase[] States;
        }

        [Min(0), Tooltip("Boss 被完整击败时获得的分数。")]
        public int ScoreValue = 10000;

        [NonSerialized] public HealthBar[] Bars;
        [NonSerialized] public float LegacyMaxHp;

        public bool UsesBarStates { get; private set; }
        public bool IsCurrentBarEmpty => UsesBarStates && CurrentHp <= 0f;

        float[][] _segmentHp;
        public int CurrentSegmentIndex { get; private set; }
        public bool UsesSegments => CurrentBar?.HasSegments == true;
        public BossHealthSegment CurrentSegment => UsesSegments && CurrentSegmentIndex >= 0 &&
            CurrentSegmentIndex < CurrentBar.Segments.Length ? CurrentBar.Segments[CurrentSegmentIndex] : null;
        public float CurrentSegmentMaxHp => CurrentSegment?.MaxHp ?? 0f;
        public float CurrentSegmentHp => GetSegmentHp(CurrentBarIndex, CurrentSegmentIndex);
        public float CurrentSegmentPercent => CurrentSegmentMaxHp > 0f
            ? Mathf.Clamp01(CurrentSegmentHp / CurrentSegmentMaxHp) * 100f : 0f;
        public bool IsCurrentSegmentEmpty => UsesSegments && CurrentSegmentHp <= 0f;
        /// <summary>本段被伤害打空；参数为管索引和段索引，不受 TriggerOnEmpty 控制。</summary>
        public event Action<int, int> OnSegmentDepleted;

        public float GetSegmentHp(int barIndex, int segmentIndex)
        {
            if (_segmentHp == null || barIndex < 0 || barIndex >= _segmentHp.Length ||
                _segmentHp[barIndex] == null || segmentIndex < 0 || segmentIndex >= _segmentHp[barIndex].Length) return 0f;
            return _segmentHp[barIndex][segmentIndex];
        }

        public void Initialize(HealthBar[] bars, bool usesBarStates = false)
        {
            if (bars != null)
                foreach (var bar in bars)
                    if (bar?.HasSegments == true && !BossHealthSegment.ValidateSegments(bar.Segments, out var error))
                        throw new ArgumentException(error, nameof(bars));
            Bars = bars;
            UsesBarStates = usesBarStates;
            LegacyMaxHp = LegacyCurrentHp = 0f;
            CurrentBarIndex = 0;
            CurrentSegmentIndex = 0;
            _segmentHp = bars == null ? null : new float[bars.Length][];
            if (bars != null)
                for (int i = 0; i < bars.Length; i++)
                    if (bars[i]?.HasSegments == true)
                    {
                        _segmentHp[i] = new float[bars[i].Segments.Length];
                        for (int j = 0; j < _segmentHp[i].Length; j++)
                            _segmentHp[i][j] = bars[i].Segments[j].MaxHp;
                    }
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
                if (Bars[i] != null && !Bars[i].HasSegments) Bars[i].CurrentHp = Bars[i].MaxHp;
            CurrentBarIndex = 0;
            SkipInvalidBars();
        }

        bool HasBars => Bars != null && Bars.Length > 0;
        HealthBar CurrentBar => HasBars && CurrentBarIndex >= 0 && CurrentBarIndex < Bars.Length
            ? Bars[CurrentBarIndex] : null;

        public float MaxHp => HasBars ? Mathf.Max(0f, CurrentBar?.EffectiveMaxHp ?? 0f) : Mathf.Max(0f, LegacyMaxHp);
        public float CurrentHp => HasBars ? GetBarHp(CurrentBarIndex) : Mathf.Max(0f, LegacyCurrentHp);

        float GetBarHp(int index)
        {
            if (!HasBars || index < 0 || index >= Bars.Length || Bars[index] == null) return 0f;
            if (!Bars[index].HasSegments) return Mathf.Max(0f, Bars[index].CurrentHp);
            double total = 0;
            if (_segmentHp != null && index < _segmentHp.Length && _segmentHp[index] != null)
                foreach (float hp in _segmentHp[index]) total += hp;
            return (float)total;
        }
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
            if (bar.EffectiveMaxHp <= 0f || !BossHealthSegment.IsFinite(bar.EffectiveMaxHp)) return false;
            if (found < CurrentBarIndex) return true;
            if (found > CurrentBarIndex) { percent = 100f; return true; }
            float hp = GetBarHp(found);
            if (!BossHealthSegment.IsFinite(hp)) return false;
            percent = Mathf.Clamp01(hp / bar.EffectiveMaxHp) * 100f;
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
                if (Bars[i] != null && Bars[i].EffectiveMaxHp > 0f) count++;
            return count;
        }

        void SkipInvalidBars()
        {
            while (HasBars && CurrentBarIndex < Bars.Length &&
                   (Bars[CurrentBarIndex] == null || Bars[CurrentBarIndex].EffectiveMaxHp <= 0f))
                CurrentBarIndex++;
        }

        public float TotalHpPercent
        {
            get
            {
                if (Bars == null || Bars.Length == 0)
                    return LegacyMaxHp > 0 ? Mathf.Clamp01(LegacyCurrentHp / LegacyMaxHp) * 100f : 0f;

                double sumMax = 0, sumCur = 0;
                for (int i = 0; i < Bars.Length; i++)
                {
                    var b = Bars[i];
                    if (b == null || b.EffectiveMaxHp <= 0) continue;
                    // 已经打空的管按 0 计入
                    sumMax += b.EffectiveMaxHp;
                    if (i < CurrentBarIndex) sumCur += 0f;
                    else if (i == CurrentBarIndex) sumCur += GetBarHp(i);
                    else sumCur += b.EffectiveMaxHp; // 未来的管满血计入
                }
                return sumMax > 0 ? Mathf.Clamp01((float)(sumCur / sumMax)) * 100f : 0f;
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
            if (ShinySTG.Level.BattleRestriction.IsActive) return;
            if (IsCurrentBarEmpty || IsCurrentSegmentEmpty || dmg <= 0f || float.IsNaN(dmg) || float.IsInfinity(dmg) || IsDead || IsInvincible || _applyingDamage) return;
            _applyingDamage = true;
            try { if (UsesSegments) ApplySegmentDamage(dmg); else ApplyDamage(dmg); }
            finally { _applyingDamage = false; }
        }

        void ApplySegmentDamage(float damage)
        {
            float multiplier = CurrentSegment.DamageTakenMultiplier;
            if (!(multiplier > 0f) || !BossHealthSegment.IsFinite(multiplier)) return;
            int barIndex = CurrentBarIndex, segmentIndex = CurrentSegmentIndex;
            var bar = CurrentBar;
            // 使用 double，避免两个有效 float 相乘溢出。
            float hp = (float)Math.Max(0d, CurrentSegmentHp - (double)damage * multiplier);
            _segmentHp[barIndex][segmentIndex] = hp;
            if (hp <= 0f)
            {
                bool barEmpty = segmentIndex == bar.Segments.Length - 1;
                if (barEmpty) BarEmptied?.Invoke(barIndex);
                OnSegmentDepleted?.Invoke(barIndex, segmentIndex);
                if (barEmpty && bar.TriggerOnEmpty)
                {
                    if (_barDepletedSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_barDepletedSfx, position: Position);
                    OnBarDepleted?.Invoke(barIndex);
                }
            }
            else if (_hitSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_hitSfx, position: Position);
            OnHealthChanged?.Invoke();
        }

        /// <summary>供 Controller 在收尾后推进已空分段；最后一段由 CompleteCurrentBar 推进。</summary>
        internal bool AdvanceToNextSegment()
        {
            if (_applyingDamage || !IsCurrentSegmentEmpty || CurrentSegmentIndex + 1 >= CurrentBar.Segments.Length) return false;
            CurrentSegmentIndex++;
            OnHealthChanged?.Invoke();
            return true;
        }

        /// <summary>超时清空当前段，不伪造受击或击破事件。</summary>
        internal void EmptyCurrentSegment()
        {
            if (_applyingDamage || !UsesSegments || IsCurrentSegmentEmpty) return;
            _segmentHp[CurrentBarIndex][CurrentSegmentIndex] = 0f;
            OnHealthChanged?.Invoke();
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
                    if (bar == null || bar.EffectiveMaxHp <= 0f) { CurrentBarIndex++; continue; }
                    // 旧管溢出不能绕过下一管的分段结算规则。
                    if (bar.HasSegments) break;

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
                        if (UsesBarStates) break;
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

        // Controller owns advancement. Empty bars stay current until their state has exited.
        internal void CompleteCurrentBar()
        {
            if (!UsesBarStates || IsDead) return;
            if (UsesSegments)
            {
                if (_applyingDamage || !IsCurrentSegmentEmpty || CurrentSegmentIndex != CurrentBar.Segments.Length - 1) return;
            }
            Bars[CurrentBarIndex].CurrentHp = 0f;
            CurrentBarIndex++;
            CurrentSegmentIndex = 0;
            if (IsDead && !_deathFired)
            {
                _deathFired = true;
                if (_deathSfx != null) ShinySTG.Audio.AudioMix.PlaySfx(_deathSfx, position: Position);
                OnDeath?.Invoke();
                OnAnyDeath?.Invoke(this);
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
