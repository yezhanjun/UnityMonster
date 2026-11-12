Shader "Jotunnslayer/SourceDeathAnimatedAlpha"
{
 Properties
 {
  _Tint("Source tint",Color)=(.4980392,.4980392,.4980392,1)
  _MainTexA("Texture A",2D)="white"{}
  _MainTexB("Texture B",2D)="white"{}
  _MainTexC("Texture C",2D)="white"{}
  _MaskTexture("Mask",2D)="white"{}
  [Normal]_LocalDistortionMap("Distortion",2D)="bump"{}
  _MainTextureATo("A tiling offset",Vector)=(1,1,0,0)
  _MainTextureBTo("B tiling offset",Vector)=(1,1,0,0)
  _MainTextureCTo("C tiling offset",Vector)=(1,1,0,0)
  _MaskTilingTo("Mask tiling offset",Vector)=(1,1,0,0)
  _TilingAndOffset("Combined tiling offset",Vector)=(1,1,0,0)
  _LocalDistortionMapTs("Distortion tiling speed",Vector)=(1,1,0,0)
  _AnimationAB("Animation AB",Vector)=(0,0,0,0)
  _AnimationC("Animation C",Vector)=(0,0,0,0)
  _LocalDistortionStrength("Distortion strength",Float)=0
  _LocalDistortionRotation("Distortion rotation",Float)=0
  _Fade("Source soft fade",Float)=0
  _Intensity("Source intensity",Float)=1
  _BlendMode("Source blend mode",Float)=0
  _SourceProbeExposure("Probe exposure product",Float)=0
  _SourceDeExposure("De-exposure multiplier",Float)=1
  [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Name "SourceSurface"
   Blend One OneMinusSrcAlpha
   ZWrite Off
   Cull [_Cull]
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D(_MainTexA);SAMPLER(sampler_MainTexA);
   TEXTURE2D(_MainTexB);SAMPLER(sampler_MainTexB);
   TEXTURE2D(_MainTexC);SAMPLER(sampler_MainTexC);
   TEXTURE2D(_MaskTexture);SAMPLER(sampler_MaskTexture);
   TEXTURE2D(_LocalDistortionMap);SAMPLER(sampler_LocalDistortionMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _Tint,_MainTextureATo,_MainTextureBTo,_MainTextureCTo,_MaskTilingTo;
    float4 _TilingAndOffset,_LocalDistortionMapTs,_AnimationAB,_AnimationC;
    float _LocalDistortionStrength,_LocalDistortionRotation,_Fade,_Intensity,_BlendMode;
    float _SourceProbeExposure,_SourceDeExposure;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float eyeDepth:TEXCOORD1;};
   Varyings Vert(Attributes v)
   {
    Varyings o;float3 world=TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS=TransformWorldToHClip(world);o.eyeDepth=-TransformWorldToView(world).z;
    o.color=v.color;o.uv=v.uv;return o;
   }
   float2 SourceUV(float2 uv,float4 transform,float2 animation,float2 distortion)
   {
    return uv*(transform.xy*_TilingAndOffset.xy)+transform.zw+_TilingAndOffset.zw+distortion+_Time.y*animation;
   }
   float4 Frag(Varyings i):SV_Target
   {
    // ForwardOnly fragment13: source uses time.x for distortion and time.y for ABC.
    float2 duv=i.uv*_LocalDistortionMapTs.xy+_Time.x*_LocalDistortionMapTs.zw-.5*_LocalDistortionMapTs.xy;
    float angle=_Time.x*_LocalDistortionRotation*.01745329252;
    float s,c;sincos(angle,s,c);
    duv=float2(dot(duv,float2(c,s)),dot(duv,float2(-s,c)))+.5*_LocalDistortionMapTs.xy;
    float3 distortionMap=SAMPLE_TEXTURE2D(_LocalDistortionMap,sampler_LocalDistortionMap,duv).rgb;
    float strength=_LocalDistortionStrength*-.1;
    float2 distortion=distortionMap.rg*strength;
    float distortionGate=1+saturate(strength)*(distortionMap.b-1);
    float4 mask=SAMPLE_TEXTURE2D(_MaskTexture,sampler_MaskTexture,SourceUV(i.uv,_MaskTilingTo,0,0));
    distortion*=mask.a*mask.a*distortionGate;
    float3 a=SAMPLE_TEXTURE2D(_MainTexA,sampler_MainTexA,SourceUV(i.uv,_MainTextureATo,_AnimationAB.xy,distortion)).rgb;
    float3 b=SAMPLE_TEXTURE2D(_MainTexB,sampler_MainTexB,SourceUV(i.uv,_MainTextureBTo,_AnimationAB.zw,distortion)).rgb;
    float3 cc=SAMPLE_TEXTURE2D(_MainTexC,sampler_MainTexC,SourceUV(i.uv,_MainTextureCTo,_AnimationC.xy,distortion)).rgb;
    float3 opacity=saturate(mask.rgb*a*b*4)*_Tint.a*i.color.a;
    float3 color=a*b*cc*i.color.rgb*_Tint.rgb;
    float rawDepth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
    float delta=Linear01Depth(rawDepth,_ZBufferParams)-i.eyeDepth*_ProjectionParams.w;
    // Preserve source reciprocal/saturate at Fade=0 for GPU comparison; do not add epsilon.
    float t=saturate(delta*(1/(_ProjectionParams.w*_Fade*.3)));
    float edge=(1-t*t*(3-2*t))*max(sign(delta+.5),0);
    float fade=saturate(1-exp2(log2(abs(edge))*(_Fade+.0001))*edge);
    opacity*=fade;
    float alpha=saturate(opacity.r*_Intensity);
    color=color*opacity*_Intensity*mask.rgb;
    color=color*(_SourceProbeExposure+alpha*_SourceDeExposure);
    if(_BlendMode==0 || _BlendMode==1)color*=alpha;
    return float4(color,alpha);
   }
   ENDHLSL
  }
 }
}
