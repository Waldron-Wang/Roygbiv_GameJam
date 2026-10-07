using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Cinematic framing (IMGUI, UiKit look). It only LISTENS:
    ///   CinematicChanged(true / false)  black letterbox bars slide in / out at the top and bottom, a thin line in the
    ///                                   level's color along their inner edges, notches at its ends.
    ///   ScreenWipe(color, seconds)      a slanted band in that color sweeps across: it covers the screen by the halfway
    ///                                   point (when the level cuts to the next shot, with a bright flash), then uncovers.
    /// Drawn over the HUD (under the scene fade). Unscaled time.
    /// </summary>
    public class CinematicView : MonoBehaviour
    {
        [Tooltip("Letterbox bar height, as a fraction of the screen height.")]
        [SerializeField] float barHeight = 0.12f;
        [Tooltip("Seconds the bars take to slide in / out.")]
        [SerializeField] float barTime = 0.35f;
        [Tooltip("Angle of the wipe's leading edge.")]
        [SerializeField] float wipeAngle = 14f;

        float bars, barsTarget, wipeStart = -100f, wipeTime = 1f;
        Color wipeColor;

        void OnEnable()
        {
            GameEvents.CinematicChanged += OnCinematic;
            GameEvents.ScreenWipe += OnWipe;
            GameEvents.SceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.CinematicChanged -= OnCinematic;
            GameEvents.ScreenWipe -= OnWipe;
            GameEvents.SceneLoaded -= OnSceneLoaded;
        }

        void OnCinematic(bool playing) => barsTarget = playing ? 1f : 0f;

        void OnWipe(Color color, float seconds)
        {
            wipeColor = color;
            wipeTime = Mathf.Max(0.1f, seconds);
            wipeStart = Time.unscaledTime;
        }

        void OnSceneLoaded(string _)
        {
            bars = barsTarget = 0f;
            wipeStart = -100f;
        }

        void Update() => bars = Mathf.MoveTowards(bars, barsTarget, Time.unscaledDeltaTime / Mathf.Max(0.01f, barTime));

        void OnGUI()
        {
            GUI.depth = -500; // over the HUD, under the scene fade
            float w = Screen.width, h = Screen.height;

            float k = (Time.unscaledTime - wipeStart) / wipeTime;
            if (k >= 0f && k < 1f)
            {
                // Covering: the band's leading edge sweeps left -> right. Uncovering: its trailing edge follows.
                float span = w * 1.6f;
                float lead = k < 0.5f ? Mathf.SmoothStep(0f, 1f, k / 0.5f) : 1f;
                float trail = k < 0.5f ? 0f : Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f);
                float x0 = -w * 0.3f + trail * span, x1 = -w * 0.3f + lead * span;
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(wipeAngle, new Vector2(w * 0.5f, h * 0.5f));
                GUI.color = wipeColor;
                GUI.DrawTexture(new Rect(x0, -h, Mathf.Max(0f, x1 - x0), h * 3f), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(wipeColor, Color.white, 0.7f);
                if (k < 0.5f) GUI.DrawTexture(new Rect(x1 - 10f, -h, 10f, h * 3f), Texture2D.whiteTexture);   // bright leading edge
                else GUI.DrawTexture(new Rect(x0, -h, 10f, h * 3f), Texture2D.whiteTexture);
                GUI.matrix = old;
                float flash = 1f - Mathf.Abs(k - 0.5f) / 0.12f; // a white pop at the cut
                if (flash > 0f)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.6f * flash);
                    GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
                }
            }

            GUI.color = Color.white;
            if (bars > 0f) DrawBars(w, h);
        }

        /// <summary>The letterbox in the UiKit look: black bars, faint scanlines, a thin line in the level's color along
        /// their inner edges with notches at the ends.</summary>
        void DrawBars(float w, float h)
        {
            float b = h * barHeight * Mathf.SmoothStep(0f, 1f, bars);
            var accent = UiKit.CurrentAccent;
            float ui = UiKit.CanvasScale;
            var top = new Rect(0f, 0f, w, b);
            var bottom = new Rect(0f, h - b, w, b);
            CardGui.Alpha = 1f;
            foreach (var bar in new[] { top, bottom })
            {
                CardGui.Box(bar, Color.black);
                UiKit.Scanlines(bar, new Color(1f, 1f, 1f, 0.025f), 4f * ui, Mathf.Max(1f, ui));
            }
            float line = Mathf.Max(1f, 2f * ui), inset = 60f * ui, notch = 34f * ui;
            var edgeColor = UiKit.WithAlpha(accent, 0.75f * bars);
            CardGui.Box(new Rect(inset, top.yMax - line, w - inset * 2f, line), edgeColor);
            CardGui.Box(new Rect(inset, bottom.y, w - inset * 2f, line), edgeColor);
            UiKit.Notches(new Rect(inset, top.yMax - line, w - inset * 2f, bottom.y - top.yMax + line * 2f),
                          UiKit.WithAlpha(accent, 0.9f * bars), notch, Mathf.Max(2f, 4f * ui));
        }
    }
}
