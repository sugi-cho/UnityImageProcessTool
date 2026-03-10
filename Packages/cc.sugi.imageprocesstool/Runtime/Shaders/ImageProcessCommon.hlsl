#ifndef SUGI_IMAGE_PROCESS_COMMON_INCLUDED
#define SUGI_IMAGE_PROCESS_COMMON_INCLUDED

#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_TexelSize;

struct ImageProcessAttributes
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct ImageProcessVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

ImageProcessVaryings ImageProcessVert(ImageProcessAttributes input)
{
    ImageProcessVaryings output;
    output.positionCS = UnityObjectToClipPos(input.vertex);
    output.uv = input.uv;
    return output;
}

float2 RotateUV(float2 uv, float2 center, float angleRadians)
{
    float s = sin(angleRadians);
    float c = cos(angleRadians);
    float2 delta = uv - center;
    return float2(delta.x * c - delta.y * s, delta.x * s + delta.y * c) + center;
}

float3 RGBToHSV(float3 rgb)
{
    float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(rgb.bg, k.wz), float4(rgb.gb, k.xy), step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 HSVToRGB(float3 hsv)
{
    float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(hsv.xxx + k.xyz) * 6.0 - k.www);
    return hsv.z * lerp(k.xxx, saturate(p - k.xxx), hsv.y);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

#endif
