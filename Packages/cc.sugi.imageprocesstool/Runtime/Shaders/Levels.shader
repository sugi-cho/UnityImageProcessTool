Shader "sugi.cc/ImageProcess/Levels"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _InputMin ("Input Min", Range(0, 1)) = 0
        _InputMax ("Input Max", Range(0, 1)) = 1
        _Gamma ("Gamma", Range(0.1, 4)) = 1
        _OutputMin ("Output Min", Range(0, 1)) = 0
        _OutputMax ("Output Max", Range(0, 1)) = 1
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

            float _InputMin;
            float _InputMax;
            float _Gamma;
            float _OutputMin;
            float _OutputMax;

            float3 ApplyLevels(float3 color)
            {
                float denom = max(_InputMax - _InputMin, 1.0e-5);
                float3 inputMin = float3(_InputMin, _InputMin, _InputMin);
                float3 normalized = saturate((color - inputMin) / denom);
                normalized = pow(normalized, 1.0 / max(_Gamma, 1.0e-5));
                return lerp(
                    float3(_OutputMin, _OutputMin, _OutputMin),
                    float3(_OutputMax, _OutputMax, _OutputMax),
                    normalized);
            }

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                color.rgb = saturate(ApplyLevels(color.rgb));
                return color;
            }
            ENDHLSL
        }
    }
}
