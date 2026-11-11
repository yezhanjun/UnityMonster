Shader "Jotunnslayer/Shadow Image Presentation"
{
    Properties
    {
        _AlbedoMap ("Original albedo", 2D) = "white" {}
        _EmissionMap ("Original emission", 2D) = "black" {}
        _OpacityMask ("Original opacity", 2D) = "white" {}
        _EmissionMapColor ("Original emission tint", Color) = (1.7840202, 1.3653538, 2.8830185, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_AlbedoMap); SAMPLER(sampler_AlbedoMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_OpacityMask); SAMPLER(sampler_OpacityMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionMapColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_AlbedoMap, sampler_AlbedoMap, input.uv).rgb;
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                half opacity = SAMPLE_TEXTURE2D(_OpacityMask, sampler_OpacityMask, input.uv).r;
                // Initial URP presentation adapter. Original HDRP lighting, rim and dissolve
                // are not reconstructed by this pass and remain separate fidelity work.
                return half4((albedo + emission * _EmissionMapColor.rgb) * opacity, opacity);
            }
            ENDHLSL
        }
    }
}
