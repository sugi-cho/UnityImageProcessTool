Shader "sugi.cc/ImageProcess/GaussianBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0, 5)) = 1
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

            float _Radius;

            float4 SampleWeighted(float2 uv, float2 offset, float weight)
            {
                return tex2D(_MainTex, uv + offset * _MainTex_TexelSize.xy * _Radius) * weight;
            }

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 sum = 0;
                sum += SampleWeighted(input.uv, float2(0, 0), 0.22702703);
                sum += SampleWeighted(input.uv, float2(1.38461538, 0), 0.15810811);
                sum += SampleWeighted(input.uv, float2(-1.38461538, 0), 0.15810811);
                sum += SampleWeighted(input.uv, float2(0, 1.38461538), 0.15810811);
                sum += SampleWeighted(input.uv, float2(0, -1.38461538), 0.15810811);
                sum += SampleWeighted(input.uv, float2(3.23076923, 0), 0.03513513);
                sum += SampleWeighted(input.uv, float2(-3.23076923, 0), 0.03513513);
                sum += SampleWeighted(input.uv, float2(0, 3.23076923), 0.03513513);
                sum += SampleWeighted(input.uv, float2(0, -3.23076923), 0.03513513);
                return sum;
            }
            ENDHLSL
        }
    }
}
