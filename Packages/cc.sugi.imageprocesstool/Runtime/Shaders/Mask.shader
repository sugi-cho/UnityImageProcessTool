Shader "sugi.cc/ImageProcess/Mask"
{
    Properties
    {
        _MainTex ("Foreground", 2D) = "white" {}
        _BackgroundTex ("Background", 2D) = "black" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _MaskSettings ("Opacity X / Invert Y", Vector) = (1,0,0,0)
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

            sampler2D _BackgroundTex;
            sampler2D _MaskTex;
            float4 _MaskSettings;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 fg = tex2D(_MainTex, input.uv);
                float4 bg = tex2D(_BackgroundTex, input.uv);
                float mask = tex2D(_MaskTex, input.uv).r;
                mask = lerp(mask, 1.0 - mask, step(0.5, _MaskSettings.y));
                mask = saturate(mask * _MaskSettings.x);
                return lerp(bg, fg, mask);
            }
            ENDHLSL
        }
    }
}
