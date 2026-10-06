using UnityEngine;
using UnityEngine.Tilemaps;

namespace Roygbiv
{
    /// <summary>
    /// Put on any sprite / tilemap that belongs to a color's district. It shows gray until that color
    /// is restored, then fades to its authored color.
    ///
    /// Two looks:
    ///   Tint (default): lerps the renderer tint from gray. Fine for white/flat placeholder sprites, but
    ///     painted art keeps its hue (it only gets darker).
    ///   Desaturate: swaps in the Roygbiv/Recolor Sprite material, which grays out the actual texture
    ///     and fades its painted colors back in. Use this for real art (tilemaps, painted sprites).
    /// </summary>
    public class Recolorable : MonoBehaviour
    {
        const string DesaturateShader = "RecolorSprite"; // Resources/RecolorSprite.shader
        static readonly int AmountId = Shader.PropertyToID("_ColorAmount");
        static readonly int GrayBrightnessId = Shader.PropertyToID("_GrayBrightness");
        static Material desaturateMaterial;

        [SerializeField] ColorId color;
        [Range(0f, 1f)] [SerializeField] float grayBrightness = 0.45f;
        [Tooltip("For painted art: grays out the texture itself instead of tinting it. Replaces the renderer's material.")]
        [SerializeField] bool desaturate;

        SpriteRenderer sprite;
        Tilemap tilemap;
        Renderer target;
        MaterialPropertyBlock block;
        Color authored;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            tilemap = GetComponent<Tilemap>();
            authored = sprite ? sprite.color : tilemap ? tilemap.color : Color.white;

            if (desaturate && TryGetComponent(out target))
            {
                if (!desaturateMaterial) desaturateMaterial = new Material(Resources.Load<Shader>(DesaturateShader));
                target.sharedMaterial = desaturateMaterial;
                block = new MaterialPropertyBlock();
            }
        }

        void OnEnable()
        {
            if (Game.Colors == null) return;
            Game.Colors.AmountChanged += OnAmountChanged;
            Apply(Game.Colors.GetAmount(color));
        }

        void OnDisable()
        {
            if (Game.Colors != null) Game.Colors.AmountChanged -= OnAmountChanged;
        }

        void OnAmountChanged(ColorId changed, float amount)
        {
            if (changed == color) Apply(amount);
        }

        void Apply(float amount)
        {
            if (block != null)
            {
                target.GetPropertyBlock(block);
                block.SetFloat(AmountId, amount);
                block.SetFloat(GrayBrightnessId, grayBrightness);
                target.SetPropertyBlock(block);
                return;
            }

            float g = authored.grayscale * grayBrightness * 2f;
            var gray = new Color(g, g, g, authored.a);
            var c = Color.Lerp(gray, authored, amount);
            if (sprite) sprite.color = c;
            if (tilemap) tilemap.color = c;
        }
    }
}
