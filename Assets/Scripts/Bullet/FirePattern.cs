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

    /// 由池/Enemy 调用：发射一组子弹。prefab 取自本对象的 BulletPrefab。
    public abstract void Fire(Vector2 position, float rotationRad,
                              BulletPool pool, Bullet owner = null);

    /// <summary>
    /// 本次 Fire() 调用会发射多少颗子弹。Composite 需要递归求和。
    /// Boss 系统的 ShotsFiredSignal 用它做全局开火计数。
    /// </summary>
    public virtual int GetFireCount() => 0;

    /// 便捷方法：基于某颗子弹（敌人自身）的位置发射。
    public void FireFromOwner(Bullet owner, float rotationRad, BulletPool pool)
    {
        Fire(owner.transform.position, rotationRad, pool, owner);
    }
}



