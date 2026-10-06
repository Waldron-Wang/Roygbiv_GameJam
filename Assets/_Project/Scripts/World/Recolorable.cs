using UnityEngine;
using UnityEngine.Tilemaps;

namespace Roygbiv
{
    /// <summary>
    /// Put on any sprite / tilemap that belongs to a color's district. It shows gray until that color
    /// is restored, then fades to its authored color.
    ///
    /// PLACEHOLDER LOOK: lerps the renderer tint (works with white/flat sprites). Once the art style is
    /// chosen, swap the body of Apply() for a desaturation material/shader — nothing else changes.
    /// </summary>
    public class Recolorable : MonoBehaviour
    {
        [SerializeField] ColorId color;
        [Range(0f, 1f)] [SerializeField] float grayBrightness = 0.45f;

        SpriteRenderer sprite;
        Tilemap tilemap;
        Color authored;

        /// <summary>Which color it belongs to. Set it before the object wakes up (on an inactive template) when made from code.</summary>
        public ColorId ColorId { get => color; set => color = value; }

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            tilemap = GetComponent<Tilemap>();
            authored = sprite ? sprite.color : tilemap ? tilemap.color : Color.white;
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
            float g = authored.grayscale * grayBrightness * 2f;
            var gray = new Color(g, g, g, authored.a);
            var c = Color.Lerp(gray, authored, amount);
            if (sprite) sprite.color = c;
            if (tilemap) tilemap.color = c;
        }
    }
}
