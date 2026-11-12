Shader "Jotunnslayer/SourceSmokeErosion"
{
    Properties
    {
        _NoiseDistortionTexture ("_NoiseDistortionTexture", 2D) = "white" {}
        _Noise_01_Texture ("_Noise_01_Texture", 2D) = "white" {}
        _Noise_02_Texture ("_Noise_02_Texture", 2D) = "white" {}
        _MaskTexture ("_MaskTexture", 2D) = "white" {}
        _MaskMoveTexture ("_MaskMoveTexture", 2D) = "white" {}
        _Color ("_Color", Color) = (1.0,1.0,1.0,0.0)
        _MaskMoveScale ("_MaskMoveScale", Vector) = (1.0,1.0,0.0,0.0)
        _NoiseDistortionSpeed ("_NoiseDistortionSpeed", Vector) = (0.0,0.0,0.0,0.0)
        _NoiseDistortionScale ("_NoiseDistortionScale", Vector) = (1.0,1.0,0.0,0.0)
        _MaskScale ("_MaskScale", Vector) = (1.0,1.0,0.0,0.0)
        _MaskOffset ("_MaskOffset", Vector) = (0.0,0.0,0.0,0.0)
        _Noise02Scale ("_Noise02Scale", Vector) = (1.0,1.0,0.0,0.0)
        _Noise02Speed ("_Noise02Speed", Vector) = (0.0,0.0,0.0,0.0)
        _Noise01Scale ("_Noise01Scale", Vector) = (2.0,2.0,0.0,0.0)
        _Noise01Speed ("_Noise01Speed", Vector) = (0.5,0.5,0.0,0.0)
        _EmissionIntensity ("_EmissionIntensity", Float) = 0.0
        _NoisesMultiply ("_NoisesMultiply", Float) = 2.0
        _NoisesPower ("_NoisesPower", Float) = 1.0
        _MaskMovePower ("_MaskMovePower", Float) = 1.0
        _MaskMultiply ("_MaskMultiply", Float) = 1.0
        _MaskPower ("_MaskPower", Float) = 1.0
        _MaskSpeed ("_MaskSpeed", Float) = 0.0
        _DistortionIntensity ("_DistortionIntensity", Float) = 0.0
        _Distortion ("_Distortion", Float) = 0.0
        _WindSpeed ("_WindSpeed", Float) = 0.4000000059604645
        _MaskMoveMultiply ("_MaskMoveMultiply", Float) = 1.0
        _OpacityBoost ("_OpacityBoost", Float) = 1.0
        _Dissolve ("_Dissolve", Float) = 0.0
        _DepthFade ("_DepthFade", Float) = 1.0
        _DstBlend ("Destination blend", Float) = 10
        _BlendMode ("Source blend mode", Float) = 0
        _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_NoiseDistortionTexture); SAMPLER(sampler_NoiseDistortionTexture);
            TEXTURE2D(_Noise_01_Texture); SAMPLER(sampler_Noise_01_Texture);
            TEXTURE2D(_Noise_02_Texture); SAMPLER(sampler_Noise_02_Texture);
            TEXTURE2D(_MaskTexture); SAMPLER(sampler_MaskTexture);
            TEXTURE2D(_MaskMoveTexture); SAMPLER(sampler_MaskMoveTexture);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MaskMoveScale;
                float4 _NoiseDistortionSpeed;
                float4 _NoiseDistortionScale;
                float4 _MaskScale;
                float4 _MaskOffset;
                float4 _Noise02Scale;
                float4 _Noise02Speed;
                float4 _Noise01Scale;
                float4 _Noise01Speed;
                float _EmissionIntensity;
                float _NoisesMultiply;
                float _NoisesPower;
                float _MaskMovePower;
                float _MaskMultiply;
                float _MaskPower;
                float _MaskSpeed;
                float _DistortionIntensity;
                float _Distortion;
                float _WindSpeed;
                float _MaskMoveMultiply;
                float _OpacityBoost;
                float _Dissolve;
                float _DepthFade;
                float _Cull;
                float _DstBlend;
                float _BlendMode;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 controls : TEXCOORD1;
                float4 warp : TEXCOORD2;
                float2 depthFog : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.color = input.color;
                output.uv = input.uv0.xy;
                output.controls = input.uv1;
                output.warp = input.uv2;
                output.depthFog = float2(-position.positionVS.z, ComputeFogFactor(position.positionCS.z));
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                // Erosion transparent blob10: source surface, erosion and depth weighting.
                float time = _Time.y * _WindSpeed;
                float distortion = SAMPLE_TEXTURE2D(_NoiseDistortionTexture, sampler_NoiseDistortionTexture,
                    input.uv * _NoiseDistortionScale.xy + time * _NoiseDistortionSpeed.xy).r * _Distortion;
                float2 noise1UV = input.uv * _Noise01Scale.xy + time * _Noise01Speed.xy + distortion * .1;
                float2 noise2UV = input.uv * _Noise02Scale.xy + time * _Noise02Speed.xy + distortion * .1;
                float noise1 = SAMPLE_TEXTURE2D(_Noise_01_Texture, sampler_Noise_01_Texture, noise1UV).r;
                float noise2 = SAMPLE_TEXTURE2D(_Noise_02_Texture, sampler_Noise_02_Texture, noise2UV).r;
                float noises = saturate(pow(noise1 * noise2, _NoisesPower) * _NoisesMultiply);
                float2 maskUV = input.uv * _MaskScale.xy + _MaskOffset.xy + float2(time * _MaskSpeed, 0)
                    + distortion * .1 * _DistortionIntensity;
                float mask = SAMPLE_TEXTURE2D(_MaskTexture, sampler_MaskTexture, maskUV).r;
                mask = saturate(pow(mask, _MaskPower) * _MaskMultiply);
                float moveMask = SAMPLE_TEXTURE2D(_MaskMoveTexture, sampler_MaskMoveTexture,
                    input.uv * _MaskMoveScale.xy + input.controls.zw).r;
                moveMask = saturate(pow(moveMask, _MaskMovePower) * _MaskMoveMultiply);
                float density = noises * mask * moveMask;
                float3 rgb = density * input.color.rgb * _Color.rgb;
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                if (unity_OrthoParams.w > .5)
                {
                    #if UNITY_REVERSED_Z
                    rawDepth = 1 - rawDepth;
                    #endif
                    sceneDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                }
                float fade = min(abs((sceneDepth - input.depthFog.x) / _DepthFade), 1);
                float alpha = saturate((saturate(density * _OpacityBoost) - (input.controls.x + _Dissolve)) * fade);
                alpha = saturate(alpha * input.color.a);
                // Source emission uses HDRP exposure texture. Unit exposure here; source material has emission=0.
                rgb += rgb * _EmissionIntensity;
                // URP fog replaces HDRP atmospheric fog; source exposure is not reproduced here.
                rgb = _BlendMode==1 ? MixFogColor(rgb, float3(0,0,0), input.depthFog.y) : MixFog(rgb, input.depthFog.y);
                return float4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
