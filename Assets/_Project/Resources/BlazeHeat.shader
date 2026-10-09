// Heat haze for Blaze Strike (Scripts/Effects/BlazeStrikeVisuals.cs): bends whatever is drawn behind it.
// A GrabPass copy of the screen, offset by a wavy shimmer (masked by the sprite's alpha) and/or a ring that pushes
// outward. Where the offset is zero it is the screen itself, so the quad's edges never show.
// Built-in pipeline only (GrabPass). Lives in Resources so builds include it.
Shader "Roygbiv/Blaze Heat"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask", 2D) = "white" {}
        _Strength ("Strength (screen fraction)", Float) = 0.005
        _Shimmer ("Shimmer (0..1)", Float) = 1
        _Ring ("Ring (0..1)", Float) = 0
        _RingRadius ("Ring Radius (0..1 of the quad)", Float) = 0.5
        _RingWidth ("Ring Width", Float) = 0.12
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off

        GrabPass { "_BlazeGrab" }

        Pass
        {
            Blend One Zero

        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BlazeGrab;
            float _Strength, _Shimmer, _Ring, _RingRadius, _RingWidth;
            fixed4 _RendererColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grab : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grab = ComputeGrabScreenPos(o.pos);
                o.color = v.color * _RendererColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).a * i.color.a;
                float2 c = i.uv - 0.5;
                float d = length(c) * 2.0;                      // 0 at the center, 1 at the quad's edge
                float2 dir = d > 0.0001 ? c * (2.0 / d) : float2(0.0, 0.0);
                float edge = saturate((1.0 - d) * 5.0);          // the ring fades out before the quad ends
                float ring = exp(-pow((d - _RingRadius) / max(_RingWidth, 0.001), 2.0)) * _Ring * edge;
                float t = _Time.y;
                float2 wave = float2(sin(i.uv.y * 23.0 + t * 9.0) + 0.5 * sin(i.uv.y * 41.0 - t * 13.0),
                                     0.6 * sin(i.uv.x * 19.0 + t * 7.0) + 0.4 * sin(i.uv.y * 31.0 + t * 11.0));
                float2 offset = (dir * ring + wave * _Shimmer * mask) * _Strength;
                return tex2D(_BlazeGrab, i.grab.xy / i.grab.w + offset);
            }
        ENDCG
        }
    }
    Fallback Off
}
