Shader "sugi.cc/ImageProcess/SobelEdge"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Thickness ("Thickness", Range(0.5, 4)) = 1
        _EdgeStrength ("Edge Strength", Range(0, 8)) = 1
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _BackgroundFade ("Background Fade", Range(0, 1)) = 1
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

            float _Thickness;
            float _EdgeStrength;
            float4 _EdgeColor;
            float _BackgroundFade;

            float SampleLuma(float2 uv)
            {
                return Luminance(tex2D(_MainTex, uv).rgb);
            }

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * _Thickness;
                float tl = SampleLuma(input.uv + float2(-t.x, t.y));
                float tc = SampleLuma(input.uv + float2(0, t.y));
                float tr = SampleLuma(input.uv + float2(t.x, t.y));
                float ml = SampleLuma(input.uv + float2(-t.x, 0));
                float mr = SampleLuma(input.uv + float2(t.x, 0));
                float bl = SampleLuma(input.uv + float2(-t.x, -t.y));
                float bc = SampleLuma(input.uv + float2(0, -t.y));
                float br = SampleLuma(input.uv + float2(t.x, -t.y));

                float gx = -tl - 2.0 * ml - bl + tr + 2.0 * mr + br;
                float gy = -bl - 2.0 * bc - br + tl + 2.0 * tc + tr;
                float edge = saturate(length(float2(gx, gy)) * _EdgeStrength);
                float3 source = tex2D(_MainTex, input.uv).rgb * _BackgroundFade;
                return float4(lerp(source, _EdgeColor.rgb, edge), 1);
            }
            ENDHLSL
        }
    }
}
