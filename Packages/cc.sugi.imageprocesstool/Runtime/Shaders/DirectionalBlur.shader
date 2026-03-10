Shader "sugi.cc/ImageProcess/DirectionalBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Direction ("Direction XY / Distance Z", Vector) = (1,0,8,0)
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

            float4 _Direction;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float2 dir = normalize(_Direction.xy + float2(1.0e-6, 1.0e-6));
                float distanceScale = _Direction.z;
                float2 stepOffset = dir * _MainTex_TexelSize.xy * distanceScale;

                float4 sum = 0;
                sum += tex2D(_MainTex, input.uv + stepOffset * -4.0) * 0.08;
                sum += tex2D(_MainTex, input.uv + stepOffset * -2.0) * 0.12;
                sum += tex2D(_MainTex, input.uv + stepOffset * -1.0) * 0.18;
                sum += tex2D(_MainTex, input.uv) * 0.24;
                sum += tex2D(_MainTex, input.uv + stepOffset * 1.0) * 0.18;
                sum += tex2D(_MainTex, input.uv + stepOffset * 2.0) * 0.12;
                sum += tex2D(_MainTex, input.uv + stepOffset * 4.0) * 0.08;
                return sum;
            }
            ENDHLSL
        }
    }
}
