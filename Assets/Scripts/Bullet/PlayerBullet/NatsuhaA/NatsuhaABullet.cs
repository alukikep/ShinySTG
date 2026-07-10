using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NatsuhaABullet : PlayerBullet
{
    protected override void Update()
    {
        // 移动子弹
        transform.Translate(new Vector3(0, speed, 0) * Time.deltaTime, Space.World);

        // 调用父类的边界检测和回收逻辑
        base.Update();
    }
}
