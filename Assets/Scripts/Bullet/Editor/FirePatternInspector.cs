using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FirePattern), true)]
public class FirePatternInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var pattern = (FirePattern)target;
        if (pattern is CompositeFirePattern composite)
        {
            EditorGUILayout.HelpBox("组合只使用 Children 和外层 FireSounds；父级子弹、运动、角度、Modifier 与雾化配置不继承。调用方 ExtraModifiers 会传给子项。重复子项按执行次数推进序列。", MessageType.Info);
            if (InvalidTree(composite, new HashSet<FirePattern>()))
                EditorGUILayout.HelpBox("组合存在循环引用或超过 64 层；运行时将跳过该分支。", MessageType.Error);
            if (composite.Children == null || System.Array.Exists(composite.Children, child => child == null))
                EditorGUILayout.HelpBox("空子项会被跳过。", MessageType.Warning);
        }
        int count = pattern is RingFirePattern ring ? ring.Count : pattern is ArcFirePattern arc ? arc.Count : pattern is LineFirePattern line ? line.Count : -1;
        if (!(pattern is CompositeFirePattern) && count <= 0)
            EditorGUILayout.HelpBox("Count <= 0 时不发射。", MessageType.Warning);
        else if (count == 1)
            EditorGUILayout.HelpBox("只发射中线方向的一颗子弹，保留 Radius 出生偏移。", MessageType.Info);
        var seen = new HashSet<FireExtension>();
        if (pattern.FireExtensions != null)
            foreach (var extension in pattern.FireExtensions)
                if (extension != null && !seen.Add(extension))
                {
                    EditorGUILayout.HelpBox("扩展数组包含重复引用，模块会按出现次数执行。", MessageType.Warning);
                    break;
                }
    }

    static bool InvalidTree(FirePattern pattern, HashSet<FirePattern> path)
    {
        if (pattern == null) return false;
        if (path.Count >= BulletPool.MaxPatternDepth || !path.Add(pattern)) return true;
        try
        {
            if (pattern is CompositeFirePattern composite && composite.Children != null)
                foreach (var child in composite.Children)
                    if (InvalidTree(child, path)) return true;
            return false;
        }
        finally { path.Remove(pattern); }
    }
}
