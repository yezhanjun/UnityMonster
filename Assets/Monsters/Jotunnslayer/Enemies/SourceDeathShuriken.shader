Shader "Jotunnslayer/SourceDeathShuriken"
{
 Properties
 {
  _mainTexture("Source texture",2D)="white"{}
  [HDR]_color("Source color",Color)=(1,1,1,1)
  _noiseSpeed("Source noise speed",Vector)=(0,0,0,0)
  _noiseScale("Source noise scale",Float)=0
  _distortionAmount("Source distortion",Float)=0
  _SourceExposure("Exposure multiplier",Float)=1
  [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Name "SourceSurface"
   Blend One One
   ZWrite Off
   Cull [_Cull]
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_mainTexture); SAMPLER(sampler_mainTexture);
   CBUFFER_START(UnityPerMaterial)
    float4 _color;
    float4 _noiseSpeed;
    float _noiseScale;
    float _distortionAmount;
    float _SourceExposure;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   Varyings Vert(Attributes v)
   {
    Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
    o.color=v.color;o.uv=v.uv;return o;
   }
   float2 SourceGradient(int2 cell)
   {
    // Source DXBC integer hash wraps at 32 bits. Normalizer literal is 0x33800001,
    // not 2^-24: the text disassembler rounds both values to 0.000000.
    uint y=asuint(cell.y)^0x41c64e6du;
    uint hash=(asuint(cell.x)+y)*y;
    hash=(hash^(hash>>5))*0x27d4eb2du;
    float value=(float)(hash>>8)*asfloat(0x33800001u);
    return normalize(float2(value-floor(value+.5),value-.5));
   }
   float SourceNoise(float2 position)
   {
    float2 cell=floor(position),f=frac(position);
    float a=dot(SourceGradient((int2)cell),f);
    float b=dot(SourceGradient((int2)(cell+float2(0,1))),f-float2(0,1));
    float c=dot(SourceGradient((int2)(cell+float2(1,0))),f-float2(1,0));
    float d=dot(SourceGradient((int2)(cell+1)),f-1);
    float2 fade=f*f*f*(f*(f*6-15)+10);
    return lerp(lerp(a,b,fade.y),lerp(c,d,fade.y),fade.x)+.5;
   }
   float4 Frag(Varyings i):SV_Target
   {
    float noise=SourceNoise((i.uv+_Time.y*_noiseSpeed.xy)*_noiseScale);
    float2 uv=lerp(i.uv,noise.xx,_distortionAmount);
    float4 value=SAMPLE_TEXTURE2D(_mainTexture,sampler_mainTexture,uv)*_color*i.color;
    value.rgb*= _SourceExposure;
    value.rgb*=value.a; // Actual source death materials use BlendMode1 and One/One.
    // Source HDRP atmospheric fog and global mip bias need separate pipeline integration.
    return value;
   }
   ENDHLSL
  }
 }
}
