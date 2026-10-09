using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A faint light around the player: a soft white glow from the start, then one small colored light behind them for
    /// every color restored, fanned out across their back in spectrum order. The colored lights trail a little when the
    /// player moves and swing round when they turn. All of it sorts behind the player's sprite and stays dim on purpose.
    ///
    /// Reads ColorWorld (not the save file), so a newly restored color's light fades in with the world's recolor.
    /// Builds its sprites in code; nothing to wire up beyond adding it to the player.
    /// </summary>
    public class PlayerGlow : MonoBehaviour
    {
        static readonly ColorId[] AllColors = (ColorId[])Enum.GetValues(typeof(ColorId));
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static Sprite soft;
        static Material glowMat;

        [Header("White glow")]
        [Tooltip("Its size, as a multiple of the player's height.")]
        public float whiteSize = 2.2f;
        [Tooltip("Its opacity. Keep it low: it should read as a faint presence, not a spotlight.")]
        [Range(0f, 1f)] public float whiteAlpha = 0.1f;
        public Color whiteColor = new(1f, 0.97f, 0.92f);

        [Header("Color lights (one per restored color)")]
        [Tooltip("Each light's size, as a multiple of the player's height.")]
        public float colorSize = 0.9f;
        [Tooltip("Each light's opacity once its color is fully restored.")]
        [Range(0f, 1f)] public float colorAlpha = 0.16f;
        [Tooltip("How far behind the player's center the fan sits, as a multiple of the player's height.")]
        public float backOffset = 0.3f;
        [Tooltip("The fan's radius, as a multiple of the player's height.")]
        public float fanRadius = 0.32f;
        [Tooltip("Degrees between neighboring lights in the fan (the whole fan never spreads past 170).")]
        public float fanStep = 26f;
        [Tooltip("How quickly the lights catch up with the player (higher = tighter, lower = more trail).")]
        public float follow = 9f;
        [Tooltip("How far each light drifts up and down (multiple of the player's height).")]
        public float bob = 0.035f;

        [Header("Breathing")]
        [Tooltip("How much every light's opacity swells and fades. 0 = steady.")]
        [Range(0f, 0.5f)] public float breathe = 0.15f;
        [Tooltip("Seconds per breath.")]
        public float breathPeriod = 3.5f;

        [Tooltip("Multiplied into everything above: one knob to turn the whole effect up or down.")]
        [Range(0f, 2f)] public float intensity = 1f;

        PlayerMotor motor;
        SpriteRenderer body;
        Collider2D bodyCollider;
        SpriteRenderer white;
        readonly SpriteRenderer[] lights = new SpriteRenderer[AllColors.Length];
        readonly Vector2[] lightPos = new Vector2[AllColors.Length];
        bool placed;
        float height = 1f;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            bodyCollider = GetComponent<Collider2D>();
            var anim = GetComponent<PlayerAnimator>();
            body = anim && anim.sprite ? anim.sprite : GetComponentInChildren<SpriteRenderer>();
            EnsureAssets();

            int order = body ? body.sortingOrder : 0;
            int layer = body ? body.sortingLayerID : 0;
            white = MakeSprite("Glow_White", layer, order - 2);
            for (int i = 0; i < AllColors.Length; i++) lights[i] = MakeSprite("Glow_" + AllColors[i], layer, order - 1);
        }

        void OnDisable() => placed = false; // snap back into place, not trail across the level, when re-enabled

        void LateUpdate()
        {
            bool shown = !body || (body.enabled && body.gameObject.activeInHierarchy);
            // A disabled collider reports empty bounds at the origin: keep the last size and sit on the transform instead.
            bool sized = bodyCollider && bodyCollider.enabled && bodyCollider.bounds.size.y > 0.01f;
            if (sized) height = Mathf.Max(0.2f, bodyCollider.bounds.size.y);
            Vector2 center = sized ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position + Vector2.up * (height * 0.5f);
            float now = Time.time;
            float breath = 1f - breathe * (0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f / Mathf.Max(0.1f, breathPeriod)));
            float z = transform.position.z;

            Place(white, center, z, whiteSize * height, whiteColor, shown ? whiteAlpha * breath * intensity : 0f);

            // The fan: the restored colors in spectrum order, top to bottom, centered on the player's back.
            int count = 0;
            foreach (var c in AllColors) if (Amount(c) > 0.001f) count++;
            float spread = Mathf.Min(170f, fanStep * Mathf.Max(0, count - 1));
            float back = -(motor ? motor.FacingSign : 1);
            Vector2 hub = center + new Vector2(back * backOffset * height, 0f);
            float k = 1f - Mathf.Exp(-follow * Time.deltaTime);

            int slot = 0;
            for (int i = 0; i < AllColors.Length; i++)
            {
                float amount = Amount(AllColors[i]);
                if (amount <= 0.001f)
                {
                    Place(lights[i], hub, z, 0f, Color.clear, 0f);
                    lightPos[i] = hub;
                    continue;
                }
                float t = count > 1 ? slot / (float)(count - 1) : 0.5f;
                float angle = (spread * 0.5f - spread * t) * Mathf.Deg2Rad; // 0 = straight back
                var target = hub + new Vector2(back * Mathf.Cos(angle), Mathf.Sin(angle)) * (fanRadius * height)
                                 + Vector2.up * (bob * height * Mathf.Sin(now * 1.3f + i * 0.9f));
                bool snap = !placed || (lightPos[i] - target).sqrMagnitude > 9f; // first frame, respawns, teleports
                lightPos[i] = snap ? target : Vector2.Lerp(lightPos[i], target, k * (1f - 0.06f * slot)); // lower lights lag a touch more
                float a = colorAlpha * amount * breath * intensity;
                Place(lights[i], lightPos[i], z, colorSize * height * Mathf.Lerp(0.6f, 1f, amount), Tint(AllColors[i]), shown ? a : 0f);
                slot++;
            }
            placed = true;
        }

        static float Amount(ColorId c) => Game.Colors ? Game.Colors.GetAmount(c) : 0f;

        static Color Tint(ColorId c)
        {
            var data = Game.Config ? Game.Config.Get(c) : null;
            return data ? data.tint : Color.white;
        }

        static void Place(SpriteRenderer sr, Vector2 at, float z, float size, Color color, float alpha)
        {
            bool on = alpha > 0.003f && size > 0.0001f;
            if (sr.enabled != on) sr.enabled = on;
            if (!on) return;
            sr.transform.position = new Vector3(at.x, at.y, z);
            sr.transform.localScale = new Vector3(size, size, 1f);
            color.a = Mathf.Clamp01(alpha);
            sr.color = color;
        }

        SpriteRenderer MakeSprite(string name, int layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = soft;
            sr.sharedMaterial = glowMat;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>One soft round sprite (1 world unit across, no visible edge) and one additive material, shared by every glow.</summary>
        static void EnsureAssets()
        {
            if (!glowMat)
            {
                var shader = Resources.Load<Shader>("BlazeFx");
                glowMat = new Material(shader && shader.isSupported ? shader : Shader.Find("Sprites/Default"))
                    { name = "PlayerGlow", hideFlags = HideFlags.DontSave };
                glowMat.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.One); // additive: overlapping colors mix into light
            }
            if (soft) return;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { name = "PlayerGlow_Soft", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Exp(-d * d * 4f) * (1f - d * d); // gaussian, pulled to exactly 0 at the rim
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            soft = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            soft.name = "PlayerGlow_Soft";
            soft.hideFlags = HideFlags.DontSave;
        }
    }
}
