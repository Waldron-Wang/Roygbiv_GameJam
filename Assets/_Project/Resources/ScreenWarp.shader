// Full-screen distortion for ScreenWarp (Scripts/Effects/ScreenWarp.cs). Built-in render pipeline, OnRenderImage.
// Lives in Resources so builds include it. Every parameter at 0 leaves the picture untouched.
Shader "Hidden/Roygbiv/ScreenWarp"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _HistoryTex;

    float4 _Tint;          // rgb, a = amount
    float4 _Vignette;      // rgb, a = amount
    float4 _Flash;         // rgb, a = amount
    float4 _Shock;         // xy = center (uv), z = radius (screen heights), w = strength
    float _Hue;            // turns
    float _Invert, _Desaturate;
    float _Chroma, _Wave, _WaveFreq, _Mirror, _Glitch, _Jitter;
    float _Smear, _SmearZoom;
    float _T, _Seed, _Aspect;

    float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

    // Rotates a color around the gray axis (Rodrigues), so 0.5 turns swaps every hue for its opposite.
    float3 hueRotate(float3 c, float turns)
    {
        float a = turns * 6.2831853;
        const float3 k = float3(0.57735, 0.57735, 0.57735);
        float ca = cos(a);
        return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
    }

    float2 warpUV(float2 uv)
    {
        // Jitter: the whole picture twitches a little every frame.
        uv += (float2(hash(float2(_Seed, 1.7)), hash(float2(3.1, _Seed))) - 0.5) * _Jitter;

        // Glitch: horizontal slices jump sideways, re-rolled ~11 times a second.
        float band = floor(uv.y * 18.0);
        float tick = floor(_T * 11.0);
        float roll = hash(float2(band, tick));
        uv.x += step(1.0 - _Glitch * 0.45, roll) * (hash(float2(tick, band + 7.0)) - 0.5) * 0.25 * _Glitch;

        // Wave: a slow seasick wobble.
        uv.x += sin(uv.y * _WaveFreq + _T * 2.3) * _Wave;
        uv.y += sin(uv.x * _WaveFreq * 0.8 + _T * 1.7) * _Wave * 0.6;

        // Shockwave: a ring that pushes the picture outward as it expands.
        float2 d = uv - _Shock.xy;
        d.x *= _Aspect;
        float dist = max(length(d), 1e-4);
        float q = (dist - _Shock.z) * 12.0; // squared by hand: pow() of a negative number is NaN in HLSL
        float ring = _Shock.w * exp(-q * q);
        uv -= (d / dist) * ring * 0.07 * float2(1.0 / _Aspect, 1.0);
        return uv;
    }

    // Chromatic aberration: red and blue slide apart, more toward the edges, plus a sideways split.
    float3 sampleSplit(float2 uv)
    {
        float2 off = (uv - 0.5) * _Chroma + float2(_Chroma * 0.6, 0.0);
        return float3(tex2D(_MainTex, uv + off).r, tex2D(_MainTex, uv).g, tex2D(_MainTex, uv - off).b);
    }

    fixed4 fragWarp(v2f_img i) : SV_Target
    {
        float2 uv = warpUV(i.uv);
        float3 col = sampleSplit(uv);

        // Mirror ghost: a left-right flipped copy of the world bleeds through.
        if (_Mirror > 0.0) col = lerp(col, sampleSplit(float2(1.0 - uv.x, uv.y)), _Mirror);

        col = saturate(hueRotate(col, _Hue));
        col = lerp(col, 1.0 - col, _Invert);

        float lum = dot(col, float3(0.299, 0.587, 0.114));
        col = lerp(col, lum.xxx, _Desaturate);
        col = lerp(col, (lum * 2.0 + 0.015) * _Tint.rgb, _Tint.a); // duotone wash; the lift keeps blacks faintly tinted

        float2 v = i.uv - 0.5;
        v.x *= _Aspect;
        float edge = smoothstep(0.35, 1.0, length(v) * 1.2);
        col = lerp(col, _Vignette.rgb, saturate(edge * _Vignette.a));

        col = lerp(col, _Flash.rgb, _Flash.a);
        return fixed4(col, 1.0);
    }

    // Motion trails: blend in last frame, zoomed in a touch and drifting through the hues (a tunnel of echoes).
    fixed4 fragSmear(v2f_img i) : SV_Target
    {
        float3 cur = tex2D(_MainTex, i.uv).rgb;
        float2 huv = (i.uv - 0.5) * (1.0 - _SmearZoom) + 0.5;
        float3 hist = hueRotate(tex2D(_HistoryTex, huv).rgb, 0.025);
        return fixed4(lerp(cur, hist, _Smear), 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0: warp + color
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragWarp
            ENDCG
        }

        Pass // 1: trails
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragSmear
            ENDCG
        }
    }
    Fallback Off
}
