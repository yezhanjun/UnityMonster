Shader "Jotunnslayer/SourceFlaskDissolve"
{
    Properties
    {
        Texture2D_F593E37E ("Packed particle", 2D) = "white" {}
        _MainTex_UV ("UV warp", 2D) = "gray" {}
        Boolean_2FAC1FE4 ("Soft particles", Float) = 1
        _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
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
            TEXTURE2D(_MainTex_UV); SAMPLER(sampler_MainTex_UV);
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
                // Source transparent ForwardOnly blob20; see DraugFlaskShaderEvidence.json.
                float2 uv = input.uv * float2(input.warp.y, 1) + float2(frac(input.warp.z), 0);
                float3 distortion = SAMPLE_TEXTURE2D(_MainTex_UV, sampler_MainTex_UV, uv).rgb;
                uv += (distortion.rg - .5) * input.warp.x * distortion.b;
                float4 a = SAMPLE_TEXTURE2D(Texture2D_F593E37E, sampler_Texture2D_F593E37E, uv + input.warp.w * float2(.0087, -.005));
                float4 b = SAMPLE_TEXTURE2D(Texture2D_F593E37E, sampler_Texture2D_F593E37E, uv + input.warp.w * float2(0, .01));
                float4 c = SAMPLE_TEXTURE2D(Texture2D_F593E37E, sampler_Texture2D_F593E37E, uv + input.warp.w * float2(-.0087, -.005));
                float3 rgb = saturate(float3(a.r, b.r, c.r) * input.color.rgb) * input.controls.z;
                float q = saturate(input.controls.y);
                float t = saturate((saturate(1 - max(a.g, max(b.g, c.g))) + input.controls.x * (2 - q) - 1) / (1.01 - q));
                float alpha = saturate(max(a.a, max(b.a, c.a)) - t * t * (3 - 2 * t));
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
                    // Zero-width fade becomes a hard intersection boundary; avoid NaN at contact.
                    alpha *= input.controls.w > 0 ? saturate((sceneDepth - input.depthFog.x) / input.controls.w) : step(input.depthFog.x, sceneDepth);
                }
                alpha *= input.color.a;
                // URP fog replaces HDRP atmospheric fog; source exposure is not reproduced here.
                rgb = MixFog(rgb, input.depthFog.y);
                return float4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
