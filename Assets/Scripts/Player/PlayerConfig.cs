using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    [Header("子弹预制体")]
    public GameObject playerBullet;
    public GameObject optionBullet;
    [Header("机体速度")]
    public float normalSpeed;
    public float focusSpeed;
    [Header("射击间隔")]
    public float playerShootingTime;
    public float optionShootingTime;
    [Header("子弹速度")]
    public float playerBulletSpeed;
    public float optionBulletSpeed;
    [Header("发射点坐标")]
    public Vector3 firePointPosition;
    //子机的坐标怎么配置？
    //之后应该还有图片等配置

}
