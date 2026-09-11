namespace ShinySTG.Audio
{
    /// <summary>
    /// 总线类型枚举 —— 用 enum 索引比字符串高效,且编译期检查类型安全。
    ///
    /// 与 AudioBus(BusName 字符串)配合:BusMixer 内部维护「enum → AudioBus 实例」映射,
    /// SfxCue.Bus 字段直接挂 AudioBus 资产,BusMixer 按其 BusName 反查对应 enum。
    /// 若用户没设 AudioBus,则 fallback 到按名字匹配:Master/Sfx/Bgm/UI/Env。
    /// </summary>
    public enum AudioBusKind
    {
        Master = 0,
        Bgm    = 1,
        Sfx    = 2,
        UI     = 3,
        // 用户后续可扩展:Voice / Ambient / ...
    }
}
