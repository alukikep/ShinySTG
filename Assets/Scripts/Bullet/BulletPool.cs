using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class BulletPool : MonoBehaviour
{

    public static BulletPool Instance { get; private set; }

    /// 按 prefab 分桶：不同 FirePattern.BulletPrefab 各自走各自的栈，避免互相覆盖外观/行为。
    readonly Dictionary<Bullet, Stack<Bullet>> _available = new();
    readonly HashSet<Bullet> _active = new();

    /// <summary>供 CollisionService 等外部系统访问当前活跃子弹。HashSet 迭代时禁止外部修改(碰撞服务用 deferred return 规避)。</summary>
    public System.Collections.Generic.IReadOnlyCollection<Bullet> ActiveBullets => _active;

    public Bullet DefaultPrefab;
    public int InitialSize = 100;

    void Awake()
    {
        Instance = this;
        // 只为 DefaultPrefab 预热一颗桶，避免在用户尚未配置任何 FirePattern 时就 NRE。
        // 其他 prefab 在第一次被 FirePattern 真正使用时按需扩容。
        if (DefaultPrefab != null && InitialSize > 0)
        {
            var stack = GetOrCreateStack(DefaultPrefab);
            for (int i = 0; i < InitialSize; i++)
            {
                var b = Instantiate(DefaultPrefab, transform);
                b.SourcePrefab = DefaultPrefab;
                b.gameObject.SetActive(false);
                stack.Push(b);
            }
        }
    }

    void OnDestroy()
    {
        // 场景切换时解除 Instance 指针。子弹实例随 GameObject 自动销毁,不需手动清 _active
        // (外部系统此刻也已停止 Update,无并发风险)。
        if (Instance == this) Instance = null;
    }

    /// 获取（或懒创建）某个 prefab 对应的桶。
    Stack<Bullet> GetOrCreateStack(Bullet prefab)
    {
        if (!_available.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Bullet>();
            _available[prefab] = stack;
        }
        return stack;
    }

    /// 取一颗子弹。
    /// </summary>
    /// <param name="prefab">要实例化的子弹 prefab。空时兜底使用 DefaultPrefab。</param>
    /// <param name="pos">发射位置</param>
    /// <param name="fireAngleRad">发射角度(弧度)</param>
    /// <param name="speed">飞行速度</param>
    /// <param name="angularSpeed">角速度(弧度/秒)</param>
    /// <param name="damage">伤害值(玩家弹才用,经 bullet.Damage 传给 CollisionService)</param>
    /// <param name="ownerTeam">发射者阵营(透传给子弹 Hitbox.Team)。null = Neutral(不参与碰撞)。</param>
    public Bullet Get(Bullet prefab, Vector2 pos, float fireAngleRad, float speed, float angularSpeed,
                      float damage, ShinySTG.Hitbox.CollisionTeam ownerTeam)
        => Get(prefab, pos, fireAngleRad, speed, angularSpeed, damage, ownerTeam, null, null, null);

    /// <summary>
    /// 取一颗子弹,并按指定 prefab 数组挂载 BulletModifier。
    /// Modifier 实例会被 Instantiate 到子弹的子层级,随子弹回池自动清理。
    /// </summary>
    /// <param name="spawnFog">
    /// 出生雾化配置(可空)。null 或 Duration=0 = 不雾化(默认,与历史行为 100% 等价)。
    /// 由 FirePattern.SpawnFog 透传,详见 Assets/Scripts/Bullet/FirePattern/SpawnFog/SpawnFogConfig.cs。
    /// </param>
    public Bullet Get(Bullet prefab, Vector2 pos, float fireAngleRad, float speed, float angularSpeed,
                      float damage, ShinySTG.Hitbox.CollisionTeam ownerTeam,
                      BulletModifier[] modifiersToAttach,
                      SpawnFogConfig spawnFog = null, Transform ownerTransform = null)
    {
        // prefab 为空时兜底使用 DefaultPrefab(避免某些 Pattern 未配置时崩溃)
        if (ShinySTG.Level.BattleRestriction.IsActive) return null;
        var usePrefab = prefab != null ? prefab : DefaultPrefab;
        if (usePrefab == null)
        {
            // 没有任何可用的 prefab:直接 return,避免 NRE。
            return null;
        }

        var stack = GetOrCreateStack(usePrefab);
        var b = stack.Count > 0 ? stack.Pop() : Instantiate(usePrefab, transform);
        b.SourcePrefab = usePrefab;
        b.gameObject.SetActive(true);
        b.Init(pos, fireAngleRad, speed, angularSpeed, damage, ownerTeam, spawnFog, ownerTransform);
        AttachModifiers(b, modifiersToAttach);
        _active.Add(b);
        return b;
    }

    /// <summary>
    /// 把 modifier 模板 Clone 一份独立实例,并 AddModifier 到子弹。
    /// 由 FirePattern.SpawnBullet 统一调用(也可被外部直接调用)。
    ///
    /// 注意:Modifier 是纯 C# 对象(SerializeReference 路线),
    /// 不是 GameObject 子对象 —— bullet.transform 下不再产生 modifier 子层级,
    /// modifier 不继承 bullet 的 transform 缩放。
    ///
    /// ★ 挂完所有 modifier 后,统一调一次 ResetWindow,把每颗子弹的时间窗口计时器归零
    ///   (OneShot modifier 重新具备触发机会;Delay/Duration 从这一刻起算)。
    /// </summary>
    void AttachModifiers(Bullet bullet, BulletModifier[] mods)
    {
        if (bullet == null || mods == null) return;
        for (int i = 0; i < mods.Length; i++)
        {
            var mod = mods[i];
            if (mod == null) continue;
            // Clone 出独立实例(默认 MemberwiseClone,纯值类型字段无开销),
            // 避免多颗子弹共享同一 modifier 模板导致状态污染。
            // ★ Clone 内部已深拷 StartTrigger(见 BulletModifier.Clone 注释),
            //   所以每颗子弹的 StartTrigger 都是独立实例,订阅不会互相覆盖。
            var runtime = mod.Clone();
            if (mod is FirePatternBulletModifier source && source.Extra is AngleOffsetFirePatternBulletExtra config
                && runtime is FirePatternBulletModifier split && split.Extra is AngleOffsetFirePatternBulletExtra angle
                && config.BatchSample == AngleOffsetFirePatternBulletExtra.BatchSampleMode.Synchronized)
            {
                float sample;
                if (_batchSamples == null) sample = angle.BaseOffset?.Sample() ?? 0f;
                else if (!_batchSamples.TryGetValue(config, out sample))
                {
                    sample = angle.BaseOffset?.Sample() ?? 0f;
                    _batchSamples.Add(config, sample);
                }
                angle.SetBatchSample(sample);
            }
            bullet.AddModifier(runtime);
        }
        // ★ 挂在 _modifiers 之后才调 ResetWindow(此前 _modifiers 已由 Bullet.Init 的 ClearModifiers 清空过)。
        // 这里没暴露 ResetAllModifierWindows,因为它需要遍历 _modifiers(私有列表),
        // 由 Bullet 自己暴露一个 public ResetAllModifierWindows() 调用更干净。
        bullet.ResetAllModifierWindows();
        // ★ 订阅型 StartTrigger(订阅 BulletSignalBus)在这里激活订阅。
        //   必须在 ResetWindow 之后调 —— OnAttach 内部 reset 自己的 per-instance 状态
        //   (如 _signalReceived=false),然后 Subscribe 进 BulletSignalBus。
        bullet.AttachSignalTriggers();
    }

    /// 回收一颗。
    public void Return(Bullet bullet)
    {
        if (bullet == null || !_active.Remove(bullet)) return;
        // ★ 先摘 BulletSignalBus 订阅(必须在 ClearModifiers 之前,因为 DetachSignalTriggers
        //   需要遍历 _modifiers 列表)。顺序:
        //     1) DetachSignalTriggers → 摘订阅(订阅型 StartTrigger.OnDetach 调 Unsubscribe)
        //     2) ClearModifiers       → 清空 _modifiers 列表
        //   防「弹已回池但 StartTrigger 还在 _subs 字典里挂着 handler」导致下次 Emit 时 NRE。
        bullet.ResetForPool();
        bullet.gameObject.SetActive(false);

        // 按该弹自己的 SourcePrefab 路由到正确的桶，保证不同 prefab 的子弹不会互相污染。
        var key = bullet.SourcePrefab != null ? bullet.SourcePrefab : DefaultPrefab;
        if (key != null)
        {
            var stack = GetOrCreateStack(key);
            stack.Push(bullet);
        }
        // 若 key 也为 null，则直接丢弃该子弹实例（极端兜底，不让池逻辑崩溃）。
    }

    public void Return(Bullet bullet, BulletClearPresentation presentation)
    {
        if (bullet == null || !_active.Contains(bullet)) return;
        ApplyPresentation(new List<Bullet> { bullet }, presentation);
        Return(bullet);
    }

    /// <summary>
    /// 批量回收符合阵营条件的活跃子弹。先复制集合再回收，避免遍历 HashSet 时修改集合。
    /// filter 为空时回收全部活跃子弹。
    /// </summary>
    public int ReturnAll(System.Predicate<ShinySTG.Hitbox.CollisionTeam> filter = null)
    {
        if (_active.Count == 0) return 0;
        var snapshot = new List<Bullet>(_active);
        int returned = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            var bullet = snapshot[i];
            if (bullet == null) continue;
            var team = bullet.Hitbox != null
                ? bullet.Hitbox.Team
                : ShinySTG.Hitbox.CollisionTeam.Neutral;
            if (filter != null && !filter(team)) continue;
            Return(bullet);
            returned++;
        }
        return returned;
    }

    public int ReturnAll(System.Predicate<ShinySTG.Hitbox.CollisionTeam> filter, BulletClearPresentation presentation)
    {
        if (_active.Count == 0) return 0;
        var snapshot = new List<Bullet>(_active);
        var selected = new List<Bullet>(snapshot.Count);
        for (int i = 0; i < snapshot.Count; i++)
        {
            var bullet = snapshot[i];
            if (bullet == null) continue;
            var team = bullet.Hitbox != null ? bullet.Hitbox.Team : ShinySTG.Hitbox.CollisionTeam.Neutral;
            if (filter == null || filter(team)) selected.Add(bullet);
        }
        ApplyPresentation(selected, presentation);
        for (int i = 0; i < selected.Count; i++) Return(selected[i]);
        return selected.Count;
    }

    static void ApplyPresentation(List<Bullet> bullets, BulletClearPresentation p)
    {
        if (p == null || p.Mode == BulletClearPresentationMode.Silent || bullets.Count == 0) return;
        if (p.Mode == BulletClearPresentationMode.ConvertToItems)
        {
            int count = Mathf.Min(256, Mathf.Min(Mathf.Max(0, p.MaxItemCount), Mathf.FloorToInt(bullets.Count * Mathf.Max(0f, p.ItemsPerBullet))));
            if (p.Item == null || count <= 0) return;
            int stride = Mathf.Max(1, bullets.Count / count);
            for (int i = 0, made = 0; i < bullets.Count && made < count; i += stride, made++)
                ShinySTG.Items.ItemDropService.SpawnSingle(p.Item, (Vector2)bullets[i].Position + UnityEngine.Random.insideUnitCircle * p.ItemScatterRadius,
                    UnityEngine.Random.insideUnitCircle.normalized * p.ItemSpeed);
            return;
        }
        if (p.EffectPrefab == null) return;
        int max = Mathf.Min(128, Mathf.Max(1, p.MaxEffectCount));
        float cell = Mathf.Max(0.1f, p.EffectGridSize);
        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < bullets.Count && cells.Count < max; i++)
        {
            var pos = bullets[i].Position;
            var key = new Vector2Int(Mathf.FloorToInt(pos.x / cell), Mathf.FloorToInt(pos.y / cell));
            if (cells.Add(key)) ShinySTG.Effects.EffectPool.Play(p.EffectPrefab, pos, bullets[i].gameObject.scene, null);
        }
    }

    /// 提供给 Enemy / Player 调用:发射一组 bullets(通过 FirePattern)。
    /// BulletPrefab 由 Pattern 自身携带,无需再传入。
    /// </summary>
    /// <param name="pattern">要发射的 FirePattern</param>
    /// <param name="pos">发射位置</param>
    /// <param name="rotationRad">整体朝向增量(弧度)</param>
    /// <param name="ownerHitbox">
    /// 发射者的 Hitbox(可为 null)。null 时子弹阵营 = Neutral(不参与碰撞)。
    /// 玩家发射传 PlayerHitbox;敌人发射传 EnemyHitbox。
    /// 子弹阵营 = ownerHitbox.Team,经 bullet.Init() 透传到 HitboxComponent.Team。
    /// </param>
    /// <param name="extraModifiers">
    /// 调用方(通常是 FireAction)在本轮发射时想额外追加的 modifier,
    /// 会在 pattern.ModifierPrefabs 之后追加。null = 不追加。
    /// </param>
    public void FireGroup(FirePattern pattern, Vector2 pos, float rotationRad,
                          ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                          BulletModifier[] extraModifiers = null)
        => FireGroup(pattern, pos, rotationRad, ownerHitbox, extraModifiers, null);

    public void FireGroup(FirePattern pattern, Vector2 pos, float rotationRad,
                          ShinySTG.Hitbox.HitboxComponent ownerHitbox,
                          BulletModifier[] extraModifiers, FirePatternRuntimeState state)
    {
        if (pattern == null || ShinySTG.Level.BattleRestriction.IsActive) return;
        // 触发 FirePattern 的开火音(FireSounds 数组)。
        // 在 pattern.Fire(...) 之前调 —— 每次"开火组"触发一次。
        // CompositeFirePattern 内部递归 Fire() 不走本入口,所以子 pattern 的 FireSounds 不重复触发。
        // 详见 Assets/Scripts/Bullet/FireExtension/FireSound.cs 顶部注释。
        pattern.PlayFireSounds(pos, ownerHitbox);

        var previousState = _currentRuntimeState;
        _currentRuntimeState = state ?? _sharedRuntimeState;
        var previousSamples = _batchSamples;
        var samples = _availableBatchSamples.Count > 0
            ? _availableBatchSamples.Pop()
            : new Dictionary<AngleOffsetFirePatternBulletExtra, float>();
        _batchSamples = samples;
        try
        {
            FireChild(pattern, pos, rotationRad, ownerHitbox, extraModifiers);
        }
        finally
        {
            _currentRuntimeState = previousState;
            _batchSamples = previousSamples;
            samples.Clear();
            _availableBatchSamples.Push(samples);
        }
    }

    readonly HashSet<FirePattern> _firePath = new();
    readonly Stack<Dictionary<AngleOffsetFirePatternBulletExtra, float>> _availableBatchSamples = new();
    public const int MaxPatternDepth = 64;

    // 子项共用上下文，不重复播放根音效或提交统计；路径集合只阻止循环，不阻止兄弟重复引用。
    public void FireChild(FirePattern pattern, Vector2 pos, float rotationRad,
        ShinySTG.Hitbox.HitboxComponent owner, BulletModifier[] extras)
    {
        if (pattern == null || ShinySTG.Level.BattleRestriction.IsActive || _firePath.Count >= MaxPatternDepth || !_firePath.Add(pattern)) return;
        try
        {
            if (_currentRuntimeState != null) _currentRuntimeState.Advance(pattern.FireExtensions);
            else if (pattern.FireExtensions != null)
                foreach (var ext in pattern.FireExtensions)
                {
                    if (ext == null) continue;
                    _fireCounts.TryGetValue(ext, out int count);
                    _fireCounts[ext] = count + 1;
                }
            pattern.Fire(pos, rotationRad, this, owner, extras);
        }
        finally { _firePath.Remove(pattern); }
    }

    /// <summary>
    /// 返回 pattern.FireExtensions 数组里每个非 null 元素当前的 fireCount(1 起)。
    /// 由 FirePattern.Fire 内部调 Resolver 时传入。
    ///
    /// 返回 Dictionary(FireExtension → int),让 pattern 子类(Ring/Arc/Line/Composite)按自己的
    /// 数组顺序查表;环形 8 颗子弹共用同一组 fireCount(本批开火序号)。
    ///
    /// 不存在 key → 不返回该元素(Resolver 走默认值 0,不调 OnFireGroupTriggered)。
    /// </summary>
    public System.Collections.Generic.IReadOnlyDictionary<FireExtension, int> GetFireExtensionFireCounts()
        => _currentRuntimeState != null ? _currentRuntimeState.FireCounts : _fireCounts;

    public FireExtension[] GetRuntimeFireExtensions(FireExtension[] source)
        => _currentRuntimeState != null ? _currentRuntimeState.GetRuntimeExtensions(source) : source;

    public System.Collections.Generic.IReadOnlyDictionary<FireExtension, int> GetRuntimeFireCounts(FireExtension[] source)
        => _currentRuntimeState != null ? _currentRuntimeState.GetRuntimeFireCounts(source) : _fireCounts;

    public void ResetFireCounts() { _fireCounts.Clear(); _sharedRuntimeState.Reset(); }

    /// <summary>
    /// per-FireExtension 累加计数(由 FireGroup 入口维护,key = pattern.FireExtensions 数组里的具体元素 ref)。
    /// 详见 FireGroup 内注释。
    /// </summary>
    readonly Dictionary<FireExtension, int> _fireCounts = new();
    readonly FirePatternRuntimeState _sharedRuntimeState = new();
    Dictionary<AngleOffsetFirePatternBulletExtra, float> _batchSamples;
    FirePatternRuntimeState _currentRuntimeState;
}
