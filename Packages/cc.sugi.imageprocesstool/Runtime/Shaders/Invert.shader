Shader "sugi.cc/ImageProcess/Invert"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Amount ("Amount", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex ImageProcessVert
            #pragma fragment Frag
            #include "ImageProcessCommon.hlsl"

            float _Amount;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                color.rgb = lerp(color.rgb, 1.0 - color.rgb, _Amount);
                return color;
            }
            ENDHLSL
        }
    }
}
