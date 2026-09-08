using ShinySTG.Level;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ShinySTG.Level.Editor.Drawers
{
    /// <summary>
    /// 抽屉注册表:启动时一次性反射扫描所有 ISpawnEntryDrawer 实现 + 缓存到 Dictionary<Type, ISpawnEntryDrawer>。
    /// 运行时按 entry.GetType() 查表,O(1)。
    ///
    /// 自动发现流程:
    ///   1. Domain Reload 时,所有 ISpawnEntryDrawer 子类被实例化
    ///   2. 对每个 SpawnEntry 子类,选第一个 Handles 返回 true 的抽屉绑定
    ///   3. 未匹配的 SpawnEntry 类型 → 走 DefaultSpawnEntryDrawer 兜底
    /// </summary>
    public static class LevelEditorDrawerRegistry
    {
        static readonly Dictionary<Type, ISpawnEntryDrawer> _cache = new();
        static readonly ISpawnEntryDrawer _fallback = new DefaultSpawnEntryDrawer();

        /// <summary>某个 entry 类型绑定的具体抽屉(供高级用法)。返回 null 表示 entry 本身是 null,调用方负责跳过。</summary>
        public static ISpawnEntryDrawer Resolve(SpawnEntry entry)
        {
            if (entry == null) return null;  // 调用方 null-check,避免后续 GetColor(null) NRE
            var t = entry.GetType();
            if (_cache.TryGetValue(t, out var d)) return d;

            // 兜底:缓存里没有(新加的 SpawnEntry 子类),即时扫一遍所有抽屉
            foreach (var drawer in EnumerateDrawers())
            {
                if (drawer.Handles(entry))
                {
                    _cache[t] = drawer;
                    return drawer;
                }
            }
            _cache[t] = _fallback;
            return _fallback;
        }

        /// <summary>手动触发一次重扫(用于"脚本运行时改了 drawer 实现"等场景;大多数情况无需调)。</summary>
        public static void Rebuild()
        {
            _cache.Clear();
        }

        static IEnumerable<ISpawnEntryDrawer> EnumerateDrawers()
        {
            // TypeCache 是 Unity 2019.1+ 的反射缓存,比 AppDomain 反射快一个量级
            return TypeCache.GetTypesDerivedFrom<ISpawnEntryDrawer>()
                            .Where(t => !t.IsAbstract && !t.IsInterface)
                            .Where(t => t != typeof(DefaultSpawnEntryDrawer))
                            .Select(Activator.CreateInstance)
                            .Cast<ISpawnEntryDrawer>();
        }
    }
}