Shader "Hidden/Jotunnslayer/SourceTextureMipRead"
{
    Properties { _MainTex ("Source", 2D) = "white" {} _SourceMip ("Mip", Float) = 0 }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _SourceMip;
            float4 Frag(v2f_img input) : SV_Target
            {
                return tex2Dlod(_MainTex, float4(input.uv, 0, _SourceMip));
            }
            ENDCG
        }
    }
}
