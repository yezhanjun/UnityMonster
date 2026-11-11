// D3D11 BASE/SOFTPARTICLES_ON ports; see ParentDistortionShader/shader-evidence.json.
Shader "RainActors/ParentDistortion"
{
    Properties
    {
        _BumpMap ("Bump texture", 2D) = "bump" {}
        _MaskTex ("Mask texture", 2D) = "white" {}
        _Magnitude ("Magnitude", Float) = .05
        _NearFadeZeroDistance ("Near zero", Float) = 0
        _NearFadeOneDistance ("Near one", Float) = 5
        _FarFadeZeroDistance ("Far zero", Float) = 250000
        _FarFadeOneDistance ("Far one", Float) = 200000
        _InvFade ("Depth fade", Float) = .1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2000" "RenderType"="Transparent" }
        Pass
        {
            Name "ParentDistortion"
            // The feature captures color after ordinary transparents, then draws this pass.
            Tags { "LightMode"="RainParentDistortion" }
            Blend One Zero
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ SOFTPARTICLES_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            TEXTURE2D_X(_GrabTexturePostProcess); SAMPLER(sampler_GrabTexturePostProcess);
            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST, _MaskTex_ST;
                float _Magnitude, _NearFadeZeroDistance, _NearFadeOneDistance;
                float _FarFadeZeroDistance, _FarFadeOneDistance, _InvFade;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float4 uv:TEXCOORD0;
                float4 grab:TEXCOORD1;
                float4 screen:TEXCOORD2;
                float alpha:COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                // Original offsets are applied before scale, unlike TRANSFORM_TEX.
                output.uv.xy = (input.uv + _BumpMap_ST.zw) * _BumpMap_ST.xy;
                output.uv.zw = (input.uv + _MaskTex_ST.zw) * _MaskTex_ST.xy;
                output.grab = output.positionCS;
                output.grab.xy = (output.positionCS.xy * float2(.5,-.5)) + output.positionCS.w * .5;
                output.screen = ComputeScreenPos(output.positionCS);
                output.alpha = input.color.a;
                return output;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float nearFade = (input.screen.w - _NearFadeZeroDistance) / (_NearFadeOneDistance - _NearFadeZeroDistance);
                float farFade = (input.screen.w - _FarFadeZeroDistance) / (_FarFadeOneDistance - _FarFadeZeroDistance);
                float strength = saturate(min(nearFade,farFade)) * input.alpha * SAMPLE_TEXTURE2D(_MaskTex,sampler_MaskTex,input.uv.zw).a;
                #if defined(SOFTPARTICLES_ON)
                    if (_InvFade > 0)
                    {
                        float sceneDepth = LinearEyeDepth(SampleSceneDepth(input.grab.xy / input.grab.w),_ZBufferParams);
                        strength *= saturate((sceneDepth-input.screen.w)*_InvFade);
                    }
                #endif
                clip(strength-.01);
                float4 packedNormal = SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv.xy);
                float2 normal = float2(packedNormal.r*packedNormal.a,packedNormal.g)*2-1;
                float normalZ = sqrt(1-min(dot(normal,normal),1));
                clip(.99-normalZ);
                float2 uv = (input.grab.xy+normal*(_Magnitude*strength))/input.grab.w;
                return SAMPLE_TEXTURE2D_X(_GrabTexturePostProcess,sampler_GrabTexturePostProcess,uv);
            }
            ENDHLSL
        }
    }
}
