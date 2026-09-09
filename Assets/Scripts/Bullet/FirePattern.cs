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

    [Header("Fire Extension (可选,下拉选基础发射逻辑的扩展)")]
    [Tooltip("对基础发射逻辑(中线方向)的扩展。\n" +
             "  - 留空(null) = 默认模式:中心方向 = 270°(向下)+ rotationRad(等价旧版 normal)\n" +
             "  - FireExtension/Base:中心方向 = BaseAngle + rotationRad(自己设整体方向)\n" +
             "  - FireExtension/Player Aim:中心方向 = 指向玩家(无玩家时退回 BaseAngle + rotationRad)\n" +
             "扩展方法:新建 FireExtension 子类 + 加 [SRName(\"FireExtension/<名字>\")] —— 自动出现在所有 FirePattern 资产的下拉菜单。")]
    [SerializeReference, SR]
    public FireExtension FireExtension;

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



