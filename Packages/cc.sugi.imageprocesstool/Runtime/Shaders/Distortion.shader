Shader "sugi.cc/ImageProcess/Distortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DistortionTex ("Distortion", 2D) = "gray" {}
        _Strength ("Strength XY", Vector) = (0.05,0.05,0,0)
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

            sampler2D _DistortionTex;
            float4 _Strength;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float2 distortion = tex2D(_DistortionTex, input.uv).rg * 2.0 - 1.0;
                float2 uv = input.uv + distortion * _Strength.xy;
                return tex2D(_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
