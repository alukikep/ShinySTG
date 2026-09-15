namespace ShinySTG.Laser
{
    /// <summary>
    /// 东方风格激光五段式生命周期。
    /// 对齐参考材料 §18:
    ///   Warning    → 不碰撞,显示预警线
    ///   Expanding  → 长度从 0 插值到 TargetLength(碰撞策略可配)
    ///   Active     → 全长 + 碰撞开启
    ///   Shrinking  → 长度从 TargetLength 收到 0(碰撞策略可配)
    ///   Dead       → 回收
    /// </summary>
    public enum LaserState
    {
        Warning,
        Expanding,
        Active,
        Shrinking,
        Dead,
    }
}
