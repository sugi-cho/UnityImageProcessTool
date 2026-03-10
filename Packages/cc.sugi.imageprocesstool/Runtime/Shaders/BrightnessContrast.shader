Shader "sugi.cc/ImageProcess/BrightnessContrast"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Contrast ("Contrast", Range(-2, 2)) = 0
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

            float _Brightness;
            float _Contrast;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                color.rgb += float3(_Brightness, _Brightness, _Brightness);
                color.rgb = (color.rgb - 0.5) * (1.0 + _Contrast) + 0.5;
                return saturate(color);
            }
            ENDHLSL
        }
    }
}
