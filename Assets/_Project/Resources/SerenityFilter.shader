// Serenity's screen look (Scripts/Effects/SerenityFilter.cs). Built-in render pipeline, OnRenderImage.
// Lives in Resources so builds include it. Everything at 0 leaves the picture untouched.
//   An indigo wash and a desaturation over everything EXCEPT a soft ellipse around the player (they stay in full
//   color), an indigo vignette, a flash, and a ripple ring that pushes the picture outward.
Shader "Hidden/Roygbiv/SerenityFilter"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Tint;      // rgb, a = amount
            float4 _Vignette;  // rgb, a = amount
            float4 _Flash;     // rgb, a = amount
            float4 _Shock;     // xy = center (uv), z = radius (screen heights), w = strength
            float4 _Keep;      // xy = player center (uv), z = radius (screen heights), w = soft edge (screen heights)
            float _Desaturate;
            float _Aspect;

            fixed4 frag(v2f_img i) : SV_Target
            {
                // Ripple: a ring that pushes the picture outward as it expands.
                float2 uv = i.uv;
                float2 d = uv - _Shock.xy;
                d.x *= _Aspect;
                float dist = max(length(d), 1e-4);
                float q = (dist - _Shock.z) * 10.0; // squared by hand: pow() of a negative number is NaN in HLSL
                float ring = _Shock.w * exp(-q * q);
                uv -= (d / dist) * ring * 0.06 * float2(1.0 / _Aspect, 1.0);

                float3 src = tex2D(_MainTex, uv).rgb;
                float lum = dot(src, float3(0.299, 0.587, 0.114));

                // The world: washed out a little and tinted indigo.
                float3 world = lerp(src, lum.xxx, _Desaturate);
                world = lerp(world, world * (0.55 + 0.9 * _Tint.rgb) + lum * _Tint.rgb * 0.15, _Tint.a);

                // The player keeps their own colors.
                float2 p = i.uv - _Keep.xy;
                p.x *= _Aspect;
                float keep = 1.0 - smoothstep(_Keep.z, _Keep.z + max(_Keep.w, 1e-4), length(p));
                float3 col = lerp(world, src, keep);

                float2 v = i.uv - 0.5;
                v.x *= _Aspect;
                float edge = smoothstep(0.3, 1.0, length(v) * 1.15);
                col = lerp(col, _Vignette.rgb, saturate(edge * _Vignette.a));

                col = lerp(col, _Flash.rgb, _Flash.a);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
