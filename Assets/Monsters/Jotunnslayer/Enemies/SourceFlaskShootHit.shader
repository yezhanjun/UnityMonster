Shader "Jotunnslayer/SourceFlaskShootHit"
{
    Properties
    {
        Texture2D_F593E37E ("Packed particle", 2D) = "white" {}
        Boolean_2FAC1FE4 ("Soft particles", Float) = 1
        _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(Texture2D_F593E37E); SAMPLER(sampler_Texture2D_F593E37E);
            CBUFFER_START(UnityPerMaterial)
                float Boolean_2FAC1FE4;
                float _Cull;
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
                // Source shoot-hit transparent blob17, including additive surface weighting.
                float blue = SAMPLE_TEXTURE2D(Texture2D_F593E37E, sampler_Texture2D_F593E37E, input.uv).b;
                float2 uv = input.uv + float2(blue * input.uv.x * input.controls.z, 0);
                float4 packed = SAMPLE_TEXTURE2D(Texture2D_F593E37E, sampler_Texture2D_F593E37E, uv);
                float3 rgb = packed.r * input.color.rgb;
                float q = clamp(input.controls.y, 0, .5);
                // Preserve source division, including its singular boundary at q=.5.
                precise float lower = q - 1;
                precise float upper = 1 - q;
                precise float span = upper - lower;
                precise float threshold = mad(input.controls.x, span, lower);
                precise float value = threshold + (1 - packed.g);
                precise float numerator = value - q;
                precise float inverseWidth = 1 / (upper - q);
                float t = saturate(numerator * inverseWidth);
                float alpha = saturate(packed.a - t * t * (3 - 2 * t));
                if (Boolean_2FAC1FE4 != 0)
                {
                    float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    if (unity_OrthoParams.w > .5)
                    {
                        #if UNITY_REVERSED_Z
                        rawDepth = 1 - rawDepth;
                        #endif
                        sceneDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                    }
                    alpha *= saturate((sceneDepth - input.depthFog.x) * input.warp.x);
                }
                rgb *= alpha * input.controls.w * input.color.a;
                // URP fog replaces HDRP atmospheric fog; source exposure is not reproduced here.
                rgb = MixFogColor(rgb, float3(0, 0, 0), input.depthFog.y);
                return float4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
