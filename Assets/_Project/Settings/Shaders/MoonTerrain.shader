// AI-assisted: written with Claude. See docs/AI_USE.md
// Шейдер рельєфу Місяця (MoonTerrain): текстура реголіту Юлі, накладена згори у світових координатах
// (на схилах не розтягується по-різному на кожному шматку), і мальоване світло: освітлені схили —
// повний колір, схили від Сонця — у тіні (_ShadowColor). Так видно кратери, пагорби й хребет.
//  _TileSize   — скільки метрів займає одна копія текстури
//  _LightRef   — звичайна яскравість сонця; коли OutdoorDarkening її зменшує (інженер у модулі), земля темнішає
//  колір вершин — дно кратерів з льодом темне й холодне (ставить MoonTerrain)
Shader "Moon/Terrain"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _TileSize ("Texture Tile Size (m)", Float) = 10
        _ShadowColor ("Shadow Tint", Color) = (0.5, 0.5, 0.66, 1)
        _LightRef ("Normal Sun Brightness", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _TileSize;
            half4 _ShadowColor;
            float _LightRef;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "TerrainForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz / max(_TileSize, 0.01);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb * i.color.rgb;

                Light sun = GetMainLight();
                half3 n = normalize(i.normalWS);
                half ndl = dot(n, sun.direction);

                // Painted light: lit / half / shadow bands with soft edges. Flat ground under the low polar Sun is fully lit.
                half lit = smoothstep(0.12h, 0.2h, ndl) * 0.5h + smoothstep(-0.14h, -0.06h, ndl) * 0.5h;
                half3 col = albedo * lerp(_ShadowColor.rgb, half3(1, 1, 1), lit);
                // Gentle slopes (waves, hills) stay in the lit band — add soft shading so they are still visible.
                // Flat ground = exactly 1 (compared with the Sun height), slopes towards the Sun a bit brighter, away a bit darker.
                col *= clamp(1.0h + 0.6h * (ndl - sun.direction.y), 0.75h, 1.2h);

                // Outdoor darkening (engineer inside a module): follow the Sun brightness
                half lum = dot(sun.color, half3(0.299h, 0.587h, 0.114h));
                col *= saturate(lum / max(_LightRef, 0.01));
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------- Depth (used by URP depth prepass) ----------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            float4 vertDepth(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragDepth(float4 positionCS : SV_POSITION) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
