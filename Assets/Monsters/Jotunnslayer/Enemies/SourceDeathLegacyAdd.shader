Shader "Jotunnslayer/SourceDeathLegacyAdd"
{
 Properties
 {
  _MainTex("Source texture",2D)="white"{}
  _TintColor("Source tint",Color)=(1,1,1,1)
  _InvFade("Source soft depth coefficient",Float)=1
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha One
   ColorMask RGB
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ SOFTPARTICLES_ON
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _TintColor;
    float _InvFade;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct Varyings{float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;float eyeDepth:TEXCOORD2;};
   Varyings Vert(Attributes i)
   {
    Varyings o;float3 world=TransformObjectToWorld(i.positionOS.xyz);
    o.positionCS=TransformWorldToHClip(world);o.screen=ComputeScreenPos(o.positionCS);
    o.eyeDepth=-TransformWorldToView(world).z;o.color=i.color;o.uv=TRANSFORM_TEX(i.uv,_MainTex);return o;
   }
   float4 Frag(Varyings i):SV_Target
   {
    float4 color=i.color;
    #if defined(SOFTPARTICLES_ON)
     float sceneDepth=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams);
     color.a*=saturate((sceneDepth-i.eyeDepth)*_InvFade);
    #endif
    float4 value=(color+color)*_TintColor*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
    value.a=saturate(value.a);return value;
   }
   ENDHLSL
  }
 }
}
