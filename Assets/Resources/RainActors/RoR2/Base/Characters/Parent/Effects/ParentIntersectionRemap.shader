// Selected original Cloud Intersection Remap D3D11 BASE program, ported to URP.
// Register mappings and original instructions: ParentIntersectionShader/shader-evidence.json.
Shader "RainActors/ParentIntersectionRemap"
{
    Properties
    {
        _MainTex ("Base mask", 2D) = "gray" {}
        _Cloud1Tex ("Cloud one", 2D) = "gray" {}
        _Cloud2Tex ("Cloud two", 2D) = "gray" {}
        _RemapTex ("Color ramp", 2D) = "gray" {}
        [HDR] _TintColor ("Tint", Color) = (1,1,1,1)
        _CutoffScroll ("Cloud scroll", Vector) = (0,0,0,0)
        _ExternalAlpha ("External mask", Float) = 1
        _AlphaBoost ("Mask multiplier", Float) = 1
        _InvFade ("Intersection depth factor", Float) = 1
        _IntersectionStrength ("Intersection strength", Float) = 0
        _SoftPower ("Intersection exponent", Float) = 1
        _RimPower ("Rim exponent", Float) = 1
        _RimStrength ("Rim strength", Float) = 1
        _Boost ("Brightness", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendFloat ("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendFloat ("Destination blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [HideInInspector] _ValidationTime ("Test phase; negative uses game time", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ParentIntersection"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlendFloat] [_DstBlendFloat]
            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Cloud1Tex); SAMPLER(sampler_Cloud1Tex);
            TEXTURE2D(_Cloud2Tex); SAMPLER(sampler_Cloud2Tex);
            TEXTURE2D(_RemapTex); SAMPLER(sampler_RemapTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Cloud1Tex_ST, _Cloud2Tex_ST;
                float4 _TintColor, _CutoffScroll;
                float _ExternalAlpha, _AlphaBoost, _InvFade, _IntersectionStrength;
                float _SoftPower, _RimPower, _RimStrength, _Boost, _ValidationTime;
            CBUFFER_END
            // The original vertex program reads TEXCOORD1, including all three texture transforms.
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD1; float4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float4 mainCloudUV:TEXCOORD0;
                float2 cloudTwoUV:TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                float3 positionWS:TEXCOORD3;
                float4 screenPosition:TEXCOORD4;
                float4 color:COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.mainCloudUV.xy = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.mainCloudUV.zw = input.uv * _Cloud1Tex_ST.xy + _Cloud1Tex_ST.zw;
                output.cloudTwoUV = input.uv * _Cloud2Tex_ST.xy + _Cloud2Tex_ST.zw;
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                return output;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float phase = _ValidationTime < 0 ? _Time.x : _ValidationTime;
                float mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.mainCloudUV.xy).a * _TintColor.a * input.color.a;
                mask *= SAMPLE_TEXTURE2D(_Cloud1Tex, sampler_Cloud1Tex, input.mainCloudUV.zw + phase * _CutoffScroll.xy).a;
                mask *= SAMPLE_TEXTURE2D(_Cloud2Tex, sampler_Cloud2Tex, input.cloudTwoUV + phase * _CutoffScroll.zw).a;
                mask *= 4 * _AlphaBoost * _ExternalAlpha;
                // Depth is unconditional with respect to the global soft-particle keyword in this program.
                float depthTerm = 1;
                if (_InvFade > 0)
                {
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(input.screenPosition.xy / input.screenPosition.w), _ZBufferParams);
                    depthTerm = pow(saturate((sceneDepth - input.screenPosition.w) * _InvFade), _SoftPower);
                }
                // Original normal is normalized per vertex, then interpolated without renormalization.
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = pow(1 - saturate(abs(dot(viewDirection, input.normalWS))), _RimPower);
                float coordinate = saturate((1 + rim * _RimStrength - depthTerm) * mask * _IntersectionStrength);
                float4 result = SAMPLE_TEXTURE2D(_RemapTex, sampler_RemapTex, float2(coordinate, .5)) * _TintColor;
                result.rgb *= _Boost;
                return result;
            }
            ENDHLSL
        }
    }
}
