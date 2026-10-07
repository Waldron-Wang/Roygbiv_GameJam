using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Serenity's screen look on a camera (shader Resources/SerenityFilter): a clear indigo wash and a slight
    /// desaturation over everything EXCEPT a soft ellipse around the player (they stay in full color), an indigo
    /// vignette, plus one-shot flashes and a ripple ring that pushes the picture outward. Driven by SerenityView.
    /// Its own material, so it stacks with a ScreenWarp on the same camera (Indigo's curses) instead of fighting it.
    /// Animates on unscaled time (it's UI, not the world) and switches itself off when there's nothing to show.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SerenityFilter : MonoBehaviour
    {
        const string ShaderResource = "SerenityFilter";

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int ShockId = Shader.PropertyToID("_Shock");
        static readonly int KeepId = Shader.PropertyToID("_Keep");
        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        [Tooltip("Indigo wash at full strength. Alpha = how much.")]
        public Color tint = new(0.29f, 0.17f, 1f, 0.55f);
        [Tooltip("Edge darkening at full strength. Alpha = how much.")]
        public Color vignette = new(0.12f, 0.06f, 0.4f, 0.7f);
        [Tooltip("How washed-out everything but the player gets at full strength.")]
        [Range(0f, 1f)] public float desaturate = 0.35f;
        [Tooltip("The player's own colors survive inside this radius (x their sprite's size), fading out over the soft edge.")]
        public Vector2 keepRadiusAndSoftness = new(1.25f, 0.9f);

        Camera cam;
        Material material;
        Vector2 rippleCenter;
        float rippleStart = -10f, rippleTime = 1f, rippleStrength;
        Color flashColor;
        float flashStart = -10f, flashTime = 0.2f;

        /// <summary>0 = untouched, 1 = the full look.</summary>
        public float Amount { get; set; }

        public static SerenityFilter On(Camera camera)
        {
            if (!camera) return null;
            return camera.TryGetComponent(out SerenityFilter f) ? f : camera.gameObject.AddComponent<SerenityFilter>();
        }

        /// <summary>A ring that pushes the picture outward runs from a world point (unscaled seconds).</summary>
        public void Ripple(Vector2 worldPoint, float strength, float seconds)
        {
            if (!cam) cam = GetComponent<Camera>();
            rippleCenter = cam.WorldToViewportPoint(worldPoint);
            rippleStart = Time.unscaledTime;
            rippleTime = Mathf.Max(0.05f, seconds);
            rippleStrength = strength;
            enabled = true;
        }

        /// <summary>The screen flashes `color` (alpha = strength) and fades back over `seconds` (unscaled).</summary>
        public void Flash(Color color, float seconds)
        {
            flashColor = color;
            flashStart = Time.unscaledTime;
            flashTime = Mathf.Max(0.02f, seconds);
            enabled = true;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            else Debug.LogWarning("SerenityFilter: shader Resources/SerenityFilter is missing or unsupported; Serenity will have no screen look.", this);
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
        }

        static float Left(float start, float duration) => Mathf.Clamp01(1f - (Time.unscaledTime - start) / duration);

        void Update()
        {
            if (Amount <= 0.001f && Left(rippleStart, rippleTime) <= 0f && Left(flashStart, flashTime) <= 0f) enabled = false;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            float ripple = Left(rippleStart, rippleTime), flash = Left(flashStart, flashTime);
            if (!material || (Amount <= 0.001f && ripple <= 0f && flash <= 0f))
            {
                Graphics.Blit(src, dst);
                return;
            }

            float a = Mathf.Clamp01(Amount);
            material.SetColor(TintId, ToShader(new Color(tint.r, tint.g, tint.b, tint.a * a)));
            material.SetColor(VignetteId, ToShader(new Color(vignette.r, vignette.g, vignette.b, vignette.a * a)));
            material.SetFloat(DesaturateId, desaturate * a);
            material.SetColor(FlashId, ToShader(new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * flash * flash)));
            material.SetVector(ShockId, new Vector4(rippleCenter.x, rippleCenter.y, (1f - ripple) * 1.5f, rippleStrength * ripple));
            material.SetVector(KeepId, PlayerKeep());
            material.SetFloat(AspectId, src.height > 0 ? (float)src.width / src.height : 1f);
            Graphics.Blit(src, dst, material, 0);
        }

        /// <summary>Where the player is on screen and how big, in screen heights: the area that keeps its colors.</summary>
        Vector4 PlayerKeep()
        {
            var pc = PlayerController.Instance;
            if (!pc || !cam) return new Vector4(-10f, -10f, 0f, 0.01f);
            var center = pc.transform.position;
            float height = 1.2f;
            var sprite = pc.GetComponentInChildren<SpriteRenderer>();
            if (sprite) { center = sprite.bounds.center; height = Mathf.Max(0.5f, sprite.bounds.size.y); }
            var vp = cam.WorldToViewportPoint(center);
            float perUnit = 1f / Mathf.Max(0.01f, cam.orthographicSize * 2f); // world units -> screen heights
            return new Vector4(vp.x, vp.y, height * 0.5f * keepRadiusAndSoftness.x * perUnit, height * keepRadiusAndSoftness.y * perUnit);
        }

        static Color ToShader(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
}
