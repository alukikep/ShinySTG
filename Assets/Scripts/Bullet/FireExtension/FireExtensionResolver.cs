using UnityEngine;

/// <summary>
/// FirePattern 子类统一通过本 helper 解析"角度"。
///
/// ★ Pipeline 调度核心 ★
///   FireExtensions 是一个 FireExtension[] 数组,本类把数组串成"角度管道":
///     center = rotationRad                                       ← 起点
///     for ext in extensions (顺序遍历):
///         center = ext.ProcessAngle(from, rotationRad, center)   ← 串成管道
///     return center
///
///   空数组 / null → fallback 到 "270° + rotationRad"(等价旧版 default)。
///
/// 历史兼容:
///   ResolveCenterAngle / ResolveBulletAngle 仍保留单 FireExtension 版本,
///   但已标 [Obsolete] —— 新代码请走 ResolvePipeline / ResolveBulletPipeline。
/// </summary>
public static class FireExtensionResolver
{
    const float FallbackDeg = 270f;

    /// <summary>
    /// Pipeline 调度:把 extensions 数组串成角度管道,产出中心方向(弧度)。
    /// null-safe:extensions == null 或空 → fallback 到 270° + rotationRad。
    ///
    /// ★ 不修 from ★
    ///   from 是值参,Resolver 不会修改调用方传入的位置,也不会应用 Base.PositionOffset。
    ///   如果想应用 Base 模块的 PositionOffset,请调 <see cref="ResolvePipelineWithOffset"/>。
    ///   (本方法 = 等价"永远不应用 PositionOffset"的版本,保留以备有"只想要方向、不想被位置影响"的场景。)
    /// </summary>
    /// <param name="extensions">扩展点数组(可为 null 或空)</param>
    /// <param name="from">发射点位置(值参,不会被修改)</param>
    /// <param name="rotationRad">外部传入的整体方向增量(也是管道的起点)</param>
    public static float ResolvePipeline(FireExtension[] extensions, Vector2 from, float rotationRad)
    {
        // 空 / null → 兜底
        if (extensions == null || extensions.Length == 0)
            return FallbackDeg * Mathf.Deg2Rad + rotationRad;

        float current = rotationRad; // 起点
        for (int i = 0; i < extensions.Length; i++)
        {
            var ext = extensions[i];
            if (ext == null) continue; // 数组里有 null 元素 → 跳过(等价"该模块不参与")
            current = ext.ProcessAngle(from, rotationRad, current);
        }
        return current;
    }

    /// <summary>
    /// Pipeline 调度 + 位置偏移:把 extensions 数组串成角度管道,同时应用 Base 模块的 PositionOffset。
    ///
    /// ★ 位置偏移应用时机 ★
    ///   - 入口处:找到 FireExtensions[0] 如果是 BaseAngleFireExtension → 把它的 PositionOffset 加到 from 上
    ///   - 然后用修正后的 from 串 pipeline(所有后续模块,包括 PlayerAim 等瞄准类,都用修正后位置)
    ///   - 后续位置(如 RingFirePattern.Radius 起始偏移)与 PositionOffset 正交叠加
    ///
    /// null-safe:extensions == null 或空 → fallback 到 270° + rotationRad,**from 不会被修改**。
    /// </summary>
    /// <param name="extensions">扩展点数组(可为 null 或空)</param>
    /// <param name="from">
    /// 发射点位置(ref 参数)。**会被 Resolver 内部修改**(叠加 Base.PositionOffset)。
    /// 调用方一般把 Fire 方法的 position 形参转成本地变量再传 ref,这样不会污染 FirePattern 调用栈。
    /// </param>
    /// <param name="rotationRad">外部传入的整体方向增量(也是管道的起点)</param>
    public static float ResolvePipelineWithOffset(FireExtension[] extensions, ref Vector2 from, float rotationRad)
    {
        // 空 / null → 兜底,不修 from
        if (extensions == null || extensions.Length == 0)
            return FallbackDeg * Mathf.Deg2Rad + rotationRad;

        // Step 1:应用 Base 模块的 PositionOffset(在 pipeline 入口处,后续所有模块都用修正后的 from)
        // 约定:Base 模块应该放在数组第一位。如果[0]不是 Base,就跳过位置偏移(等价旧版行为)。
        if (extensions[0] is BaseAngleFireExtension base0)
            from += base0.PositionOffset;

        // Step 2:串角度 pipeline
        float current = rotationRad; // 起点
        for (int i = 0; i < extensions.Length; i++)
        {
            var ext = extensions[i];
            if (ext == null) continue; // 数组里有 null 元素 → 跳过(等价"该模块不参与")
            current = ext.ProcessAngle(from, rotationRad, current);
        }
        return current;
    }

    /// <summary>
    /// Pipeline 每发独立方向版:把 extensions 数组串成角度管道,产出第 bulletIndex 颗子弹的方向(弧度)。
    /// null-safe:extensions == null 或空 → fallback 到 270° + rotationRad。
    /// 默认行为:ProcessAngleForBullet 默认实现走 ProcessAngle → 等价"中线 + 等分"语义。
    /// </summary>
    public static float ResolveBulletPipeline(FireExtension[] extensions, Vector2 from,
                                              int bulletIndex, int totalCount, float rotationRad)
    {
        if (extensions == null || extensions.Length == 0)
            return FallbackDeg * Mathf.Deg2Rad + rotationRad;

        float current = rotationRad;
        for (int i = 0; i < extensions.Length; i++)
        {
            var ext = extensions[i];
            if (ext == null) continue;
            current = ext.ProcessAngleForBullet(from, bulletIndex, totalCount, rotationRad, current);
        }
        return current;
    }

    // ---- 旧 API 兼容(单 FireExtension 版本) ----
    // 保留给可能存在的老调用方;新代码请走 ResolvePipeline。

    /// <summary>
    /// [旧 API] 解析本轮发射的中心方向(弧度)。null-safe。
    /// 已弃用:请走 ResolvePipeline(数组) —— 新架构支持模块拼装。
    /// </summary>
    [System.Obsolete("请走 ResolvePipeline(FireExtension[]) —— 新架构支持模块数组 pipeline。")]
    public static float ResolveCenterAngle(FireExtension extension, Vector2 from, float rotationRad)
    {
        if (extension == null) return FallbackDeg * Mathf.Deg2Rad + rotationRad;
        return extension.ComputeAngleRad(from, rotationRad);
    }

    /// <summary>
    /// [旧 API] 解析本轮发射中第 bulletIndex 颗子弹的方向(弧度)。null-safe。
    /// 已弃用:请走 ResolveBulletPipeline。
    /// </summary>
    [System.Obsolete("请走 ResolveBulletPipeline。")]
    public static float ResolveBulletAngle(FireExtension extension, Vector2 from, int bulletIndex, int totalCount, float rotationRad)
    {
        if (extension == null) return FallbackDeg * Mathf.Deg2Rad + rotationRad;
        return extension.ComputeAngleRadForBullet(from, bulletIndex, totalCount, rotationRad);
    }
}
