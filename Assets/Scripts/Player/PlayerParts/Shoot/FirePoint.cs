using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirePoint : MonoBehaviour
{

    public void Fire(Vector3 pointPosition, GameObject bullet, float speed, float damage)
    {
        Vector3 firePosition = transform.TransformPoint(pointPosition);
        BulletFactory.Instance.SpawnPlayerBullet(firePosition, bullet, speed, damage);
    }
}
