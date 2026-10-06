using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// What a ScreenWarp does to the picture. Every field at zero leaves it untouched, so default(WarpLook)
    /// is "clear" and two looks blend field by field. Tune them in the Inspector wherever one is exposed
    /// (the Indigo boss has one per curse).
    /// </summary>
    [Serializable]
    public struct WarpLook
    {
        [Header("Color")]
        [Tooltip("Washes the picture toward this color. Alpha = how much.")]
        public Color tint;
        [Range(0f, 1f)] public float desaturate;
        [Tooltip("Turns every hue, in turns: 0.5 = blues become yellows, reds become cyans.")]
        [Range(-1f, 1f)] public float hueShift;
        [Tooltip("Keeps turning the hues, in turns per second.")]
        public float hueCycle;
        [Tooltip("Photo negative.")]
        [Range(0f, 1f)] public float invert;
        [Tooltip("Darkens the edges toward this color. Alpha = how much.")]
        public Color vignette;

        [Header("Distortion")]
        [Tooltip("Red / blue split, in screen widths.")]
        [Range(0f, 0.06f)] public float chromatic;
        [Tooltip("Seasick wobble, in screen widths.")]
        [Range(0f, 0.05f)] public float wave;
        [Tooltip("Wobbles across the screen.")]
        public float waveFrequency;
        [Tooltip("A left-right mirrored copy of the screen bleeds through.")]
        [Range(0f, 0.6f)] public float mirrorGhost;
        [Tooltip("Horizontal slices that jump sideways.")]
        [Range(0f, 1f)] public float glitch;
        [Tooltip("The picture twitches every frame, in screen widths.")]
        [Range(0f, 0.02f)] public float jitter;
        [Tooltip("Motion trails: how much of the last frame stays on screen.")]
        [Range(0f, 0.95f)] public float smear;
        [Tooltip("The trails zoom inward every frame: a tunnel of echoes.")]
        [Range(0f, 0.05f)] public float smearZoom;

        [Header("Camera")]
        [Tooltip("Held camera roll, in degrees. 180 = the world upside down.")]
        public float roll;
        [Tooltip("Rocking camera roll, in degrees either way.")]
        public float sway;
        [Tooltip("Rocks per second.")]
        public float swaySpeed;
        [Tooltip("Breathing zoom, as a fraction of the view size.")]
        [Range(0f, 0.3f)] public float zoomPulse;

        public static WarpLook Lerp(in WarpLook a, in WarpLook b, float t) => new()
        {
            tint = Color.Lerp(a.tint, b.tint, t),
            desaturate = Mathf.Lerp(a.desaturate, b.desaturate, t),
            hueShift = Mathf.Lerp(a.hueShift, b.hueShift, t),
            hueCycle = Mathf.Lerp(a.hueCycle, b.hueCycle, t),
            invert = Mathf.Lerp(a.invert, b.invert, t),
            vignette = Color.Lerp(a.vignette, b.vignette, t),
            chromatic = Mathf.Lerp(a.chromatic, b.chromatic, t),
            wave = Mathf.Lerp(a.wave, b.wave, t),
            // A look with no wave keeps the other's frequency, so fading a wave in or out doesn't also scrub through frequencies.
            waveFrequency = a.wave <= 0f ? b.waveFrequency : b.wave <= 0f ? a.waveFrequency : Mathf.Lerp(a.waveFrequency, b.waveFrequency, t),
            mirrorGhost = Mathf.Lerp(a.mirrorGhost, b.mirrorGhost, t),
            glitch = Mathf.Lerp(a.glitch, b.glitch, t),
            jitter = Mathf.Lerp(a.jitter, b.jitter, t),
            smear = Mathf.Lerp(a.smear, b.smear, t),
            smearZoom = Mathf.Lerp(a.smearZoom, b.smearZoom, t),
            roll = Mathf.Lerp(a.roll, b.roll, t),
            sway = Mathf.Lerp(a.sway, b.sway, t),
            swaySpeed = Mathf.Lerp(a.swaySpeed, b.swaySpeed, t),
            zoomPulse = Mathf.Lerp(a.zoomPulse, b.zoomPulse, t),
        };
    }

    /// <summary>
    /// A full-screen disorientation effect on a camera: color washes, hue swaps, wobble, glitch slices,
    /// mirrored ghosts, motion trails, camera roll and breathing zoom, plus one-shot flashes, shockwave
    /// rings and pulses. Built for Indigo's curses, usable by any boss or level:
    ///
    ///   ScreenWarp.Main.BlendTo(look, 0.5f);   // ease into a look
    ///   ScreenWarp.Main.Clear(0.3f);           // ease back to a clean picture
    ///   ScreenWarp.Main.Shockwave(worldPos);   // one-shots stack on top of the look
    ///
    /// Added to the camera on demand. Roll and zoom move the real camera, so mouse aim stays true;
    /// the rest is a post effect (built-in pipeline OnRenderImage, shader Resources/ScreenWarp).
    /// IMGUI (HUD, dialogue, Tip card) draws after it and stays readable. Runs on scaled time, so pause freezes it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenWarp : MonoBehaviour
    {
        const string ShaderResource = "ScreenWarp";

        static readonly int HistoryId = Shader.PropertyToID("_HistoryTex");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int ShockId = Shader.PropertyToID("_Shock");
        static readonly int HueId = Shader.PropertyToID("_Hue");
        static readonly int InvertId = Shader.PropertyToID("_Invert");
        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        static readonly int ChromaId = Shader.PropertyToID("_Chroma");
        static readonly int WaveId = Shader.PropertyToID("_Wave");
        static readonly int WaveFreqId = Shader.PropertyToID("_WaveFreq");
        static readonly int MirrorId = Shader.PropertyToID("_Mirror");
        static readonly int GlitchId = Shader.PropertyToID("_Glitch");
        static readonly int JitterId = Shader.PropertyToID("_Jitter");
        static readonly int SmearId = Shader.PropertyToID("_Smear");
        static readonly int SmearZoomId = Shader.PropertyToID("_SmearZoom");
        static readonly int TimeId = Shader.PropertyToID("_T");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        Camera cam;
        Material material;
        RenderTexture history;
        Quaternion baseRotation;
        float baseSize;

        WarpLook from, to, current;
        float blendStart, blendTime;
        float huePhase, swayPhase, zoomPhase;

        Color flashColor;
        float flashStart, flashTime;
        Vector2 shockCenter;
        float shockStart, shockTime, shockStrength;
        float pulseStart, pulseTime, pulseStrength;

        /// <summary>The look on screen right now (mid-blend included), without one-shots.</summary>
        public WarpLook Current => current;

        /// <summary>The camera's warp, added if it has none. Null if there's no camera.</summary>
        public static ScreenWarp On(Camera camera)
        {
            if (!camera) return null;
            return camera.TryGetComponent(out ScreenWarp warp) ? warp : camera.gameObject.AddComponent<ScreenWarp>();
        }

        /// <summary>The main camera's warp, added if it has none.</summary>
        public static ScreenWarp Main => On(Camera.main);

        /// <summary>Eases from whatever is on screen to `look` over `seconds` (0 = snap).</summary>
        public void BlendTo(in WarpLook look, float seconds)
        {
            from = current;
            to = look;
            blendStart = Time.time;
            blendTime = Mathf.Max(0f, seconds);
            if (blendTime <= 0f) current = to;
        }

        /// <summary>Eases back to an untouched picture.</summary>
        public void Clear(float seconds) => BlendTo(default, seconds);

        /// <summary>The screen flashes `color` (alpha = strength) and fades back over `seconds`.</summary>
        public void Flash(Color color, float seconds)
        {
            flashColor = color;
            flashStart = Time.time;
            flashTime = Mathf.Max(0.01f, seconds);
        }

        /// <summary>A ring that pushes the picture outward expands from a world point.</summary>
        public void Shockwave(Vector2 worldPoint, float strength = 1f, float seconds = 0.8f)
        {
            if (!cam) return;
            shockCenter = cam.WorldToViewportPoint(worldPoint);
            shockStart = Time.time;
            shockTime = Mathf.Max(0.01f, seconds);
            shockStrength = strength;
        }

        /// <summary>A short burst of extra split and wobble on top of the look (impacts, spell casts).</summary>
        public void Pulse(float strength = 1f, float seconds = 0.35f)
        {
            pulseStart = Time.time;
            pulseTime = Mathf.Max(0.01f, seconds);
            pulseStrength = strength;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseRotation = transform.localRotation;
            baseSize = cam.orthographicSize;

            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            else Debug.LogWarning("ScreenWarp: shader Resources/ScreenWarp is missing or unsupported; only roll and zoom will work.", this);
        }

        void OnDisable()
        {
            transform.localRotation = baseRotation;
            if (cam) cam.orthographicSize = baseSize;
            ReleaseHistory();
        }

        void OnDestroy()
        {
            ReleaseHistory();
            if (material) Destroy(material);
        }

        void Update()
        {
            float k = blendTime > 0f ? Mathf.Clamp01((Time.time - blendStart) / blendTime) : 1f;
            current = WarpLook.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));

            // Phases accumulate, so changing a speed mid-blend never makes the picture jump.
            float dt = Time.deltaTime;
            huePhase = Mathf.Repeat(huePhase + current.hueCycle * dt, 1f);
            if (Mathf.Abs(current.hueCycle) < 0.01f) huePhase = Mathf.MoveTowards(huePhase, Mathf.Round(huePhase), dt); // settle back to no turn
            swayPhase = Mathf.Repeat(swayPhase + current.swaySpeed * dt * 2f * Mathf.PI, 2f * Mathf.PI);
            zoomPhase = Mathf.Repeat(zoomPhase + 0.35f * dt * 2f * Mathf.PI, 2f * Mathf.PI);
        }

        void LateUpdate()
        {
            float roll = current.roll + Mathf.Sin(swayPhase) * current.sway;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
            cam.orthographicSize = baseSize * (1f + Mathf.Sin(zoomPhase) * current.zoomPulse);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            float pulse = OneShot(pulseStart, pulseTime) * pulseStrength;
            float flash = OneShot(flashStart, flashTime);
            float shock = OneShot(shockStart, shockTime);
            var look = current;

            if (!material || (IsClear(look) && pulse <= 0f && flash <= 0f && shock <= 0f))
            {
                ReleaseHistory();
                Graphics.Blit(src, dst);
                return;
            }

            material.SetColor(TintId, ToShader(look.tint));
            material.SetColor(VignetteId, ToShader(look.vignette));
            material.SetColor(FlashId, ToShader(new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * flash * flash)));
            float shockAge = 1f - shock;
            material.SetVector(ShockId, new Vector4(shockCenter.x, shockCenter.y, shockAge * 1.6f, shockStrength * shock));
            material.SetFloat(HueId, look.hueShift + huePhase);
            material.SetFloat(InvertId, look.invert);
            material.SetFloat(DesaturateId, look.desaturate);
            material.SetFloat(ChromaId, look.chromatic + 0.025f * pulse);
            material.SetFloat(WaveId, look.wave + 0.012f * pulse);
            material.SetFloat(WaveFreqId, look.waveFrequency > 0f ? look.waveFrequency : 14f);
            material.SetFloat(MirrorId, look.mirrorGhost);
            material.SetFloat(GlitchId, Mathf.Clamp01(look.glitch + 0.4f * pulse));
            material.SetFloat(JitterId, look.jitter + 0.004f * pulse);
            material.SetFloat(TimeId, Time.time);
            material.SetFloat(SeedId, UnityEngine.Random.value * 100f);
            material.SetFloat(AspectId, src.height > 0 ? (float)src.width / src.height : 1f);

            if (look.smear <= 0.001f)
            {
                ReleaseHistory();
                Graphics.Blit(src, dst, material, 0);
                return;
            }

            var warped = RenderTexture.GetTemporary(src.descriptor);
            Graphics.Blit(src, warped, material, 0);

            bool fresh = EnsureHistory(src);
            if (fresh) Graphics.Blit(warped, history);
            material.SetTexture(HistoryId, history);
            material.SetFloat(SmearId, look.smear);
            material.SetFloat(SmearZoomId, look.smearZoom);

            var trailed = RenderTexture.GetTemporary(src.descriptor);
            Graphics.Blit(warped, trailed, material, 1);
            Graphics.Blit(trailed, history);
            Graphics.Blit(trailed, dst);

            RenderTexture.ReleaseTemporary(warped);
            RenderTexture.ReleaseTemporary(trailed);
        }

        /// <summary>Colors are picked in the Inspector (sRGB); the image the shader works on is linear in a Linear project.</summary>
        static Color ToShader(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        /// <summary>1 when a one-shot starts, easing to 0 at its end.</summary>
        static float OneShot(float start, float duration)
        {
            if (duration <= 0f) return 0f;
            float t = (Time.time - start) / duration;
            return t >= 0f && t < 1f ? 1f - t : 0f;
        }

        static bool IsClear(in WarpLook l) =>
            l.tint.a <= 0.001f && l.vignette.a <= 0.001f && l.desaturate <= 0.001f && l.invert <= 0.001f &&
            Mathf.Abs(l.hueShift) <= 0.001f && Mathf.Abs(l.hueCycle) <= 0.001f && l.chromatic <= 0.0001f &&
            l.wave <= 0.0001f && l.mirrorGhost <= 0.001f && l.glitch <= 0.001f && l.jitter <= 0.0001f && l.smear <= 0.001f;

        bool EnsureHistory(RenderTexture src)
        {
            if (history && history.width == src.width && history.height == src.height) return false;
            ReleaseHistory();
            history = new RenderTexture(src.descriptor) { name = "ScreenWarp History" };
            return true;
        }

        void ReleaseHistory()
        {
            if (!history) return;
            history.Release();
            Destroy(history);
            history = null;
        }
    }
}
