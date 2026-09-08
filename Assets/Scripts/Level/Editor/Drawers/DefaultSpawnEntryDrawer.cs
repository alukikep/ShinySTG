using ShinySTG.Level;
using ShinySTG.Level.SpawnEntries;

namespace ShinySTG.Level.Editor.Drawers
{
    /// <summary>
    /// 默认抽屉:所有未自定义画法的 SpawnEntry 都走这里(继承基类全部默认实现)。
    ///   - 时间轴 block:背景色 + 标签
    ///   - Scene Gizmo:SpawnPosition 画小圆 + 时间标签
    ///
    /// 之所以保留这个空类,是为了:
    ///   1. 与 Registry 里 "Where(t => t != typeof(DefaultSpawnEntryDrawer))" 的排除逻辑保持一致;
    ///   2. 显式锚定"默认实现"语义,后续若想为所有 fallback 加额外行为(比如统计/日志),
    ///      只在这里加,不需要改基类。
    /// </summary>
    public class DefaultSpawnEntryDrawer : ISpawnEntryDrawer
    {
    }
}
