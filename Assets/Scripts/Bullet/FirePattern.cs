using UnityEngine;

/// FirePattern：决定"射什么子弹 + 怎么射"的完整定义。
/// 每个 Pattern SO 自带 BulletPrefab 与基础 Speed/AngularSpeed，
/// 子类可继续在内部覆盖（例如连射速度递增）。
public abstract class FirePattern : ScriptableObject
{
    [Header("Bullet")]
    [Tooltip("该 pattern 发射的子弹 prefab。留空则使用 BulletPool.DefaultPrefab 兜底。")]
    public Bullet BulletPrefab;

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
    /// 子类负责在内部调 pool.Get(...),把 Damage + owner 的 Hitbox.Team 透传给每颗新生成的子弹。
    /// </summary>
    /// <param name="position">发射位置</param>
    /// <param name="rotationRad">整体朝向增量(弧度)</param>
    /// <param name="pool">BulletPool(子类从池里 Get 新子弹)</param>
    /// <param name="ownerHitbox">发射者 Hitbox(可为 null)。null 时子弹阵营 = Neutral。</param>
    public abstract void Fire(Vector2 position, float rotationRad,
                              BulletPool pool, ShinySTG.Hitbox.HitboxComponent ownerHitbox = null);

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
}



