Shader "Custom/URP_MuzzleFlash_Additive"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MainTex("Main Tex (Fallback)", 2D) = "white" {}
        _TintColor("Tint Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Float) = 1.3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+500"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "MuzzleFlashAdditive"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            BlendOp Add
            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MainTex_ST;
                half4 _BaseColor;
                half4 _TintColor;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample main texture
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (col.r == 1.0 && col.g == 1.0 && col.b == 1.0 && col.a == 1.0)
                {
                    // Fallback to _MainTex if _BaseMap was default white
                    col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                }

                // If texture has alpha channel, mask RGB by alpha so zero-alpha pixels are strictly 0
                col.rgb *= col.a;

                // Threshold filter to eliminate any near-black background compression noise (under 5% brightness)
                half brightness = max(col.r, max(col.g, col.b));
                half cutoff = smoothstep(0.02, 0.12, brightness);
                col.rgb *= cutoff;

                // Apply tint and intensity multiplier
                col.rgb *= _BaseColor.rgb * _TintColor.rgb * _Intensity * input.color.rgb;

                // Return additive color (with alpha = 1 for additive output)
                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    // Built-in fallback in case URP is absent in any preview context
    SubShader
    {
        Tags { "Queue"="Transparent+500" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            SetTexture [_MainTex] {
                constantColor [_TintColor]
                combine texture * constant
            }
        }
    }
}

