using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    [Header("子机预制体")]
    public GameObject option;
    [Header("子弹预制体")]
    public PlayerBullet playerBullet;
    public PlayerBullet optionBullet;
    [Header("子弹伤害")]
    public float playerDamage;
    public float optionDamage;
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
    public Vector3[] firePointPosition;
    [Header("子机坐标")]
    public Vector3 optionPositionP1;
    public Vector3 optionPositionP1Focus;
    public Vector3[] optionPositionP2;
    public Vector3[] optionPositionP2Focus;
    public Vector3[] optionPositionP3;
    public Vector3[] optionPositionP3Focus;
    public Vector3[] optionPositionP4;
    public Vector3[] optionPositionP4Focus;



    //之后应该还有图片等配置

}
