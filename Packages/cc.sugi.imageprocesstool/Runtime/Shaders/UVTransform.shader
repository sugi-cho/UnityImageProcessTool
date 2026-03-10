Shader "sugi.cc/ImageProcess/UVTransform"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScaleOffset ("Scale XY / Offset ZW", Vector) = (1,1,0,0)
        _RotationCenter ("Rotation / Center XY", Vector) = (0,0.5,0.5,0)
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

            float4 _ScaleOffset;
            float4 _RotationCenter;

            float4 Frag(ImageProcessVaryings input) : SV_Target
            {
                float angle = radians(_RotationCenter.x);
                float2 center = _RotationCenter.yz;
                float2 uv = (input.uv - center) / max(_ScaleOffset.xy, float2(1.0e-5, 1.0e-5)) + center + _ScaleOffset.zw;
                uv = RotateUV(uv, center, angle);
                return tex2D(_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
