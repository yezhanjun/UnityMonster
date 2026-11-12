Shader "AAAArpg/Whitebox/ProBuilder Default Triplanar"
{
    Properties
    {
        [MainTexture] _BaseMap("ProBuilder Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _DetailMap("Detail / Text Map", 2D) = "black" {}
        _MaskMap("Tile Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _EmissionMap("Emission Map", 2D) = "black" {}
        _WorldScale("Texture Size (meters)", Float) = 4
        _BlendSharpness("Projection Blend Sharpness", Range(1, 16)) = 8
        _DetailOpacity("Detail Opacity", Range(0, 1)) = 0
        _MaskStrength("Mask Strength", Range(0, 1)) = 1
        _NormalStrength("Normal Strength", Range(0, 2)) = 0
        _EmissionStrength("Emission Strength", Range(0, 8)) = 0
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WorldScale;
                half _BlendSharpness;
                half _DetailOpacity;
                half _MaskStrength;
                half _NormalStrength;
                half _EmissionStrength;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half4 fogFactorAndVertexLight : TEXCOORD2;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD3;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                half fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                half3 vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = input.color;
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(positionInputs);
                #endif
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 SampleWorldTriplanar(float3 positionWS, half3 normalWS)
            {
                float3 worldPosition = positionWS / max(_WorldScale, 0.001);
                half3 weights = pow(abs(normalWS), _BlendSharpness);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);

                half4 xProjection = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPosition.zy);
                half4 yProjection = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPosition.xz);
                half4 zProjection = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPosition.xy);
                return xProjection * weights.x + yProjection * weights.y + zProjection * weights.z;
            }

            half4 SampleWorldTriplanarDetail(float3 positionWS, half3 normalWS)
            {
                float3 worldPosition = positionWS / max(_WorldScale, 0.001);
                half3 weights = pow(abs(normalWS), _BlendSharpness);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);
                half4 xProjection = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, worldPosition.zy);
                half4 yProjection = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, worldPosition.xz);
                half4 zProjection = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, worldPosition.xy);
                return xProjection * weights.x + yProjection * weights.y + zProjection * weights.z;
            }

            half3 SampleWorldTriplanarEmission(float3 positionWS, half3 normalWS)
            {
                float3 worldPosition = positionWS / max(_WorldScale, 0.001);
                half3 weights = pow(abs(normalWS), _BlendSharpness);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);
                half3 xProjection = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, worldPosition.zy).rgb;
                half3 yProjection = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, worldPosition.xz).rgb;
                half3 zProjection = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, worldPosition.xy).rgb;
                return xProjection * weights.x + yProjection * weights.y + zProjection * weights.z;
            }

            half SampleWorldTriplanarMask(float3 positionWS, half3 normalWS)
            {
                float3 worldPosition = positionWS / max(_WorldScale, 0.001);
                half3 weights = pow(abs(normalWS), _BlendSharpness);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);
                half xProjection = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, worldPosition.zy).r;
                half yProjection = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, worldPosition.xz).r;
                half zProjection = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, worldPosition.xy).r;
                return xProjection * weights.x + yProjection * weights.y + zProjection * weights.z;
            }

            half3 SampleWorldTriplanarNormal(float3 positionWS, half3 normalWS)
            {
                float3 worldPosition = positionWS / max(_WorldScale, 0.001);
                half3 weights = pow(abs(normalWS), _BlendSharpness);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);
                half3 xNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, worldPosition.zy));
                half3 yNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, worldPosition.xz));
                half3 zNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, worldPosition.xy));
                half3 perturbation = (xNormal - half3(0, 0, 1)) * weights.x
                    + (yNormal - half3(0, 0, 1)) * weights.y
                    + (zNormal - half3(0, 0, 1)) * weights.z;
                return normalize(normalWS + perturbation * _NormalStrength);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half4 sampled = SampleWorldTriplanar(input.positionWS, normalWS);
                half4 detail = SampleWorldTriplanarDetail(input.positionWS, normalWS);
                half mask = SampleWorldTriplanarMask(input.positionWS, normalWS);
                half3 albedo = lerp(sampled.rgb, detail.rgb, saturate(mask * _MaskStrength * _DetailOpacity));
                half3 mappedNormal = SampleWorldTriplanarNormal(input.positionWS, normalWS);
                half3 emission = SampleWorldTriplanarEmission(input.positionWS, normalWS) * _EmissionStrength;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo * _BaseColor.rgb * input.color.rgb;
                surfaceData.alpha = 1.0h;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = emission;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = mappedNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = input.fogFactorAndVertexLight.x;
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, mappedNormal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    half3 packedNormalWS = PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5));
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
