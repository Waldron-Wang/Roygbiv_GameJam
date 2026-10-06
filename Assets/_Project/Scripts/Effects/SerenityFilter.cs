using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The calm look of Serenity on a camera: an indigo vignette, slightly washed-out colors and a faint indigo
    /// wash, scaled by Amount, plus a soft ripple ring that runs out from a point. Driven by SerenityView.
    /// Uses the ScreenWarp shader's color pass with its own material, so it stacks with a ScreenWarp on the same
    /// camera (Indigo's curses) instead of fighting over it. Animates on unscaled time: it's UI, not the world.
    /// Switches itself off (no render cost) while there's nothing to show.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SerenityFilter : MonoBehaviour
    {
        const string ShaderResource = "ScreenWarp";

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        static readonly int ShockId = Shader.PropertyToID("_Shock");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int WaveFreqId = Shader.PropertyToID("_WaveFreq");

        [Tooltip("Edge darkening at full strength. Alpha = how much.")]
        public Color vignette = new(0.1f, 0.06f, 0.32f, 0.6f);
        [Tooltip("Color wash at full strength. Alpha = how much.")]
        public Color tint = new(0.5f, 0.45f, 1f, 0.12f);
        [Tooltip("How washed-out the colors get at full strength.")]
        [Range(0f, 1f)] public float desaturate = 0.3f;

        Camera cam;
        Material material;
        Vector2 rippleCenter;
        float rippleStart = -10f, rippleTime = 1f, rippleStrength;

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

        void Awake()
        {
            cam = GetComponent<Camera>();
            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            else Debug.LogWarning("SerenityFilter: shader Resources/ScreenWarp is missing or unsupported; Serenity will have no screen look.", this);
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
        }

        float RippleLeft => Mathf.Clamp01(1f - (Time.unscaledTime - rippleStart) / rippleTime);

        void Update()
        {
            if (Amount <= 0.001f && RippleLeft <= 0f) enabled = false;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            float ripple = RippleLeft;
            if (!material || (Amount <= 0.001f && ripple <= 0f))
            {
                Graphics.Blit(src, dst);
                return;
            }
            float a = Mathf.Clamp01(Amount);
            material.SetColor(TintId, ToShader(new Color(tint.r, tint.g, tint.b, tint.a * a)));
            material.SetColor(VignetteId, ToShader(new Color(vignette.r, vignette.g, vignette.b, vignette.a * a)));
            material.SetFloat(DesaturateId, desaturate * a);
            material.SetVector(ShockId, new Vector4(rippleCenter.x, rippleCenter.y, (1f - ripple) * 1.4f, rippleStrength * ripple));
            material.SetFloat(AspectId, src.height > 0 ? (float)src.width / src.height : 1f);
            material.SetFloat(WaveFreqId, 14f);
            Graphics.Blit(src, dst, material, 0);
        }

        static Color ToShader(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
}
