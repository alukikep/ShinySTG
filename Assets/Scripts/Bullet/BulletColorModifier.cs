using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 子弹视觉修饰:暗部染色 / 渐变 / 闪烁。
///
/// 适用场景:
///   - 黑白灰 bullet 素材,通过染色变成彩色弹(主炮红 / 子机蓝 / Boss 紫 / ...)。
///   - 同一 FirePattern,不同 FireAction / 不同阶段发不同色(挂在 ExtraModifierPrefabs 即可)。
///   - 子弹飞行途中渐隐 / 闪烁(经典 STG 表现)。
///
/// 工作原理:
///   - 走标准 BulletModifier 套路,挂在 FirePattern.ModifierPrefabs / FireAction.ExtraModifierPrefabs。
///   - 每颗子弹生成时 Clone() 出独立实例,per-instance 状态(_lifetime / _mpb 等)互不干扰。
///   - Modify() 通过 MaterialPropertyBlock 把颜色写入 shader 的 _TintColor(per-instance 不破坏 batching)。
///   - 与配套 shader 'STG/BulletTint' 协作 —— 它只对"暗像素"染色,亮像素(白高光)保持原色。
///
/// ★★★ 必读:为什么本 modifier 必须配 STG/BulletTint shader 用 ★★★
///   Unity 内置 Sprites/Default 走 tex * color 乘法,白色像素会被完全染成主色(失高光)。
///   配套 shader 'STG/BulletTint' 的染色权重 = 1 - luminance(像素亮度感知),从而:
///     - 黑色像素 (lum=0):完全染色 → 渲染为主色 ✓
///     - 灰色像素 (lum=0.5):部分染色 → 渲染为"主色深浅过渡" ✓
///     - 白色像素 (lum=1):完全不染色 → 渲染为纯白 ✓  ★关键★
///   配套 shader 路径:Assets/Shaders/BulletTint.shader。
///
/// ★★★ 使用步骤 ★★★
///   1. 把每个 bullet prefab 的 SpriteRenderer.Material 切到 'STG/BulletTint'(一次性,编辑器里点)。
///   2. FirePattern.ModifierPrefabs 数组加 Modifier/Color,设 Color = 红色。
///   3. 运行时子弹 = 黑边染红 + 白心保持白 = 经典 STG 弹。
///
/// 字段约定:
///   - _lifetime / _solidApplied / _mpb 都是 per-instance 状态。
///   - _mpb 是 MaterialPropertyBlock 引用(Clone 时浅拷没问题,因为 Unity 调用 SetPropertyBlock 时
///     会把 mpb 内部数据复制到 renderer;MPB 自身不在子弹之间共享状态)。
///
/// 与 HomingEnemyModifier 同源套路:走 SerializeReference 多态下拉,无新增 prefab 成本。
/// </summary>
[Serializable, SRName("Modifier/Color")]
public class BulletColorModifier : BulletModifier
{
    /// <summary>染色模式。</summary>
    public enum ColorMode
    {
        /// <summary>出生时一次性染色,之后保持不变。</summary>
        Solid,
        /// <summary>按 _lifetime / MaxLifetime 比例从 Color 渐变到 FadeOutColor(FadeStart 之后才开始)。</summary>
        FadeByLifetime,
        /// <summary>正弦闪烁:alpha 在 [BaseAlpha, 1] 间周期振荡(FlashFrequency Hz)。</summary>
        Flash,
    }

    [Header("Mode")]
    [Tooltip("Solid = 一次性染色(典型主炮/子机/不同关卡的颜色区分)。\n" +
             "FadeByLifetime = 子弹老化时渐变到 FadeOutColor(典型短命散弹 / 余烬)。\n" +
             "Flash = 闪烁(典型高亮 boss 弹 / 受击预警)。")]
    public ColorMode Mode = ColorMode.Solid;

    [Header("Color")]
    [Tooltip("主色。Solid 模式直接用此色;FadeByLifetime 模式的'起点色'。\n" +
             "Alpha 通道在 FadeByLifetime / Flash 模式中也会被改写。")]
    public Color Color = Color.red;

    [Tooltip("仅 FadeByLifetime 模式有效:老化终点的颜色(通常把 alpha 设 0 做渐隐)。")]
    public Color FadeOutColor = new Color(1f, 0f, 0f, 0f);

    [Range(0f, 1f)]
    [Tooltip("仅 FadeByLifetime 模式有效:从 Lifetime 的该比例开始渐变(避免一出生就开始淡)。\n" +
             "0 = 出生即开始;0.5 = 飞行一半寿命才开始淡。")]
    public float FadeStart = 0.5f;

    [Tooltip("仅 FadeByLifetime 模式有效:'参考寿命'估值(秒)。子弹飞行到 ReferenceLifetime 时归一化到 1。\n" +
             "默认 1.5s 覆盖大多数 STG 短命/中命子弹;若子弹实际寿命远超此值,会先到 FadeOutColor 后保持。\n" +
             "若希望按 b.Lifetime 自动归一化,需要在 Bullet 暴露 MaxLifetime 字段 — 暂留接口。")]
    public float ReferenceLifetime = 1.5f;

    [Range(0f, 30f)]
    [Tooltip("仅 Flash 模式有效:闪烁频率(Hz)。5 ≈ 1/12 秒一个周期;10 ≈ 1/6 秒一个周期。")]
    public float FlashFrequency = 5f;

    [Range(0f, 1f)]
    [Tooltip("仅 Flash 模式有效:闪烁的最低 alpha 占比(0 = 闪到完全透明;1 = 不闪烁)。")]
    [SerializeField] float FlashMinAlpha = 0.3f;

    [Header("Renderer Settings")]
    [Tooltip("已废弃(保留仅为兼容旧 .asset):早期版本曾尝试把 SpriteRenderer.drawMode 切到 Additive\n" +
             "—— 但 Unity 的 SpriteDrawMode 枚举只有 Simple/Sliced/Tiled 三个值,没有 Additive。\n" +
             "★ 正确做法:把 bullet prefab 的 SpriteRenderer.Material 切到 STG/BulletTint shader(本 modifier 配套)。\n" +
             "本字段保留不删,如果你已经在 .asset 上勾选了它,Unity 不会再报错(下面没有引用代码);\n" +
             "新项目建议在 Inspector 里取消勾选,避免误导。")]
    [System.Obsolete("SpriteRenderer.drawMode 没有 Additive 值。请把 prefab 的 Material 切到 STG/BulletTint shader。")]
    public bool AutoSetDrawMode = true;

    // per-instance 状态(每颗子弹 Clone 时独立,无污染)
    float _lifetime;
    bool _solidApplied;     // Solid 模式只设一次,避免覆盖美术在 prefab 上配的 sprite 颜色被反复回写(虽然等价)
    MaterialPropertyBlock _mpb;  // per-instance MPB,避免和别的子弹共享状态

    public override void ModifyCore(Bullet b, float dt)
    {
        // 缓存缺失保护:bullet prefab 上若没有 SpriteRenderer(纯 VFX/粒子表现),
        // 安全跳过 — 不要 NRE 阻断其他 modifier。
        if (b == null || b.Renderer == null) return;

        _lifetime += dt;

        switch (Mode)
        {
            case ColorMode.Solid:
                ApplySolid(b);
                break;
            case ColorMode.FadeByLifetime:
                ApplyFade(b);
                break;
            case ColorMode.Flash:
                ApplyFlash(b);
                break;
        }
    }

    /// <summary>
    /// 把颜色写入 MPB 并应用到 Renderer。
    ///
    /// 为什么走 MPB 而不是 Renderer.color:
    ///   - 配套 shader 'STG/BulletTint' 走自定义 _TintColor 字段,不走内置 color。
    ///   - MPB 不会破坏 SRP Batcher / 静态 batching(Renderer.color 会破坏 batching,大弹量场景下掉帧)。
    /// </summary>
    void ApplyTintColor(Bullet b, Color c)
    {
        // 懒分配 per-instance MPB(Clone modifier 时 _mpb 为 null,首帧才创建)
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        b.Renderer.GetPropertyBlock(_mpb);   // 读取 renderer 已有的 mpb(其他 modifier 可能也写过),保留其属性
        _mpb.SetColor("_TintColor", c);     // 覆盖或写入 _TintColor
        b.Renderer.SetPropertyBlock(_mpb);   // 应用
    }

    /// <summary>
    /// Solid:只在首帧设一次。后续不再修改(让美术在 prefab 上配的颜色 / 其他 modifier 不被覆盖)。
    /// 一次性染色足以满足"主炮红 / 子机蓝 / 不同关卡不同色"的典型需求。
    /// </summary>
    void ApplySolid(Bullet b)
    {
        if (_solidApplied) return;
        ApplyTintColor(b, Color);
        _solidApplied = true;
    }

    /// <summary>
    /// FadeByLifetime:从 Color 渐变到 FadeOutColor,渐变区间为 [FadeStart, 1.0] 占 ReferenceLifetime 的比例。
    ///
    /// 注:本 modifier 不持有 b.MaxLifetime(架构上 Bullet.Lifetime 是累计存活时间,没有上限字段)。
    /// 用户通过 ReferenceLifetime 字段控制渐变时基;若想精确"按 b.Lifetime 自动归一化",
    /// 可在 Bullet 暴露 MaxLifetime 字段后扩展本 modifier。
    /// </summary>
    void ApplyFade(Bullet b)
    {
        // 参考寿命 <=0 时兜底 1s,避免除零。
        float refLife = ReferenceLifetime > 0f ? ReferenceLifetime : 1f;
        float t = Mathf.Clamp01(_lifetime / refLife);
        float fadeT = Mathf.InverseLerp(FadeStart, 1f, t);
        ApplyTintColor(b, Color.Lerp(Color, FadeOutColor, fadeT));
    }

    /// <summary>
    /// Flash:TintColor.a 在 [FlashMinAlpha, 1] 间正弦振荡。RGB 保持 Color。
    /// </summary>
    void ApplyFlash(Bullet b)
    {
        float s = 0.5f * (Mathf.Sin(_lifetime * FlashFrequency * Mathf.PI * 2f) + 1f);
        float a = Mathf.Lerp(FlashMinAlpha, 1f, s);
        var c = Color;
        c.a *= a;
        ApplyTintColor(b, c);
    }
}
