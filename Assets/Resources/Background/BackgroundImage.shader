Shader "Hidden/ShinySTG/BackgroundImage"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _UvTransform ("UV", Vector) = (1,1,0,0)
        _SpriteRegion ("Sprite Region", Vector) = (1,1,0,0)
        _SpriteContent ("Sprite Content", Vector) = (1,1,0,0)
        _Repeat ("Repeat", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color, _UvTransform;
            float4 _SpriteRegion, _SpriteContent, _MainTex_TexelSize;
            float _Repeat;
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(uint id : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((id << 1) & 2, id & 2);
                output.position = float4(uv * 2 - 1, 0, 1);
                output.position.y *= _ProjectionParams.x;
                output.uv = uv * _UvTransform.xy + _UvTransform.zw;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float2 local = _Repeat > 0.5 ? frac(input.uv) : saturate(input.uv);
                local = (local - _SpriteContent.zw) / _SpriteContent.xy;
                float visible = step(0, local.x) * step(local.x, 1) * step(0, local.y) * step(local.y, 1);
                float2 uv = saturate(local) * _SpriteRegion.xy + _SpriteRegion.zw;
                float2 inset = min(abs(_MainTex_TexelSize.xy) * 0.5, _SpriteRegion.xy * 0.5);
                uv = clamp(uv, _SpriteRegion.zw + inset, _SpriteRegion.zw + _SpriteRegion.xy - inset);
                float4 color = tex2D(_MainTex, uv) * _Color;
                color.a *= visible;
                return color;
            }
            ENDCG
        }
        // Pure-color transition is independent of Sprite atlas sampling and UV clipping.
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float4 _Color;
            float4 vert(uint id : SV_VertexID) : SV_POSITION
            {
                float2 uv = float2((id << 1) & 2, id & 2);
                float4 position = float4(uv * 2 - 1, 0, 1);
                position.y *= _ProjectionParams.x;
                return position;
            }
            float4 frag() : SV_Target { return _Color; }
            ENDCG
        }
    }
}
