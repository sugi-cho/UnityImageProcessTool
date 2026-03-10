Shader "Hidden/sugi.cc/ImageProcessTool/IterativeBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            #pragma vertex Vert
            #pragma fragment FragSeparable
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BlurOffset;
            float _BlurMode;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 FragSeparable(Varyings input) : SV_Target
            {
                const float2 dir = _BlurOffset.xy;
                fixed4 center = tex2D(_MainTex, input.uv);

                if (_BlurMode < 0.5)
                {
                    fixed4 sum = center * 0.2;
                    sum += tex2D(_MainTex, input.uv + dir) * 0.2;
                    sum += tex2D(_MainTex, input.uv - dir) * 0.2;
                    sum += tex2D(_MainTex, input.uv + dir * 2.0) * 0.2;
                    sum += tex2D(_MainTex, input.uv - dir * 2.0) * 0.2;
                    return sum;
                }

                fixed4 sum = center * 0.22702703;
                sum += tex2D(_MainTex, input.uv + dir * 1.38461538) * 0.31621622;
                sum += tex2D(_MainTex, input.uv - dir * 1.38461538) * 0.31621622;
                sum += tex2D(_MainTex, input.uv + dir * 3.23076923) * 0.07027027;
                sum += tex2D(_MainTex, input.uv - dir * 3.23076923) * 0.07027027;
                return sum;
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragKawase
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BlurOffset;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 FragKawase(Varyings input) : SV_Target
            {
                const float2 dir = _BlurOffset.xy;
                fixed4 sum = tex2D(_MainTex, input.uv) * 0.2;
                sum += tex2D(_MainTex, input.uv + float2(dir.x, dir.y)) * 0.2;
                sum += tex2D(_MainTex, input.uv + float2(-dir.x, dir.y)) * 0.2;
                sum += tex2D(_MainTex, input.uv + float2(dir.x, -dir.y)) * 0.2;
                sum += tex2D(_MainTex, input.uv + float2(-dir.x, -dir.y)) * 0.2;
                return sum;
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDilate
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BlurOffset;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 FragDilate(Varyings input) : SV_Target
            {
                const float2 dir = _BlurOffset.xy;
                fixed4 value = tex2D(_MainTex, input.uv);
                value = max(value, tex2D(_MainTex, input.uv + float2(dir.x, 0.0)));
                value = max(value, tex2D(_MainTex, input.uv - float2(dir.x, 0.0)));
                value = max(value, tex2D(_MainTex, input.uv + float2(0.0, dir.y)));
                value = max(value, tex2D(_MainTex, input.uv - float2(0.0, dir.y)));
                value = max(value, tex2D(_MainTex, input.uv + dir));
                value = max(value, tex2D(_MainTex, input.uv - dir));
                value = max(value, tex2D(_MainTex, input.uv + float2(dir.x, -dir.y)));
                value = max(value, tex2D(_MainTex, input.uv + float2(-dir.x, dir.y)));
                return value;
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragErode
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BlurOffset;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 FragErode(Varyings input) : SV_Target
            {
                const float2 dir = _BlurOffset.xy;
                fixed4 value = tex2D(_MainTex, input.uv);
                value = min(value, tex2D(_MainTex, input.uv + float2(dir.x, 0.0)));
                value = min(value, tex2D(_MainTex, input.uv - float2(dir.x, 0.0)));
                value = min(value, tex2D(_MainTex, input.uv + float2(0.0, dir.y)));
                value = min(value, tex2D(_MainTex, input.uv - float2(0.0, dir.y)));
                value = min(value, tex2D(_MainTex, input.uv + dir));
                value = min(value, tex2D(_MainTex, input.uv - dir));
                value = min(value, tex2D(_MainTex, input.uv + float2(dir.x, -dir.y)));
                value = min(value, tex2D(_MainTex, input.uv + float2(-dir.x, dir.y)));
                return value;
            }
            ENDHLSL
        }
    }
}
