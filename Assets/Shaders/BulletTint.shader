// STG 子弹染色 + 出生雾化 shader —— 二合一。
//
// 主通道(原有 tint):
//   - _TintColor 只染暗部,亮部(白色高光)保留 — 经典 STG 子弹表现
//   - BulletColorModifier 通过 MaterialPropertyBlock 写 _TintColor(per-instance)
//
// 雾化通道:
//   - _FogAmount:0 = 清晰(等价历史);1 = 完全被 _FogColor 覆盖(雾团)
//   - 默认 _FogAmount=0,未启用雾化的子弹零分支开销(只多一个 uniform 读)
//
// 视觉缩放走 C# 端 transform.localScale(由 Bullet.cs 在 SpawnFog 期写 _baseLocalScale * fogScale),
// shader vertex 完全不动 → 100% 不产生位置偏移(与 FogStartScale 字段语义一致:FogStartScale=1.4 表示
// 出生瞬间大小是正常的 1.4 倍)。
//
// 与 SpriteRenderer.color / MaterialPropertyBlock 的协作:
//   - 必须通过 MaterialPropertyBlock 传 _TintColor / _FogAmount(per-instance,SRP Batcher 友好)。
//   - 不用 material.instance(避免破坏 SRP Batcher)。
//
// 适用:
//   - 子弹染色(黑白灰素材 → 各种颜色),与 BulletColorModifier 配套
//   - 出生雾化(由 Bullet.cs 在 FirePattern.SpawnFog 期间通过 MPB 写 _FogAmount + transform.localScale)
//
// 性能:一个 Pass,per-instance MPB,不破坏 batching。

Shader "STG/BulletTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor      ("Tint Color (RGB = tint, A = strength)", Color) = (1, 1, 1, 1)
        _LuminanceMin   ("Luminance Min (below = full tint)", Range(0, 1)) = 0.0
        _LuminanceMax   ("Luminance Max (above = no tint)", Range(0, 1)) = 0.65

        // 出生雾化通道(per-instance,MaterialPropertyBlock 写入)
        _FogAmount      ("Fog Amount (1 = full fog, 0 = clear)", Range(0, 1)) = 0.0
        _FogColor       ("Fog Color (覆盖雾化期整体颜色)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
            "RenderType"     = "Transparent"
            "PreviewType"    = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // 预乘 alpha 输出:output_rgb = tex.rgb + (1-tex.a) * dst

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4    _TintColor;
            float     _LuminanceMin;
            float     _LuminanceMax;

            // 出生雾化 uniforms(per-instance,MPB)
            float     _FogAmount;
            float4    _FogColor;

            v2f vert(appdata_t v)
            {
                // vertex 完全不动 —— 视觉缩放走 C# 端 transform.localScale(Bullet.ApplyFogVisual)。
                // 之前 _FogScale vertex 写法在 FogStartScale>1 时 Unity 内部出现"快速移动"视觉异常,
                // 改用 transform.localScale 乘法后语义直观且不触发该 bug。
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color    = v.color;
                #ifdef PIXELSNAP_ON
                    OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : COLOR
            {
                // 1. 采样原始贴图(RGBA,LDR)
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                // 2. 亮度感知权重(Rec.709)
                //    黑色 (0,0,0) → lum = 0,完全染色
                //    白色 (1,1,1) → lum = 1,完全不染色
                float lum = dot(tex.rgb, float3(0.2126, 0.7152, 0.0722));

                // 3. 平滑过渡:min 以下满染色,max 以上完全不染
                float tintWeight = 1.0 - smoothstep(_LuminanceMin, _LuminanceMax, lum);

                // 4. 染色权重叠加 TintColor.a(让用户能整体调染色强度)
                float finalWeight = tintWeight * _TintColor.a;

                // 5. 合成:暗像素 → tex * _TintColor.rgb,亮像素 → tex
                fixed3 tinted = lerp(tex.rgb, tex.rgb * _TintColor.rgb, finalWeight);

                // 6. 基础 alpha(预乘输出专用)
                fixed baseAlpha = tex.a * lerp(1.0, _TintColor.a, finalWeight * 0.5);

                // ═══════════════════════════════════════════════════════════
                // 出生雾化合成:
                //   _FogAmount=1:整张弹被 _FogColor 替换(= 雾团)
                //   _FogAmount=0:等价上面的 tinted(正常 tint)
                //   中间值:lerp(清晰, 雾团)
                //   雾化期整体 alpha 提升(雾团更不透明,聚焦视觉),清晰后回到 baseAlpha
                // ═══════════════════════════════════════════════════════════
                fixed3 finalRgb   = lerp(tinted, _FogColor.rgb, _FogAmount);
                fixed  finalAlpha = lerp(baseAlpha, baseAlpha + (1.0 - baseAlpha) * 0.6, _FogAmount);

                // 7. 预乘 alpha 输出(配合 Blend One OneMinusSrcAlpha)
                return fixed4(finalRgb * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
