Shader "RainActors/LunarShardTrail"
{
    Properties
    {
        _Lightning ("Lightning mask", 2D) = "white" {}
        _Cloud ("Cloud mask", 2D) = "white" {}
        _Ramp ("Color ramp", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Lightning);
            SAMPLER(sampler_Lightning);
            TEXTURE2D(_Cloud);
            SAMPLER(sampler_Cloud);
            TEXTURE2D(_Ramp);
            SAMPLER(sampler_Ramp);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 lightningUV = float2(frac(-16.0 * _Time.y), input.uv.y);
                float2 cloudUV = float2(frac(input.uv.x * 0.2 - 100.0 * _Time.y), input.uv.y);
                half lightning = SAMPLE_TEXTURE2D(_Lightning, sampler_Lightning, lightningUV).a;
                half cloud = SAMPLE_TEXTURE2D(_Cloud, sampler_Cloud, cloudUV).r;
                half mask = lightning * cloud;
                half4 ramp = SAMPLE_TEXTURE2D(_Ramp, sampler_Ramp, float2(mask, 0.5));
                return half4(ramp.rgb * input.color.rgb, saturate(mask * 6.11h) * ramp.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
