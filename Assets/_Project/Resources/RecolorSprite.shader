// Grayscale-to-color sprite shader for Recolorable (Scripts/World/Recolorable.cs) in "desaturate" mode.
// Same as Sprites/Default, plus _ColorAmount: 0 = gray, 1 = the painted colors. Works on SpriteRenderer and TilemapRenderer.
// Lives in Resources so builds include it.
Shader "Roygbiv/Recolor Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _ColorAmount ("Color Amount", Range(0, 1)) = 1
        [PerRendererData] _GrayBrightness ("Gray Brightness", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _ColorAmount;
            float _GrayBrightness;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                // Same gray as Recolorable's tint mode: luminance scaled by grayBrightness (0.5 = unchanged).
                float gray = dot(c.rgb, float3(0.299, 0.587, 0.114)) * _GrayBrightness * 2.0;
                c.rgb = lerp(gray.xxx, c.rgb, _ColorAmount);
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
