Shader "ShinySTG/Background/Distance Fog Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackgroundUvSpeed ("UV Speed (X/Y tiles per second)", Vector) = (0,0,0,0)
        [HideInInspector] _BackgroundUvOffset ("Runtime UV Offset", Vector) = (0,0,0,0)
        _BackgroundFogColor ("Fog Color", Color) = (0.06,0.08,0.12,1)
        _BackgroundFogStart ("Fog Start", Float) = 35
        _BackgroundFogEnd ("Fog End", Float) = 100
        _BackgroundFogStrength ("Fog Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            Cull Back
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BackgroundUvOffset;
            fixed4 _Color;
            fixed4 _BackgroundFogColor;
            float _BackgroundFogStart;
            float _BackgroundFogEnd;
            float _BackgroundFogStrength;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex) + _BackgroundUvOffset.xy;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float distanceToCamera = distance(input.worldPosition, _WorldSpaceCameraPos);
                float start = max(0.0, _BackgroundFogStart);
                float span = max(0.001, _BackgroundFogEnd - start);
                float fog = saturate((distanceToCamera - start) / span) * saturate(_BackgroundFogStrength);
                fixed3 color = tex2D(_MainTex, input.uv).rgb * _Color.rgb;
                return fixed4(lerp(color, _BackgroundFogColor.rgb, fog), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
