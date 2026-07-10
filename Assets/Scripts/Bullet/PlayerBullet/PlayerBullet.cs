using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    public float speed;
    public float damage;

    protected virtual void Update()
    {
        // 检查子弹是否超出回收区域
        CheckBoundsAndRecycle();
    }
    
    /// <summary>
    /// 检查子弹是否超出回收区域，如果超出则回收到对象池
    /// </summary>
    protected void CheckBoundsAndRecycle()
    {
        BulletRecycleArea area = FindObjectOfType<BulletRecycleArea>();
        if (area != null && area.IsOutOfBounds(transform.position))
        {
            Pool.Despawn(gameObject);
        }
    }
}
