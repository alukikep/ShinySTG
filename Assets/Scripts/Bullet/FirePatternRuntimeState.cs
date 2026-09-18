using System.Collections.Generic;
public sealed class FirePatternRuntimeState
{
    readonly Dictionary<FireExtension, int> _fireCounts = new();
    readonly Dictionary<FireExtension, FireExtension> _runtimeExtensions = new();
    public IReadOnlyDictionary<FireExtension, int> FireCounts => _fireCounts;

    public FireExtension[] GetRuntimeExtensions(FireExtension[] source)
    {
        if (source == null) return null;
        var result = new FireExtension[source.Length];
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
        return result;
    }

    public IReadOnlyDictionary<FireExtension, int> GetRuntimeFireCounts(FireExtension[] source)
    {
        var result = new Dictionary<FireExtension, int>();
        if (source == null) return result;
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
    }
}
