Shader "Hidden/RainActors/ParentDecalInstructionValidation"
{
    Properties
    {
        _Cloud1Tex ("Cloud one", 2D) = "white" {}
        _Cloud2Tex ("Cloud two", 2D) = "white" {}
        _RemapTex ("Remap", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Resources/RainActors/RoR2/Base/Characters/Parent/Effects/ParentDecalInstructionMath.hlsl"
            TEXTURE2D(_Cloud1Tex); SAMPLER(sampler_Cloud1Tex);
            TEXTURE2D(_Cloud2Tex); SAMPLER(sampler_Cloud2Tex);
            TEXTURE2D(_RemapTex); SAMPLER(sampler_RemapTex);
            float4 _Cloud1Tex_ST, _Cloud2Tex_ST, _Scroll, _Tint, _Ambient;
            float4 _Receiver, _Right, _Up, _PackedNormal, _ReceiverSpec;
            float4 _Gate, _Settings, _NormalSettings, _SpecSettings, _RayDepth, _DepthParams;
            float4x4 _CameraToWorldTest, _WorldToDecalTest;
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.position=TransformObjectToHClip(input.positionOS); o.uv=input.uv; return o;
            }
            struct Payload { float4 albedo:SV_Target0; float4 emission:SV_Target1; float4 spec:SV_Target2; float4 normal:SV_Target3; };
            Payload Frag(Varyings i)
            {
                float3 local=RainParentDecalReconstruct(_RayDepth.xyz,_DepthParams.z,_RayDepth.w,
                    _DepthParams.xy,_CameraToWorldTest,_WorldToDecalTest);
                // Uniform fixtures independently exercise all original discard boundaries.
                if (!RainParentDecalCommonAccept(local,_Gate.x,_Gate.y,_Gate.z,_Gate.w,_Receiver.w,_Settings.w)) discard;
                float2 uv=local.xz+.5;
                uv += (i.uv-.5)*.13;
                float a=SAMPLE_TEXTURE2D(_Cloud1Tex,sampler_Cloud1Tex,RainParentDecalCloudUV(uv,_Cloud1Tex_ST,_Scroll.xy,_Settings.z)).a;
                float b=SAMPLE_TEXTURE2D(_Cloud2Tex,sampler_Cloud2Tex,RainParentDecalCloudUV(uv,_Cloud2Tex_ST,_Scroll.zw,_Settings.z)).a;
                float rampX=RainParentDecalRemapCoordinate(_Receiver.xyz,_Up.xyz,_Settings.x,_Gate.z,_Gate.w,a,b,_Settings.y);
                float4 ramp=SAMPLE_TEXTURE2D(_RemapTex,sampler_RemapTex,float2(rampX,.5));
                Payload o;
                RainParentDecalColorPayload(ramp,_Tint,_Ambient.xyz,_NormalSettings.w!=0,o.albedo,o.emission);
                float weight=_Gate.z*_Gate.w;
                o.spec=RainParentDecalSpecularPayload(_ReceiverSpec,weight,_SpecSettings.x,_SpecSettings.y,(int)_SpecSettings.z,_SpecSettings.w);
                float nw=_NormalSettings.z!=0 ? weight : _Gate.w;
                o.normal=float4(RainParentDecalNormalPayload(_Receiver.xyz,_PackedNormal,_NormalSettings.x,_Right.xyz,_Up.xyz,_NormalSettings.y!=0,nw),1);
                // Original normal/specular pass discards negative angles, unlike color pass.
                if(!RainParentDecalNormalAccept(_Receiver.xyz,_Up.xyz,_Settings.x)) { o.spec=float4(-7,-7,-7,-7); o.normal=float4(-7,-7,-7,-7); }
                return o;
            }
            ENDHLSL
        }
    }
}
