// Consumed Antler Cloud Remap variants, including original DISABLEREMAP.
// Evidence: Docs/FrontEnd/AntlersConsumedCloudShader/shader-evidence.json.
// Shared ParentCloudRemap is unchanged.
Shader "RainItems/AntlersConsumedCloudRemap"
{
    Properties
    {
        _MainTex ("Main mask", 2D) = "gray" {}
        _Cloud1Tex ("Cloud one", 2D) = "gray" {}
        _Cloud2Tex ("Cloud two", 2D) = "gray" {}
        _RemapTex ("Color ramp", 2D) = "gray" {}
        [HDR] _TintColor ("Tint", Color) = (1,1,1,1)
        _CutoffScroll ("Cloud scroll", Vector) = (0,0,0,0)
        _Boost ("Brightness", Float) = 1
        _AlphaBoost ("Remap multiplier", Float) = 1
        _AlphaBias ("Remap bias", Float) = 0
        _ExternalAlpha ("External remap multiplier", Float) = 1
        _Fade ("Fade remap multiplier", Float) = 1
        _DistortionStrength ("Cloud UV offset", Float) = .1
        _InvFade ("Depth fade", Float) = .1
        _DepthOffset ("Depth offset", Float) = 0
        _FadeCloseDistance ("Near fade multiplier", Float) = .5
        [Toggle(USE_CLOUDS)] _CloudsOn ("Use clouds", Float) = 0
        [Toggle(CLOUDOFFSET)] _CloudOffsetOn ("Cloud offset", Float) = 0
        [Toggle(VERTEXCOLOR)] _VertexColorOn ("Vertex RGB", Float) = 0
        [Toggle(FADECLOSE)] _FadeCloseOn ("Near fade", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        [HideInInspector] _ValidationTime ("Deterministic test time; negative uses game time", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "AntlersConsumedCloud"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZTest [_ZTest]
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ USE_CLOUDS
            #pragma shader_feature_local _ CLOUDOFFSET
            #pragma shader_feature_local _ VERTEXCOLOR
            #pragma shader_feature_local _ DISABLEREMAP
            #pragma shader_feature_local _ FADECLOSE
            #pragma multi_compile _ SOFTPARTICLES_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Cloud1Tex); SAMPLER(sampler_Cloud1Tex);
            TEXTURE2D(_Cloud2Tex); SAMPLER(sampler_Cloud2Tex);
            TEXTURE2D(_RemapTex); SAMPLER(sampler_RemapTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Cloud1Tex_ST, _Cloud2Tex_ST;
                float4 _TintColor, _CutoffScroll;
                float _Boost, _AlphaBoost, _AlphaBias, _ExternalAlpha, _Fade;
                float _DistortionStrength, _InvFade, _DepthOffset, _FadeCloseDistance, _ValidationTime;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float4 mainCloudUV:TEXCOORD0;
                float2 cloudTwoUV:TEXCOORD1;
                float4 screenPosition:TEXCOORD2;
                float4 color:COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.mainCloudUV.xy = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.mainCloudUV.zw = input.uv * _Cloud1Tex_ST.xy + _Cloud1Tex_ST.zw;
                output.cloudTwoUV = input.uv * _Cloud2Tex_ST.xy + _Cloud2Tex_ST.zw;
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                return output;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float4 mainValue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.mainCloudUV.xy);
                float mask = mainValue.a * _TintColor.a * input.color.a;
                #if defined(USE_CLOUDS)
                    float phase = _ValidationTime < 0 ? _Time.x : _ValidationTime;
                    #if !defined(VERTEXCOLOR)
                        phase += input.color.r + input.color.g + input.color.b;
                    #endif
                    float cloudOne = SAMPLE_TEXTURE2D(_Cloud1Tex, sampler_Cloud1Tex,
                        input.mainCloudUV.zw + phase * _CutoffScroll.xy).a;
                    float cloudTwo = SAMPLE_TEXTURE2D(_Cloud2Tex, sampler_Cloud2Tex,
                        input.cloudTwoUV + phase * _CutoffScroll.zw).a;
                    #if defined(CLOUDOFFSET)
                        float2 shiftedUV = input.mainCloudUV.xy + float2(0, _DistortionStrength * (4 * cloudOne * cloudTwo - 1));
                        mask *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shiftedUV).a * _TintColor.a * input.color.a;
                    #else
                        mask *= 4 * cloudOne * cloudTwo;
                    #endif
                #endif
                #if defined(DISABLEREMAP)
                    // Original DISABLEREMAP always multiplies vertex RGBA, and ignores
                    // ExternalAlpha/Fade, depth fade and the remap texture even in soft variants.
                    float4 raw = mainValue * _TintColor * input.color;
                    raw.rgb *= _Boost;
                    raw.a = mask * _AlphaBoost + _AlphaBias;
                    return raw;
                #endif
                float coordinate = (mask * _AlphaBoost + _AlphaBias) * _ExternalAlpha * _Fade;
                #if defined(SOFTPARTICLES_ON)
                    // Original variants apply near fade only when soft particles are active and InvFade > 0.
                    if (_InvFade > 0)
                    {
                        float eyeDepth = input.screenPosition.w + _DepthOffset;
                        float sceneDepth = LinearEyeDepth(SampleSceneDepth(input.screenPosition.xy / input.screenPosition.w), _ZBufferParams);
                        coordinate *= saturate((sceneDepth - eyeDepth) * _InvFade);
                        #if defined(FADECLOSE)
                            coordinate *= saturate((eyeDepth - .5) * _FadeCloseDistance);
                        #endif
                    }
                #endif
                float4 result = SAMPLE_TEXTURE2D(_RemapTex, sampler_RemapTex, float2(coordinate, .5)) * _TintColor;
                #if defined(VERTEXCOLOR)
                    result.rgb *= input.color.rgb;
                #endif
                result.rgb *= _Boost;
                return result;
            }
            ENDHLSL
        }
    }
}
