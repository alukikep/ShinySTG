using SerializeReferenceEditor;
using UnityEngine;

/// FirePattern：决定"射什么子弹 + 怎么射"的完整定义。
/// 每个 Pattern SO 自带 BulletPrefab 与基础 Speed/AngularSpeed，
/// 子类可继续在内部覆盖（例如连射速度递增）。
public abstract class FirePattern : ScriptableObject
{
    [Header("Bullet")]
    [Tooltip("该 pattern 发射的子弹 prefab。留空则使用 BulletPool.DefaultPrefab 兜底。")]
    public Bullet BulletPrefab;

    [Header("Modifiers (子弹生成后自动挂载)")]
    [Tooltip("下拉选 modifier 类型,直接编辑字段(走 SerializeReference + SRName)。\n" +
             "运行时每颗子弹会 Clone 一份独立实例,modifier 状态不会跨子弹污染。\n" +
             "Modifier 只持有逻辑,不要访问自己的 transform(它不是 GameObject)。\n" +
             "扩展方法:新建 BulletModifier 子类 + 加 [SRName(\"Modifier/<名字>\")] —— 自动出现在下拉菜单。")]
    [SerializeReference, SR]
    public BulletModifier[] ModifierPrefabs;

    [Header("Fire Extensions (Pipeline 模块数组,按顺序串成角度管道)")]
    [Tooltip("基础发射逻辑的模块数组 —— 每个模块是 FireExtension 子类实例,按数组顺序串成\"角度管道\":\n" +
             "  - 空数组 / null = 默认模式:中心方向 = 270° + rotationRad(等价旧版 default)\n" +
             "  - FireExtension/Base:覆盖型,把角度设为 BaseAngle + rotationRad(通常放数组第一位作\"锚点\")\n" +
             "  - FireExtension/Player Aim:覆盖型 —— 瞄得到玩家 → 角度设为指向玩家;瞄不到 → 透传上游角度\n" +
             "  - FireExtension/Offset Angle:累加型 —— 在上一步角度上叠加 N°(逆时针为正);常配合 PlayerAim 实现 \"绕后弹\"\n" +
             "\n" +
             "★ 拼装示例 ★\n" +
             "  [Base(270°)]                              → 始终向下\n" +
             "  [Base(270°), PlayerAim]                   → 瞄不到玩家时向下,瞄得到时改指向玩家\n" +
             "  [Base(0°), PlayerAim]                     → 瞄不到时向右,瞄得到时改指向玩家\n" +
             "  [PlayerAim, Offset Angle(+180°)]          → 玩家方向的反方向(瞄准玩家但飞向玩家背后 —— \"绕后弹\")\n" +
             "  [PlayerAim, Offset Angle(+30°)]           → 玩家方向 + 30°(从玩家右侧掠过)\n" +
             "  [Base(270°), PlayerAim, Offset Angle(+180°)] → 瞄得到:玩家反方向;瞄不到:向下\n" +
             "\n" +
             "扩展方法:新建 FireExtension 子类 + 加 [SRName(\"FireExtension/<名字>\")] —— 自动出现在所有 FirePattern 资产的下拉菜单。\n" +
             "Pipeline 细节:见 Assets/Scripts/Bullet/FireExtension/FireExtension.cs 顶部注释。")]
    [SerializeReference, SR]
    public FireExtension[] FireExtensions;

    [Header("Fire Sounds (开火音多态模块,按数组顺序并行触发)")]
    [Tooltip("开火音模块数组 —— 每次 BulletPool.FireGroup 调用都会按顺序触发所有模块。\n" +
             "★ 与 FireExtensions 的区别 ★\n" +
             "  - FireExtensions = 角度管道(上一步输出角度 → 下一步输入角度)\n" +
             "  - FireSounds     = 并行触发器(每个模块独立播一个音,可叠播多个 cue)\n" +
             "\n" +
             "触发时机:BulletPool.FireGroup 入口 → pattern.Fire(...) 之前 → PlayFireSounds()。\n" +
             "CompositeFirePattern 的子 pattern 不重复触发(只在最外层触发一次)。\n" +
             "留空数组 = 不播放(性能开销 ≈ 0)。\n" +
             "\n" +
             "扩展方法:新建 FireSound 子类 + 加 [SRName(\"FireSound/<名字>\")],Inspector 自动出现。\n" +
             "与 PlayerShooting._shootSfx 的区别(可并存):\n" +
             "  - _shootSfx  = 玩家整体开火音(不论哪个 pattern)\n" +
             "  - FireSound  = 特定 pattern 的特征音(同一 pattern 在玩家 vs 敌人可配不同 cue)\n" +
             "详见 Assets/Scripts/Audio/README.md §6.5 / Assets/Scripts/Bullet/FireExtension/FireSound.cs。")]
    [SerializeReference, SR]
    public FireSound[] FireSounds;

    [Header("Motion (可被子类覆盖)")]
    public float Speed = 5f;
    public float AngularSpeed = 0f;

    [Header("Combat")]
    [Tooltip("子弹命中敌人时的伤害值。\n" +
             "玩家弹用:CollisionService 会按 b.Damage 调 enemy.TakeDamage(b.Damage)。\n" +
             "敌人弹用:本字段会被忽略 — 敌人弹命中玩家直接走 player.OnHit(1),无视 Damage。\n" +
             "STG 默认 1(一击毙小怪);想做多击高伤可调到 2+。")]
    public float Damage = 1f;

    /// 由池/Enemy 调用：发射一组子弹。prefab 取自本对象的 BulletPrefab。
    /// 子类负责在内部调 SpawnBullet(...) 把 Damage + owner 的 Hitbox.Team 透传给每颗新生成的子弹。
    /// </summary>
    /// <param name="position">发射位置</param>
    /// <param name="rotationRad">整体朝向增量(弧度)</param>
    /// <param name="pool">BulletPool(子类从池里 Get 新子弹)</param>
    /// <param name="ownerHitbox">发射者 Hitbox(可为 null)。null 时子弹阵营 = Neutral。</param>
    /// <param name="extraModifiers">调用方(FireAction)临时追加的 modifier,在 ModifierPrefabs 之后追加。null = 不追加。</param>
    public abstract void Fire(Vector2 position, float rotationRad,
                              BulletPool pool, ShinySTG.Hitbox.HitboxComponent ownerHitbox = null,
                              BulletModifier[] extraModifiers = null);

    /// <summary>
    /// 本次 Fire() 调用会发射多少颗子弹。Composite 需要递归求和。
    /// Boss 系统的 ShotsFiredSignal 用它做全局开火计数。
    /// </summary>
    public virtual int GetFireCount() => 0;

    /// <summary>
    /// 触发 FireSounds 数组里的所有开火音模块。
    /// 由 BulletPool.FireGroup 在调 pattern.Fire(...) 之前调用一次(每次"开火组"触发一次)。
    ///
    /// 子类可 override 此方法做更复杂行为(例如 Composite 改成"遍历所有子 pattern 各触发一次"),
    /// 默认实现 = 按数组顺序串行触发每个模块。留空数组 / null 跳过(零开销)。
    ///
    /// 注意:此方法与 pattern.Fire(...) 解耦 —— 即使某次 Fire() 因为弹药耗尽等条件没真的发射子弹,
    /// 已经调过本方法播过音。这是预期行为(开火意图已发生,与子弹是否成功生成无关)。
    /// 若子类需要"实际生成子弹时才播",应在 Fire() 内的 SpawnBullet 处手动调 FireSound。
    /// </summary>
    public virtual void PlayFireSounds(Vector2 position, ShinySTG.Hitbox.HitboxComponent ownerHitbox)
    {
        if (FireSounds == null) return;
        for (int i = 0; i < FireSounds.Length; i++)
        {
            var s = FireSounds[i];
            if (s != null) s.OnFireTriggered(position, ownerHitbox);
        }
    }

    /// 便捷方法：基于某颗子弹（敌人自身）的位置发射。
    public void FireFromOwner(Bullet owner, float rotationRad, BulletPool pool)
    {
        // 旧 API 兼容:从子弹 GameObject 上读 Hitbox。
        var hb = owner != null ? owner.GetComponent<ShinySTG.Hitbox.HitboxComponent>() : null;
        Fire(owner.transform.position, rotationRad, pool, hb);
    }

    /// <summary>
    /// 子类统一通过本方法生成子弹,而不是直接调 pool.Get。
    /// 内部会合并 ModifierPrefabs + extras,传给 pool 的 modifier 挂载重载。
    /// </summary>
    protected Bullet SpawnBullet(BulletPool pool, Vector2 pos, float rad,
                                 float speed, float angularSpeed, float damage,
                                 ShinySTG.Hitbox.CollisionTeam team,
                                 BulletModifier[] extraModifiers)
    {
        var combined = CombineArrays(ModifierPrefabs, extraModifiers);
        return pool.Get(BulletPrefab, pos, rad, speed, angularSpeed, damage, team, combined);
    }

    /// <summary>把 pattern 的 modifier 和调用方追加的 modifier 拼成一个数组。null-safe。</summary>
    static BulletModifier[] CombineArrays(BulletModifier[] a, BulletModifier[] b)
    {
        int alen = a?.Length ?? 0;
        int blen = b?.Length ?? 0;
        if (alen == 0) return b;
        if (blen == 0) return a;
        var result = new BulletModifier[alen + blen];
        a.CopyTo(result, 0);
        b.CopyTo(result, alen);
        return result;
    }
}



