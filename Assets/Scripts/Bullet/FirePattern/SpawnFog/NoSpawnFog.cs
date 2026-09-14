using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 显式"不使用雾化"占位选项 —— 让"下拉菜单"里有 None 选项,不用让用户清字段。
///
/// 行为:Duration=0 + ApplyVisual/ClearVisual 都是空实现,与"字段为 null"100% 等价。
/// 选这个 vs 选 None(字段为 null)的差别:
///   - null 字段 = SpawnFog 字段本身没东西(传统语义)
///   - NoneSpawnFog = 明确告诉读者"这个 pattern 故意选择了不雾化"(更直观,可追溯意图)
///
/// 推荐用法:在不希望雾化但又想保留"下拉选了 None"的明确意图时使用本类;
/// 想完全无 SpawnFog 字段时,直接把 FirePattern.SpawnFog 字段拖空(也是合法状态)。
/// </summary>
[Serializable, SRName("Spawn Fog/None")]
public class NoSpawnFog : SpawnFogConfig
{
    public override void ApplyVisual(Bullet b, float t01, Vector3 baseLocalScale)
    {
        // 空实现 —— 不雾化(等价 Duration=0)。
        // Bullet.Update 在 IsFogged=true 时才会调 ApplyVisual;
        // 而 IsFogged = FogDuration > 0 && FogElapsed < FogDuration;
        // Duration=0 时 IsFogged 恒为 false → 本方法实际永远不会被调用。
        // 但保留空实现以满足抽象基类契约。
    }

    public override void ClearVisual(Bullet b, Vector3 baseLocalScale)
    {
        // 空实现 —— 同上,实际不会被调用。
    }
}
