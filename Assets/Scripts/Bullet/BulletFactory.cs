using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletFactory : Singleton<BulletFactory>
{

    public void SpawnPlayerBullet(Vector3 pointPosition, GameObject bullet, float speed, float damage)
    {
        var PlayerBullet = Pool.Spawn(bullet, pointPosition, Quaternion.identity, 50);
        PlayerBullet.GetComponent<PlayerBullet>().speed = speed;
        PlayerBullet.GetComponent<PlayerBullet>().damage = damage;

    }

    public void SpawnEnemyBullet()
    {

    }
}
