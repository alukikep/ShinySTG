using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerOption : MonoBehaviour
{
    private PlayerBullet optionBullet;
    private float bulletSpeed;
    private float bulletDamage;
    private FirePoint firePoint;

    void Start()
    {
        firePoint = this.GetComponent<FirePoint>();
    }
    public void SetTargetPosition(Vector3 position)
    {
        transform.localPosition = position;
    }

    public void GetBullet(PlayerBullet bullet, float speed, float damage)
    {
        optionBullet = bullet;
        bulletSpeed = speed;
    }

    public void OptionFire()
    {
        firePoint.Fire(Vector3.zero, optionBullet, bulletSpeed, bulletDamage);
    }
}
