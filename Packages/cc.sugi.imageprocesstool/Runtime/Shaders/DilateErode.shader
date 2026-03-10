Shader "sugi.cc/ImageProcess/DilateErode"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0, 4)) = 1
        _Mode ("Mode (-1 Erode / 1 Dilate)", Range(-1, 1)) = 1
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
            float _Mode;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * _Radius;
                float4 center = tex2D(_MainTex, input.uv);
                float4 result = center;
                float4 s1 = tex2D(_MainTex, input.uv + float2(t.x, 0));
                float4 s2 = tex2D(_MainTex, input.uv + float2(-t.x, 0));
                float4 s3 = tex2D(_MainTex, input.uv + float2(0, t.y));
                float4 s4 = tex2D(_MainTex, input.uv + float2(0, -t.y));
                float4 s5 = tex2D(_MainTex, input.uv + t);
                float4 s6 = tex2D(_MainTex, input.uv - t);
                float4 s7 = tex2D(_MainTex, input.uv + float2(t.x, -t.y));
                float4 s8 = tex2D(_MainTex, input.uv + float2(-t.x, t.y));

                if (_Mode >= 0)
                {
                    result = max(result, s1);
                    result = max(result, s2);
                    result = max(result, s3);
                    result = max(result, s4);
                    result = max(result, s5);
                    result = max(result, s6);
                    result = max(result, s7);
                    result = max(result, s8);
                }
                else
                {
                    result = min(result, s1);
                    result = min(result, s2);
                    result = min(result, s3);
                    result = min(result, s4);
                    result = min(result, s5);
                    result = min(result, s6);
                    result = min(result, s7);
                    result = min(result, s8);
                }

                return result;
            }
            ENDHLSL
        }
    }
}
