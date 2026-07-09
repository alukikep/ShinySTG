using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NatsuhaABullet : PlayerBullet
{
    void Update()
    {
        transform.Translate(new Vector3(0, speed, 0) * Time.deltaTime, Space.World);
    }
}
