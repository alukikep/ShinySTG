using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 默认基础雾化:染色 + 缩放 + 缓动三件套。
/// 这是最早版本的"基础雾化"行为直接搬到子类,行为与历史 SpawnFogConfig 100% 等价。
///
/// 视觉走 STG/BulletTintFog shader:
///   - _FogAmount = 1 - EasedT(全雾 → 清晰)
///   - _FogColor  = FogColor
///   - transform.localScale = _baseLocalScale × Lerp(FogStartScale, 1, EasedT)
///     → prefab 美术基准(典型 0.28)始终保留,雾化期 0.28 × fogScale,清晰后 0.28 × 1 = 0.28
///
/// Hitbox 影响:雾化期 Hitbox.IsFogged=true → CollisionService 各 Tick 跳过,雾化结束自动恢复碰撞。
///
/// 与 BulletColorModifier 的协作:
///   两者都走 MPB。Bullet 内部用 _fogMpb(懒分配)写 _FogAmount / _FogColor,
///   先 GetPropertyBlock(保留 ColorModifier 已写的 _TintColor),再 Set,合并应用。
///   → 子类不要直接访问 Bullet._fogMpb 私有字段,统一走 Bullet.ApplyFogMaterialParams(...) helper。
///
/// 扩展建议(参考):
///   - WaveSpawnFog:雾化期内 sprite 颜色沿波形渐变
///   - TrailSpawnFog:雾化期内生成 ParticleSystem 拖尾
///   - RadialSpawnFog:雾化期内做径向膨胀(脉冲感)
///   - ColorShiftSpawnFog:雾化期内颜色从 A 渐变到 B
/// 每个都是新建 SpawnFogConfig 子类 + 加 [SRName("Spawn Fog/<名字>")] 即可自动出现在下拉菜单。
/// </summary>
[Serializable, SRName("Spawn Fog/Default")]
public class DefaultSpawnFog : SpawnFogConfig
{
    [Tooltip("雾化阶段显示的颜色(覆盖原 _TintColor,雾化结束恢复)。\n" +
             "Alpha=1 完全覆盖;经典 STG 用低饱和白/淡蓝/淡红做'能量凝聚'感。")]
    public Color FogColor = new Color(0.7f, 0.85f, 1f, 1f);

    [Tooltip("雾化期内 sprite 的视觉缩放倍率(从该值 → 1)。\n" +
             "1 = 不缩放,只染色。\n" +
             "1.4 = 出生瞬间大小是正常的 1.4 倍,雾化中收缩到正常(经典'凝聚')。\n" +
             "0.5 = 出生瞬间是正常的 0.5 倍,雾化中放大到正常(反向'展开')。\n" +
             "★ 实现方式:走 transform.localScale = baseLocalScale × fogScale 乘法,\n" +
             "   prefab 的 0.28 美术基准始终保留(雾化期最终 localScale = 0.28 × fogScale,\n" +
             "   清晰后 = 0.28 × 1 = 0.28)。\n" +
             "★ Hitbox 影响:雾化期 IsFogged=true → 不参与碰撞/擦弹;雾化结束恢复 baseLocalScale,判定盒大小正常。\n" +
             "★ 与 shader vertex 缩放的历史对比:vertex 写法在 FogStartScale>1 时出现'雾团快速移动'视觉异常,\n" +
             "   改用 C# 端 transform.localScale 乘法后 100% 不偏移,语义直观。")]
    [Min(0.01f)] public float FogStartScale = 1.4f;

    [Tooltip("雾化阶段视觉淡入的缓动曲线。None=线性(默认),其它可选 EaseOut / EaseIn / EaseInOut。\n" +
             "★ 雾化结束那一帧的 _FogAmount 一定是 0,与缓动无关(确保清晰'切断',不留余像)。")]
    public FogEasing Easing = FogEasing.None;

    public override void ApplyVisual(Bullet b, float t01, Vector3 baseLocalScale)
    {
        if (b.Renderer == null) return;

        float eased = FogEasingUtil.Apply(Mathf.Clamp01(t01), Easing);
        float fogAmount = 1f - eased;                              // 1(全雾)→ 0(清晰)
        float fogScale  = Mathf.Lerp(Mathf.Max(FogStartScale, 0.01f), 1f, eased);

        // ★ 视觉缩放:transform.localScale 乘法叠加在 prefab 美术基准上。
        //   baseLocalScale = 0.28(典型),fogScale=1.4 → 最终 0.392(显示大小 1.4× 正常)。
        //   fogScale=1 → 0.28(正常,等价历史)。
        //   不会破坏 prefab 美术缩放,Hitbox 在雾化期不参与碰撞所以 lossyScale 变化无副作用。
        b.transform.localScale = baseLocalScale * fogScale;

        // shader 参数统一走 Bullet.ApplyFogMaterialParams 内部 helper,
        // Bullet 控制 _fogMpb 的懒分配与生命周期,与 BulletColorModifier 的 _TintColor 合并写入。
        b.ApplyFogMaterialParams(fogAmount, FogColor);
    }

    public override void ClearVisual(Bullet b, Vector3 baseLocalScale)
    {
        // ★ 视觉缩放复位:回到 prefab 美术基准,Hitbox 判定盒大小恢复正常。
        b.transform.localScale = baseLocalScale;
        // _FogAmount=0(让 BulletColorModifier / 普通 tint 完全接管渲染);
        // 不复位 _FogColor(下次再用时 ApplyVisual 会重写)。
        b.ApplyFogMaterialParams(0f, FogColor);
    }
}
