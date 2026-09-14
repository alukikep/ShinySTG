using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 子弹"出生雾化"配置的多态基类(对齐 FireExtension / BulletModifier / MoveBehaviour 的扩展套路)。
///
/// 挂在 FirePattern.SpawnFog 上,字段类型是 [SerializeReference, SR],可下拉选不同子类型实现:
///   - (字段为 null)                        → 不雾化,与历史行为 100% 等价
///   - [SRName("Spawn Fog/None")]           → 显式"不雾化"占位选项,Duration=0,行为与 null 等价
///   - [SRName("Spawn Fog/Default")]        → 默认基础雾化(染色 + 缩放 + 缓动)
///   - [SRName("Spawn Fog/<中雾化>")]     → 后续扩展:每个新实现一个子类 + [SRName] 即可自动出现在下拉
///
/// 雾化期内的子弹行为(由 Bullet.cs 统一执行,Bullet.Update 内 FogDuration>0 时 early-return):
///   - 位置固定(不按 Speed 移动)
///   - 不参与碰撞(HitboxComponent.IsFogged=true,CollisionService 各 Tick 阵营过滤后会 continue)
///   - modifier 时间窗口计时器不累计(雾化结束 → 时间窗口从 0 开始)
///   - 视觉由子类 override ApplyVisual(Bullet b, float t01) 实现;默认走 STG/BulletTintFog shader
///
/// 复用安全:回池再发射时,Bullet.Init 会重置 FogElapsed / FogDuration / FogCfg / 视觉参数。
///
/// 与 FirePattern.ModifierPrefabs 的关系:
///   - ModifierPrefabs = 子弹生成后的持续行为(追踪 / 加速 / 分裂 / 颜色...)
///   - SpawnFog       = 仅"出生瞬间"的视觉/碰撞/运动压制
///   - 两者正交,互不冲突 —— SpawnFog 期间 modifier 不动,雾化结束 modifier 从头开始计时。
///
/// 扩展方法:新建 SpawnFogConfig 子类 + 加 [SRName("Spawn Fog/<名字>")] —— 自动出现在所有 FirePattern 资产的 SpawnFog 下拉菜单。
/// </summary>
[Serializable]
public abstract class SpawnFogConfig
{
    [Tooltip("雾化持续时间(秒)。0 = 不雾化(默认,与历史行为 100% 等价)。\n" +
             "STG 经典值:0.10~0.25(短促);Boss 警示弹:0.3~0.5(给玩家反应时间)。\n" +
             "Duration=0 时,所有视觉字段全部忽略,子弹按普通方式发射。\n" +
             "★ 该字段是基类约定:子类可以 override 但默认就够用。\n" +
             "★ '不使用雾化' 推荐用下拉里的 NoneSpawnFog(更直观)而不是 Duration=0。")]
    [Min(0f)] public float Duration = 0f;

    /// <summary>
    /// 雾化期内每帧调用:子类把自定义视觉/逻辑写到子弹上。
    /// </summary>
    /// <param name="b">子弹实例(可用字段见 Bullet.cs 顶部注释)</param>
    /// <param name="t01">雾化进度,0 = 出生瞬间(全雾),1 = 雾化结束(清晰)</param>
    /// <param name="baseLocalScale">prefab 美术缩放基准(Init 时缓存,典型 0.28)。子类若做缩放,务必走乘法叠加,不要覆盖。</param>
    public abstract void ApplyVisual(Bullet b, float t01, Vector3 baseLocalScale);

    /// <summary>
    /// 雾化结束后调用一次:子类复位所有视觉修改,让 BulletColorModifier / 普通 tint 完全接管渲染。
    /// 常见实现:transform.localScale = baseLocalScale + Bullet.ApplyFogMaterialParams(0f, ...) 清 _FogAmount。
    /// 子类若写过其它 shader 属性,override 此方法做对应复位。
    /// </summary>
    public abstract void ClearVisual(Bullet b, Vector3 baseLocalScale);
}

/// <summary>
/// 雾化期 _FogAmount 的缓动函数(影响'凝聚'节奏,与视觉是否切换的逻辑完全无关)。
/// 默认 None = 线性;用户可选 EaseOut / EaseIn / EaseInOut 给雾化期不同节奏感。
/// 放在 SpawnFogConfig.cs 顶层以便所有 SpawnFogConfig 子类共享(DefaultSpawnFog 默认用)。
/// </summary>
public enum FogEasing
{
    None,        // 线性:默认
    EaseOut,     // 前期快,后期慢(出生瞬间强,凝聚慢)
    EaseIn,      // 前期慢,后期快(酝酿感)
    EaseInOut,   // 两头慢,中间快
}

/// <summary>
/// 共享缓动函数(供所有 SpawnFogConfig 子类的 ApplyVisual 调用)。
/// 输入 t ∈ [0,1],输出 easedT ∈ [0,1]。
/// </summary>
public static class FogEasingUtil
{
    public static float Apply(float t, FogEasing e)
    {
        switch (e)
        {
            case FogEasing.None: return t;
            case FogEasing.EaseOut: return 1f - (1f - t) * (1f - t);                // 前期快,后期慢
            case FogEasing.EaseIn: return t * t;                                    // 前期慢,后期快
            case FogEasing.EaseInOut:
                return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);       // 两头慢,中间快
            default: return t;
        }
    }
}