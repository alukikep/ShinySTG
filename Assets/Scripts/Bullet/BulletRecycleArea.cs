using UnityEngine;

/// <summary>
/// 子弹回收区域组件
/// 挂载在游戏物体上，在Inspector中调整边界并自动显示
/// </summary>
public class BulletRecycleArea : MonoBehaviour
{
    [Header("回收区域边界")]
    public float topBoundary = 15f;      // 上边界
    public float bottomBoundary = -10f;  // 下边界
    public float leftBoundary = -12f;    // 左边界
    public float rightBoundary = 12f;    // 右边界
    
    [Header("显示设置")]
    public Color gizmoColor = Color.cyan; // 边界线颜色
    
    /// <summary>
    /// 检查位置是否超出回收区域
    /// </summary>
    public bool IsOutOfBounds(Vector3 position)
    {
        return position.y > topBoundary || 
               position.y < bottomBoundary || 
               position.x < leftBoundary || 
               position.x > rightBoundary;
    }
    
    /// <summary>
    /// 在Scene视图中绘制边界线
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        
        Vector3 topLeft = new Vector3(leftBoundary, topBoundary, 0);
        Vector3 topRight = new Vector3(rightBoundary, topBoundary, 0);
        Vector3 bottomLeft = new Vector3(leftBoundary, bottomBoundary, 0);
        Vector3 bottomRight = new Vector3(rightBoundary, bottomBoundary, 0);
        
        Gizmos.DrawLine(topLeft, topRight);       // 上边
        Gizmos.DrawLine(topRight, bottomRight);    // 右边
        Gizmos.DrawLine(bottomRight, bottomLeft);  // 下边
        Gizmos.DrawLine(bottomLeft, topLeft);      // 左边
    }
}
