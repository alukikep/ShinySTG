using UnityEngine;

/// <summary>玩法消弹强度；对象生命周期回收不使用此等级。</summary>
public enum BulletClearLevel
{
    [InspectorName("一级：普通消弹")]
    Normal = 0,
    [InspectorName("二级：强力消弹")]
    Strong = 1
}
