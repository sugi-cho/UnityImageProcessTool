Shader "sugi.cc/ImageProcess/HueSaturationValue"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HueShift ("Hue Shift", Range(-1, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Value ("Value", Range(0, 2)) = 1
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

            float _HueShift;
            float _Saturation;
            float _Value;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                float3 hsv = RGBToHSV(saturate(color.rgb));
                hsv.x = frac(hsv.x + _HueShift);
                hsv.y = saturate(hsv.y * _Saturation);
                hsv.z = saturate(hsv.z * _Value);
                color.rgb = HSVToRGB(hsv);
                return color;
            }
            ENDHLSL
        }
    }
}
