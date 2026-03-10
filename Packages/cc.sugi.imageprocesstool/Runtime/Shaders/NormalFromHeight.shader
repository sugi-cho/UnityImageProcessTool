Shader "sugi.cc/ImageProcess/NormalFromHeight"
{
    Properties
    {
        _MainTex ("Height", 2D) = "gray" {}
        _Strength ("Strength", Range(0, 8)) = 1
        _FlipY ("Flip Y", Range(0, 1)) = 0
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
            float _FlipY;

            float SampleHeight(float2 uv)
            {
                return Luminance(tex2D(_MainTex, uv).rgb);
            }

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy;
                float left = SampleHeight(input.uv - float2(t.x, 0));
                float right = SampleHeight(input.uv + float2(t.x, 0));
                float down = SampleHeight(input.uv - float2(0, t.y));
                float up = SampleHeight(input.uv + float2(0, t.y));

                float dx = (left - right) * _Strength;
                float dy = (down - up) * _Strength;
                dy = lerp(dy, -dy, _FlipY);
                float3 normal = normalize(float3(dx, dy, 1.0));
                return float4(normal * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
    }
}
