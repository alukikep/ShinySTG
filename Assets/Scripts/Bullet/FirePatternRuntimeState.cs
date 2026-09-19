using System.Collections.Generic;
/// <summary>
/// 发射者拥有的运行状态。配置数组及元素修改后须 Reset；返回的缓存只供同步批次准备读取。
/// </summary>
public sealed class FirePatternRuntimeState
{
    readonly Dictionary<FireExtension, int> _fireCounts = new();
    readonly Dictionary<FireExtension, FireExtension> _runtimeExtensions = new();
    readonly Dictionary<FireExtension[], FireExtension[]> _extensionArrays = new();
    readonly Dictionary<FireExtension[], Dictionary<FireExtension, int>> _countMaps = new();
    static readonly IReadOnlyDictionary<FireExtension, int> EmptyCounts = new Dictionary<FireExtension, int>();
    public IReadOnlyDictionary<FireExtension, int> FireCounts => _fireCounts;

    public FireExtension[] GetRuntimeExtensions(FireExtension[] source)
    {
        if (source == null) return null;
        if (_extensionArrays.TryGetValue(source, out var result)) return result;
        result = new FireExtension[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var extension = source[i];
            if (extension == null) continue;
            if (!_runtimeExtensions.TryGetValue(extension, out var runtime))
            {
                runtime = extension.Clone();
                _runtimeExtensions.Add(extension, runtime);
            }
            result[i] = runtime;
        }
        _extensionArrays.Add(source, result);
        return result;
    }

    public IReadOnlyDictionary<FireExtension, int> GetRuntimeFireCounts(FireExtension[] source)
    {
        if (source == null) return EmptyCounts;
        GetRuntimeExtensions(source);
        if (!_countMaps.TryGetValue(source, out var result))
        {
            result = new Dictionary<FireExtension, int>();
            _countMaps.Add(source, result);
        }
        result.Clear();
        for (int i = 0; i < source.Length; i++)
        {
            var original = source[i];
            if (original == null || !_fireCounts.TryGetValue(original, out var count)) continue;
            if (!_runtimeExtensions.TryGetValue(original, out var runtime)) continue;
            result[runtime] = count;
        }
        return result;
    }
    public void Advance(FireExtension[] extensions) { if (extensions == null) return; foreach (var e in extensions) if (e != null) { _fireCounts.TryGetValue(e, out var n); _fireCounts[e] = n + 1; } }
    public void Reset()
    {
        _fireCounts.Clear();
        _runtimeExtensions.Clear();
        _extensionArrays.Clear();
        _countMaps.Clear();
    }
}
