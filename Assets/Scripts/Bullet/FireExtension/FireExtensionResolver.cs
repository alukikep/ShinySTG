using UnityEngine;

/// <summary>
/// FirePattern 子类统一通过本 helper 解析"角度"。
///
/// null-safe 设计:extension == null 时 fallback 到"默认 270° + rotationRad",
/// 等价于旧版"直接用 BaseAngle=270 算"的语义 —— 保证旧资产零行为变更
/// (旧资产反序列化后 FireExtension 字段 = null,行为 = 270° + rotationRad)。
///
/// 这里故意没有"none 子类"——extension == null 本身就是 normal 状态,
/// 不需要再造一个 FireExtension/None 子类把 null 再封装一层。
///
/// 未来扩展:新增"每发独立方向"的 FireExtension 子类(override ComputeAngleRadForBullet),
/// 子类用 ResolveBulletAngle(...) 即可。详见 FireExtension.cs 注释。
/// </summary>
public static class FireExtensionResolver
{
    /// <summary>
    /// 解析本轮发射的中心方向(弧度)。null-safe。
    /// </summary>
    /// <param name="extension">扩展点(可为 null)</param>
    /// <param name="from">发射点位置</param>
    /// <param name="rotationRad">外部传入的整体方向增量</param>
    public static float ResolveCenterAngle(FireExtension extension, Vector2 from, float rotationRad)
        => extension != null ? extension.ComputeAngleRad(from, rotationRad) : (270f * Mathf.Deg2Rad + rotationRad);

    /// <summary>
    /// 解析本轮发射中第 bulletIndex 颗子弹的方向(弧度)。null-safe。
    /// 默认 extension == null → 等价于"中线 = 270° + rotationRad"(由调用方按形态公式分摊)。
    /// </summary>
    public static float ResolveBulletAngle(FireExtension extension, Vector2 from, int bulletIndex, int totalCount, float rotationRad)
        => extension != null ? extension.ComputeAngleRadForBullet(from, bulletIndex, totalCount, rotationRad) : (270f * Mathf.Deg2Rad + rotationRad);
}
