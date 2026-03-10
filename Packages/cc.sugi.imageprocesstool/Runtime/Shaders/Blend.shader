Shader "sugi.cc/ImageProcess/Blend"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _BlendTex ("Blend", 2D) = "black" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Mode ("Mode", Range(0, 4)) = 0
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

            sampler2D _BlendTex;
            float _Opacity;
            float _Mode;

            float3 ApplyBlendMode(float3 baseColor, float3 blendColor)
            {
                if (_Mode < 0.5)
                {
                    return blendColor;
                }
                if (_Mode < 1.5)
                {
                    return baseColor + blendColor;
                }
                if (_Mode < 2.5)
                {
                    return baseColor * blendColor;
                }
                if (_Mode < 3.5)
                {
                    return 1.0 - (1.0 - baseColor) * (1.0 - blendColor);
                }

                float3 low = 2.0 * baseColor * blendColor;
                float3 high = 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
                return lerp(low, high, step(0.5, baseColor));
            }

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float4 baseColor = tex2D(_MainTex, input.uv);
                float4 blendColor = tex2D(_BlendTex, input.uv);
                float3 blended = ApplyBlendMode(baseColor.rgb, blendColor.rgb);
                float alpha = saturate(_Opacity * blendColor.a);
                return float4(lerp(baseColor.rgb, saturate(blended), alpha), baseColor.a);
            }
            ENDHLSL
        }
    }
}
