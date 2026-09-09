// STG 子弹染色 shader —— 只对"暗部"染色,亮部(白色高光)保持原色。
//
// 物理原理:
//   Unity 内置 Sprites/Default 走 tex * color 乘法,白色像素会被完全染成主色。
//   本 shader 把"染色权重"和"像素亮度"挂钩:
//     - 暗像素 (luminance → 0):weight = 1,完全染色 → tex * _TintColor
//     - 亮像素 (luminance → 1):weight = 0,完全保持 → tex
//   中间像素按 smoothstep 平滑过渡。
//
// 与 SpriteRenderer.color / MaterialPropertyBlock 的协作:
//   - 必须通过 MaterialPropertyBlock 传 _TintColor(per-instance,SRP Batcher 友好)。
//   - 不用 material.instance(避免破坏 SRP Batcher)。
//   - _TintColor.a 控制整体染色强度;0 = 完全不染色(显示原黑白图)。
//
// 适用:
//   - STG 黑白灰 bullet 素材 → 主炮红 / 子机蓝 / Boss 紫(白色高光保留)。
//   - 与 BulletColorModifier(modifier 内部走 MPB)完美配套。
//
// 不适用:
//   - 需要"亮像素也染色"或"全图单色化"的场景 —— 用 ParticleSystem 或自定义。
//   - HDR / 自发光子弹 —— 本 shader 是纯 LDR tint,需要的话后续加 _Emission。
//
// 性能:
//   - 顶点数 / draw call 与 Sprites/Default 同级(一个 Pass)。
//   - per-instance 通过 MPB 传 _TintColor,不破坏 batching。

Shader "STG/BulletTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor      ("Tint Color (RGB = tint, A = strength)", Color) = (1, 1, 1, 1)
        _LuminanceMin   ("Luminance Min (below = full tint)", Range(0, 1)) = 0.0
        _LuminanceMax   ("Luminance Max (above = no tint)", Range(0, 1)) = 0.65
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

            v2f vert(appdata_t v)
            {
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

                // 6. 预乘 alpha 输出(配合 Blend One OneMinusSrcAlpha)
                //    TintColor 也会降低 alpha 让暗部稍微更"轻",视觉更协调
                fixed alpha = tex.a * lerp(1.0, _TintColor.a, finalWeight * 0.5);
                return fixed4(tinted * alpha, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
