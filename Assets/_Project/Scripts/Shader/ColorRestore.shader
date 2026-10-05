Shader "Roygbiv/ColorRestore"
{
    Properties
    {
        _Width ("Hue Falloff", Range(0.02, 0.3)) = 0.1
        _FullColor ("Full Color (bypass)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "ColorRestore"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            
            // Violet, Indigo, Blue, Green, Yellow, Orange, Red
            float _Unlock[7];

            CBUFFER_START(UnityPerMaterial)
                float _Width;
                float _FullColor;
            CBUFFER_END

            static const float HueCenters[7] =
            {
                0.78,   // Violet
                0.74,   // Indigo
                0.667,  // Blue
                0.333,  // Green
                0.167,  // Yellow
                0.083,  // Orange
                0.0     // Red
            };

            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                float hue = RGBToHSV(col.rgb).x;

                // How much of this pixel's color is allowed through.
                float keep = 0;
                for (int i = 0; i < 7; i++)
                {
                    float d = abs(hue - HueCenters[i]);
                    d = min(d, 1.0 - d); // hue wraps around
                    float w = 1.0 - smoothstep(_Width * 0.5, _Width, d);
                    keep = max(keep, w * _Unlock[i]);
                }

                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float3 result = lerp(gray.xxx, col.rgb, keep);

                // Once every color is unlocked, blend to the untouched image
                // so the final look is exactly the original game.
                result = lerp(result, col.rgb, _FullColor);

                return half4(result, col.a);
            }
            ENDHLSL
        }
    }
}