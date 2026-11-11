Shader "DemonHunter/Combat/SkinScratch"
{
    Properties
    {
        _SurfaceOffset("Surface Offset", Float) = 0.001
        _StampMasks("Native sword R / blunt G masks", 2D) = "black" {}
        _FractureMap("Native glowing decal fracture", 2D) = "black" {}
        _SlashMap("Native sword decal emission", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_StampMasks); SAMPLER(sampler_StampMasks);
            TEXTURE2D(_FractureMap); SAMPLER(sampler_FractureMap);
            TEXTURE2D(_SlashMap); SAMPLER(sampler_SlashMap);
            float _SurfaceOffset;
            float _ScratchClock;
            float4 _ScratchCenters[8], _ScratchTangents[8], _ScratchBitangents[8];
            float4 _ScratchNormals[8], _ScratchTiming[8], _ScratchGlow[8], _ScratchScar[8];
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 rest : TEXCOORD3;
                float3 restNormal : TEXCOORD4;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 rest : TEXCOORD0;
                float3 restNormal : TEXCOORD1;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz + v.normalOS * _SurfaceOffset);
                o.rest = v.rest;
                o.restNormal = v.restNormal;
                return o;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float3 color = 0;
                float alpha = 0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float age = _ScratchClock - _ScratchTiming[i].x;
                    float life = _ScratchTiming[i].y;
                    if (life <= 0 || age < 0 || age >= life) continue;
                    float3 delta = v.rest - _ScratchCenters[i].xyz;
                    float x = dot(delta, _ScratchTangents[i].xyz) * 2;
                    float y = dot(delta, _ScratchBitangents[i].xyz) * 2;
                    float depth = abs(dot(delta, _ScratchNormals[i].xyz));
                    if (abs(x) >= 1 || abs(y) >= 1 || depth > _ScratchNormals[i].w ||
                        dot(normalize(v.restNormal), _ScratchNormals[i].xyz) < 0.25) continue;
                    // Native TEX masks/decals. Style belongs to this hit slot, so weapon
                    // switching cannot change an older mark already on the target.
                    float2 uv = float2(x, y) * 0.5 + 0.5;
                    float2 nativeMasks = SAMPLE_TEXTURE2D(_StampMasks, sampler_StampMasks, uv).rg;
                    float blunt = saturate(_ScratchCenters[i].w);
                    float mask = lerp(nativeMasks.r, nativeMasks.g, blunt);
                    float cracks = SAMPLE_TEXTURE2D(_FractureMap, sampler_FractureMap, uv).r;
                    float slash = SAMPLE_TEXTURE2D(_SlashMap, sampler_SlashMap, uv.yx).r * (1 - blunt);
                    float edge = 1 - smoothstep(.68, 1, length(float2(x, y)));
                    float depthFade = 1 - smoothstep(_ScratchNormals[i].w * .65, _ScratchNormals[i].w, depth);
                    float normalFade = smoothstep(.25, .6, dot(normalize(v.restNormal), _ScratchNormals[i].xyz));
                    // The radial stamp already contains the impact rays. Multiplying
                    // it only by sword cracks can erase blunt hits on curved armor.
                    float core = saturate(lerp((cracks * 1.6 + slash) * mask,
                        mask * (0.65 + cracks * 1.2), blunt));
                    float aMask = core * edge * depthFade * normalFade;
                    float fade = saturate((life - age) / max(0.001, life * _ScratchTiming[i].w));
                    float glow = 1 - smoothstep(_ScratchTiming[i].z * 0.4, _ScratchTiming[i].z, age);
                    float a = aMask * fade;
                    float3 c = _ScratchScar[i].rgb +
                        _ScratchGlow[i].rgb * (0.35 + core) * glow;
                    color = lerp(color, c, a);
                    alpha = a + alpha * (1 - a);
                }
                clip(alpha - 0.002);
                // Undo premultiplication because the pass uses straight-alpha blending.
                return half4(color / max(alpha, 0.002), alpha);
            }
            ENDHLSL
        }
    }
}
