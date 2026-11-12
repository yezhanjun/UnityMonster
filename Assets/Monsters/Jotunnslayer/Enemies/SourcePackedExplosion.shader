Shader "Jotunnslayer/SourcePackedExplosion"
{
 Properties
 {
  _BaseMap("Source packed atlas",2D)="white"{}
  _Emission_Power("Source emission",Float)=1
  [HideInInspector]_SrcBlend("Source blend",Float)=5
  [HideInInspector]_DstBlend("Destination blend",Float)=10
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Blend [_SrcBlend] [_DstBlend]
   ZWrite Off
   Cull Off
   ColorMask RGB
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float _Emission_Power;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 custom:TEXCOORD1;};
   struct Varyings{float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float3 custom:TEXCOORD1;};
   Varyings Vert(Attributes input)
   {
    Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    o.color=input.color;o.uv=TRANSFORM_TEX(input.uv.xy,_BaseMap);o.custom=input.custom.xyz;return o;
   }
   float4 Frag(Varyings input):SV_Target
   {
    // Transcribed from source ps_4_0 non-soft-particle variant. RGB are packed data, not final color.
    float blue=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).b;
    float4 packed=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv+blue*input.custom.z);
    float x=input.custom.x,y=input.custom.y;
    float dissolve=saturate(((packed.g-x+1)*(x*y+1))/(x*.1+1)-x*y);
    return packed.r*input.color*dissolve*_Emission_Power;
   }
   ENDHLSL
  }
 }
}
