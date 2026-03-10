Shader "sugi.cc/ImageProcess/Threshold"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Threshold", Range(0, 1)) = 0.5
        _Softness ("Softness", Range(0.0001, 1)) = 0.05
        _LowColor ("Low Color", Color) = (0,0,0,1)
        _HighColor ("High Color", Color) = (1,1,1,1)
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

            float _Threshold;
            float _Softness;
            float4 _LowColor;
            float4 _HighColor;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                float luma = Luminance(color.rgb);
                float t = smoothstep(_Threshold - _Softness, _Threshold + _Softness, luma);
                return lerp(_LowColor, _HighColor, t);
            }
            ENDHLSL
        }
    }
}
