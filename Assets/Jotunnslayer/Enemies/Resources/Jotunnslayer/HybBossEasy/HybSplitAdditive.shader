Shader "Jotunnslayer/HybSplitAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaskOnly ("Use Alpha Mask", Float) = 0
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _UseVertexColor ("Use Vertex Color", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha [_DstBlend]
            Cull Off
            ZWrite Off

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed _MaskOnly;
            fixed4 _Color;
            fixed _UseVertexColor;

            struct Vertex
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Vertex input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 texel = tex2D(_MainTex, input.uv);
                texel.rgb = lerp(texel.rgb, 1, _MaskOnly);
                return texel * lerp(1, input.color, _UseVertexColor) * _Color;
            }
            ENDCG
        }
    }
}
