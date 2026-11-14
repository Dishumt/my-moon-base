// AI-assisted: written with Claude. See docs/AI_USE.md
// Мультяшний (toon) шейдер для модулів: замість плавного 3D-світла — 3 рівні кольору
// (світло / напівтінь / тінь) і темний контур по краях. Так 3D-будинки виглядають «намальованими»
// і не вибиваються з 2D-спрайтів і пласкої землі.
//  _BaseColor  — колір (ModuleParts фарбує кожну деталь через MaterialPropertyBlock)
//  _Indoor     — 0..1, «світло всередині» (ставить ModuleInterior, коли інженер у модулі)
//  _LightRef   — звичайна яскравість сонця; коли OutdoorDarkening її зменшує, модулі темнішають
// Прозорість (скло, зникаюча оболонка) вмикає матеріал через _SrcBlend/_DstBlend/_ZWrite (робить меню Moon).
Shader "Moon/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.62, 0.78, 1)
        _LightRef ("Normal Sun Brightness", Float) = 1.2
        _OutlineColor ("Outline Color", Color) = (0.1, 0.09, 0.12, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.15)) = 0.035
        _Indoor ("Indoor Light", Range(0, 1)) = 0
        _IndoorColor ("Indoor Light Color", Color) = (1, 0.92, 0.8, 1)
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            float _LightRef;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _Indoor;
            half4 _IndoorColor;
        CBUFFER_END

        // Sun brightness vs normal (1 = normal, less = outdoor darkening)
        half SunBrightness()
        {
            Light sun = GetMainLight();
            half lum = dot(sun.color, half3(0.299h, 0.587h, 0.114h));
            return saturate(lum / max(_LightRef, 0.01));
        }
        ENDHLSL

        // ---------- Main color: 3 light bands ----------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light sun = GetMainLight();
                half3 n = normalize(i.normalWS);
                half ndl = dot(n, sun.direction);

                // 3 bands: lit / half / shadow. Up-facing (roofs) always lit, like painted games.
                half band = ndl > 0.3h ? 1.0h : (ndl > -0.15h ? 0.5h : 0.0h);
                band = max(band, step(0.7h, n.y));

                half3 col = _BaseColor.rgb * lerp(_ShadowColor.rgb, half3(1, 1, 1), band);
                col *= SunBrightness();
                col = lerp(col, _BaseColor.rgb * _IndoorColor.rgb, _Indoor);
                return half4(col, _BaseColor.a);
            }
            ENDHLSL
        }

        // ---------- Outline: slightly bigger inside-out copy ----------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Front

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            struct AttributesO
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsO
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsO vertOutline(AttributesO v)
            {
                VaryingsO o;
                // Push each vertex away from the center per axis by the same width in meters
                // (scale-aware), so box corners stay closed.
                float4x4 m = GetObjectToWorldMatrix();
                float3 scale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
                float3 p = v.positionOS.xyz + sign(v.positionOS.xyz) * (_OutlineWidth / max(scale, 0.0001));
                o.positionCS = TransformObjectToHClip(p);
                return o;
            }

            half4 fragOutline(VaryingsO i) : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a * _BaseColor.a);
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
