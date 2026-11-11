Shader "Hidden/RainActors/ParentOpaqueInstructionValidation"
{
    Properties
    {
        _MainTex ("Main", 2D) = "gray" {}
        _Cloud1Tex ("Cloud one", 2D) = "gray" {}
        _Cloud2Tex ("Cloud two", 2D) = "gray" {}
        _RemapTex ("Remap", 2D) = "gray" {}
        _NormalTex ("Normal", 2D) = "bump" {}
        _WarpRamp ("Original global ramp", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _CutoffScroll ("Scroll", Vector) = (0,0,0,0)
        _AlphaBoost ("Alpha boost", Float) = 1
        _ExternalAlpha ("External alpha", Float) = 1
        _VertexColorInfluence ("Vertex influence", Float) = 1
        _Cutoff ("Cutoff", Float) = .5
        _NormalStrength ("Normal strength", Float) = 1
        _SpecularStrength ("Specular strength", Float) = 0
        _SpecularExponent ("Specular exponent", Float) = 1
        _RampInfo ("Ramp", Int) = 0
        _ValidationTime ("Phase", Float) = .37
        _ValidationHDR ("HDR", Float) = 1
        _ValidationLocalLight ("Point/spot diffuse coordinate", Float) = 0
        _ValidationLightDirection ("Direction to light", Vector) = (0,0,1,0)
        [HDR] _ValidationLightColor ("Light color", Color) = (1,1,1,1)
        _ValidationViewDirection ("Direction to camera", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/Resources/RainActors/RoR2/Base/Characters/Parent/Effects/ParentOpaqueInstructionMath.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_Cloud1Tex); SAMPLER(sampler_Cloud1Tex);
        TEXTURE2D(_Cloud2Tex); SAMPLER(sampler_Cloud2Tex);
        TEXTURE2D(_RemapTex); SAMPLER(sampler_RemapTex);
        TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
        TEXTURE2D(_WarpRamp); SAMPLER(sampler_WarpRamp);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _Cloud1Tex_ST, _Cloud2Tex_ST;
            float4 _TintColor, _EmissionColor, _CutoffScroll;
            float4 _ValidationLightDirection, _ValidationLightColor, _ValidationViewDirection;
            float _AlphaBoost, _ExternalAlpha, _VertexColorInfluence, _Cutoff, _NormalStrength;
            float _SpecularStrength, _SpecularExponent, _ValidationTime, _ValidationHDR, _ValidationLocalLight;
            int _RampInfo;
        CBUFFER_END
        struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD1; float4 color:COLOR; };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float4 mainCloudUV:TEXCOORD0;
            float2 cloudTwoUV:TEXCOORD1;
            float3 normalWS:TEXCOORD2;
            float3 tangentWS:TEXCOORD3;
            float3 bitangentWS:TEXCOORD4;
            float4 color:COLOR;
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.mainCloudUV.xy = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
            output.mainCloudUV.zw = input.uv * _Cloud1Tex_ST.xy + _Cloud1Tex_ST.zw;
            output.cloudTwoUV = input.uv * _Cloud2Tex_ST.xy + _Cloud2Tex_ST.zw;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
            output.bitangentWS = cross(output.normalWS,output.tangentWS) * (input.tangentOS.w * GetOddNegativeScale());
            output.color = input.color;
            return output;
        }
        RainParentOpaquePayload Surface(Varyings input)
        {
            float mainAlpha = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.mainCloudUV.xy).a;
            float one = SAMPLE_TEXTURE2D(_Cloud1Tex,sampler_Cloud1Tex,input.mainCloudUV.zw + _ValidationTime * _CutoffScroll.xy).a;
            float two = SAMPLE_TEXTURE2D(_Cloud2Tex,sampler_Cloud2Tex,input.cloudTwoUV + _ValidationTime * _CutoffScroll.zw).a;
            float4 color = RainParentOpaqueVertexColor(input.color,_VertexColorInfluence);
            float mask = RainParentOpaqueMask(mainAlpha,one,two,color,_TintColor.a,_AlphaBoost,_ExternalAlpha);
            clip(mask-_Cutoff);
            float3 remap = SAMPLE_TEXTURE2D(_RemapTex,sampler_RemapTex,float2(mask,.5)).rgb;
            // Original normal uses Main UV; there is no independent NormalTex ST.
            float3 normalTS = RainParentOpaqueNormalTS(SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,input.mainCloudUV.xy),_NormalStrength);
            float3 normalWS = normalize(input.tangentWS * normalTS.x + input.bitangentWS * normalTS.y + input.normalWS * normalTS.z);
            return RainParentOpaquePack(remap,_TintColor,color,normalWS,_SpecularStrength,_SpecularExponent,_RampInfo,_EmissionColor.rgb,_ValidationHDR>0);
        }
        struct Mrt
        {
            float4 albedo:SV_Target0;
            float4 specularRamp:SV_Target1;
            float4 normal:SV_Target2;
            float4 emission:SV_Target3;
        };
        Mrt PayloadFrag(Varyings input)
        {
            RainParentOpaquePayload payload=Surface(input);
            Mrt output;
            output.albedo=payload.albedo; output.specularRamp=payload.specularRamp;
            output.normal=payload.normal; output.emission=payload.emission;
            return output;
        }
        float4 LightFrag(Varyings input):SV_Target
        {
            RainParentOpaquePayload payload=Surface(input);
            float3 coordinates=RainParentOpaqueLightCoordinates(normalize(payload.normal.xyz*2-1),normalize(_ValidationViewDirection.xyz),
                normalize(_ValidationLightDirection.xyz),payload.specularRamp.y,payload.specularRamp.z,_ValidationLocalLight>0);
            float3 diffuse=SAMPLE_TEXTURE2D(_WarpRamp,sampler_WarpRamp,coordinates.yz).rgb;
            float specular=SAMPLE_TEXTURE2D(_WarpRamp,sampler_WarpRamp,coordinates.xz).r;
            return float4(RainParentOpaqueDirectColor(payload.albedo.rgb,_ValidationLightColor.rgb,payload.specularRamp.x,diffuse,specular),1);
        }
        ENDHLSL
        Pass
        {
            Name "OriginalPayload"
            Cull Off ZWrite Off ZTest Always Blend One Zero
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma fragment PayloadFrag
            ENDHLSL
        }
        Pass
        {
            Name "OriginalLight"
            Cull Off ZWrite Off ZTest Always Blend One Zero
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma fragment LightFrag
            ENDHLSL
        }
    }
}
