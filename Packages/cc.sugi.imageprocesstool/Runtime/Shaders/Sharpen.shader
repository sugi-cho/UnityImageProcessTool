Shader "sugi.cc/ImageProcess/Sharpen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Strength", Range(0, 4)) = 1
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

            float _Strength;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy;
                float4 center = tex2D(_MainTex, input.uv);
                float4 blur = tex2D(_MainTex, input.uv + float2(t.x, 0));
                blur += tex2D(_MainTex, input.uv + float2(-t.x, 0));
                blur += tex2D(_MainTex, input.uv + float2(0, t.y));
                blur += tex2D(_MainTex, input.uv + float2(0, -t.y));
                blur *= 0.25;
                return float4(saturate(center.rgb + (center.rgb - blur.rgb) * _Strength), center.a);
            }
            ENDHLSL
        }
    }
}
