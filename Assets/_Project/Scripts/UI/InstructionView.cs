using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The level's Tip button and how-to card (IMGUI). It only draws and reports clicks: InstructionRunner decides
    /// when the button may show (CanOpen), owns open / close and the frozen time, and raises InstructionShown /
    /// InstructionClosed, which this listens to.
    ///
    /// Tip button: a small "? Tip" pill in the top-right corner (under the debug GOD MODE label), in the level's
    /// accent, lit on hover. Clicking it toggles the card. Its rect (and the card's X) is registered with
    /// InputReader as a pointer blocker, so the click never reaches gameplay as an attack or a shot.
    ///
    /// Card: a dark holographic frame in the accent around a light "screen" playing the demo (InstructionDemos),
    /// the caption with keycaps, an X to close and a close hint. Laid out on a 1920x1080 canvas scaled to fit the
    /// screen, animated on unscaled time (the level is frozen underneath).
    /// To reskin: replace this, CardGui and InstructionDemos; InstructionRunner and the data stay.
    /// </summary>
    public class InstructionView : MonoBehaviour
    {
        const float RefWidth = 1920f, RefHeight = 1080f;
        const string Hint = "Click X or press [Z] / [Enter] to close";

        // Card layout on the 1920x1080 canvas, top to bottom.
        const float PanelWidth = 1120f, StageTop = 76f, StageHeight = 370f;
        const float CaptionGap = 54f;    // stage bottom -> caption center
        const float SubCaptionGap = 52f; // caption -> sub-caption center, only when there is one
        const float HintRow = 100f;      // last caption line -> panel bottom: room for the close hint
        const int CaptionSize = 34, SubCaptionSize = 25;
        // The hint reads at sub-caption size, with keycaps big enough for "Enter".
        const int HintSize = 25;
        const float HintKeyScale = 1.55f, HintLabelScale = 1.25f;

        // Tip button, in screen pixels at 1080p (scaled with the screen height).
        const float TipWidth = 112f, TipHeight = 42f, TipMargin = 14f;
        const float TipTop = 40f; // clear of the debug GOD MODE label in the same corner

        static readonly Color Ink = new(0.02f, 0.04f, 0.06f, 0.86f);
        static readonly Color Paper = new(0.93f, 0.93f, 0.91f, 0.97f);

        InstructionData data;
        ColorData colorData;
        float openedAt;

        // Screen space (IMGUI: origin top-left), as last laid out; InputReader hit-tests against these.
        Rect tipButton, closeButton;
        bool tipVisible, closeVisible;
        Func<Vector2, bool> pointerBlocker;

        void OnEnable()
        {
            GameEvents.InstructionShown += Show;
            GameEvents.InstructionClosed += Hide;
            pointerBlocker ??= IsOverButton;
            if (Game.Input) Game.Input.AddPointerBlocker(pointerBlocker);
        }

        void OnDisable()
        {
            GameEvents.InstructionShown -= Show;
            GameEvents.InstructionClosed -= Hide;
            if (Game.Input) Game.Input.RemovePointerBlocker(pointerBlocker);
        }

        void Show(ColorId color, InstructionData d)
        {
            data = d;
            colorData = Game.Config.Get(color);
            openedAt = Time.unscaledTime;
        }

        void Hide() => data = null;

        /// <summary>Input System screen position (origin bottom-left) over the Tip button or the card's X?</summary>
        bool IsOverButton(Vector2 screen)
        {
            var p = new Vector2(screen.x, Screen.height - screen.y);
            return (tipVisible && tipButton.Contains(p)) || (closeVisible && closeButton.Contains(p));
        }

        void OnGUI()
        {
            GUI.depth = -100; // over the HUD, under the scene fade (-1000)
            var runner = Game.Instructions;
            var e = Event.current;

            // Lay out on every event, so clicks and InputReader's hit test match what's on screen.
            bool open = runner.IsOpen && data;
            var card = open ? data : runner.LevelCard;
            tipVisible = card && (open || runner.CanOpen);
            closeVisible = open;
            if (!tipVisible) return;

            float ui = Mathf.Clamp(Screen.height / RefHeight, 0.6f, 2f);
            tipButton = new Rect(Screen.width - (TipWidth + TipMargin) * ui, TipTop, TipWidth * ui, TipHeight * ui);
            var canvas = CanvasMatrix();
            var panel = open ? PanelRect() : default;
            if (open) closeButton = ToScreen(canvas, CloseRect(panel));

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (tipButton.Contains(e.mousePosition)) { runner.Toggle(); e.Use(); }
                else if (open && closeButton.Contains(e.mousePosition)) { runner.Close(); e.Use(); }
                return;
            }
            if (e.type != EventType.Repaint) return;

            var accent = card.accent;
            accent.a = 1f;
            if (open) DrawCard(panel, canvas, accent, closeButton.Contains(e.mousePosition));
            DrawTipButton(tipButton, accent, open, tipButton.Contains(e.mousePosition), ui);
        }

        // ---------- Tip button ----------

        static void DrawTipButton(Rect r, Color accent, bool open, bool hover, float ui)
        {
            float radius = r.height * 0.5f;
            CardGui.Alpha = 1f;
            if (hover || open) CardGui.Glow(r.center, r.width * 0.8f, WithAlpha(accent, hover ? 0.4f : 0.25f));
            CardGui.Round(r, new Color(0.03f, 0.05f, 0.07f, hover ? 0.94f : 0.8f), radius);
            if (open || hover) CardGui.Round(r, WithAlpha(accent, open ? 0.3f : 0.15f), radius);
            CardGui.Outline(r, WithAlpha(accent, hover || open ? 1f : 0.7f), Mathf.Max(1.5f, 2f * ui), radius);

            var icon = new Vector2(r.x + radius, r.center.y);
            CardGui.Disc(icon, r.height * 0.31f, accent);
            CardGui.Text(new Rect(icon.x - radius, r.y, radius * 2f, r.height), "?", Mathf.RoundToInt(r.height * 0.44f), Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            CardGui.Text(new Rect(icon.x + radius * 0.6f, r.y, r.xMax - icon.x - radius * 0.6f - radius * 0.4f, r.height), "Tip",
                         Mathf.RoundToInt(r.height * 0.42f), hover || open ? Color.white : new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        // ---------- Card ----------

        void DrawCard(Rect panel, Matrix4x4 canvas, Color accent, bool closeHover)
        {
            float t = Time.unscaledTime - openedAt;
            float open = Smooth(t / 0.25f);

            CardGui.Alpha = 1f;
            CardGui.Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f * open));

            var screenMatrix = GUI.matrix;
            GUI.matrix = canvas;

            // The hologram unfolds from its middle line, then the content fades in.
            var frame = panel;
            float h = Mathf.Max(4f, frame.height * open);
            frame.y += (frame.height - h) * 0.5f;
            frame.height = h;
            DrawFrame(frame, accent, t);

            CardGui.Alpha = Smooth((t - 0.18f) / 0.2f);
            if (CardGui.Alpha > 0f) DrawContent(panel, accent, t - 0.18f, closeHover);

            CardGui.Alpha = 1f;
            GUI.matrix = screenMatrix;
        }

        static void DrawFrame(Rect p, Color accent, float t)
        {
            CardGui.Outline(Expand(p, 7f), WithAlpha(accent, 0.15f), 2f);
            CardGui.Box(p, Ink);
            CardGui.Box(p, WithAlpha(accent, 0.07f));
            for (float y = p.y + 3f; y < p.yMax; y += 6f)
                CardGui.Box(new Rect(p.x, y, p.width, 1.5f), WithAlpha(accent, 0.035f));

            // A faint band sweeping down, like a projector refreshing.
            float bandY = p.y + (Mathf.Repeat(t * 0.3f, 1.25f) - 0.1f) * p.height;
            float top = Mathf.Max(p.y, bandY), bottom = Mathf.Min(p.yMax, bandY + 70f);
            if (bottom > top) CardGui.Box(new Rect(p.x, top, p.width, bottom - top), WithAlpha(accent, 0.05f));

            CardGui.Outline(p, WithAlpha(accent, 0.9f), 2f);
            const float len = 38f, thick = 6f;
            Corner(new Vector2(p.x, p.y), 1f, 1f);
            Corner(new Vector2(p.xMax, p.y), -1f, 1f);
            Corner(new Vector2(p.x, p.yMax), 1f, -1f);
            Corner(new Vector2(p.xMax, p.yMax), -1f, -1f);

            // An L-bracket on corner `c`, its arms running inward along +-x and +-y.
            void Corner(Vector2 c, float sx, float sy)
            {
                float x = sx > 0f ? c.x - thick * 0.5f : c.x - len + thick * 0.5f;
                float y = sy > 0f ? c.y - thick * 0.5f : c.y - len + thick * 0.5f;
                CardGui.Box(new Rect(x, c.y - thick * 0.5f, len, thick), accent);
                CardGui.Box(new Rect(c.x - thick * 0.5f, y, thick, len), accent);
            }
        }

        void DrawContent(Rect p, Color accent, float t, bool closeHover)
        {
            // Header: tab and the spaced-out color name; the X on the right.
            var header = new Rect(p.x + 2f, p.y + 2f, p.width - 4f, 46f);
            CardGui.Box(header, WithAlpha(accent, 0.14f));
            CardGui.Box(new Rect(p.x, header.yMax, p.width, 2f), WithAlpha(accent, 0.6f));
            CardGui.Box(new Rect(p.x + 22f, header.y + 13f, 8f, 20f), accent);
            string title = Spaced((colorData ? colorData.displayName : data.name).ToUpperInvariant());
            CardGui.Text(new Rect(p.x + 44f, header.y, 500f, header.height), title, 24, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            DrawClose(CloseRect(p), accent, closeHover);

            // The screen the demo plays on: light, because the player art is a black silhouette.
            var stage = new Rect(p.x + 48f, p.y + StageTop, p.width - 96f, StageHeight);
            CardGui.Box(stage, Paper);
            var grid = new Color(0f, 0f, 0f, 0.045f);
            for (float x = stage.x + 40f; x < stage.xMax; x += 40f) CardGui.Box(new Rect(x, stage.y, 1f, stage.height), grid);
            for (float y = stage.y + 40f; y < stage.yMax; y += 40f) CardGui.Box(new Rect(stage.x, y, stage.width, 1f), grid);
            InstructionDemos.Draw(data, stage, t);
            CardGui.Outline(stage, WithAlpha(accent, 0.55f), 2f);

            // Caption (+ optional small line); its keycaps press in sync with the demo's.
            float captionY = stage.yMax + CaptionGap, maxWidth = p.width - 80f;
            CardGui.Inline(data.caption, new Vector2(p.center.x, captionY), FitFont(data.caption, CaptionSize, maxWidth), Color.white, accent, InstructionDemos.Press);
            if (HasSubCaption)
                CardGui.Inline(data.subCaption, new Vector2(p.center.x, captionY + SubCaptionGap), FitFont(data.subCaption, SubCaptionSize, maxWidth), new Color(1f, 1f, 1f, 0.78f), accent, InstructionDemos.Press);

            // Close hint, bottom right, once the keys are accepted (InstructionRunner.minShowTime). A gentle blink.
            float alpha = CardGui.Alpha;
            CardGui.Alpha *= Smooth((t - 0.25f) / 0.3f) * (0.75f + 0.25f * Mathf.Sin(t * 4f));
            float width = CardGui.InlineWidth(Hint, HintSize, HintKeyScale, HintLabelScale);
            CardGui.Inline(Hint, new Vector2(p.xMax - 34f - width * 0.5f, p.yMax - 40f), HintSize, new Color(1f, 1f, 1f, 0.88f), accent, null,
                           true, HintKeyScale, HintLabelScale);
            CardGui.Alpha = alpha;
        }

        /// <summary>The card's X, at the right end of the header (canvas units).</summary>
        static Rect CloseRect(Rect panel) => new(panel.xMax - 58f, panel.y + 8f, 36f, 34f);

        static void DrawClose(Rect r, Color accent, bool hover)
        {
            CardGui.Round(r, hover ? WithAlpha(accent, 0.35f) : new Color(0f, 0f, 0f, 0.35f), 6f);
            CardGui.Outline(r, WithAlpha(accent, hover ? 1f : 0.75f), 2f, 6f);
            float arm = r.height * 0.24f;
            var c = r.center;
            var color = hover ? Color.white : accent;
            CardGui.Line(c + new Vector2(-arm, -arm), c + new Vector2(arm, arm), 3.5f, color);
            CardGui.Line(c + new Vector2(-arm, arm), c + new Vector2(arm, -arm), 3.5f, color);
        }

        bool HasSubCaption => !string.IsNullOrEmpty(data.subCaption);

        /// <summary>Centered, and only as tall as its content: no empty gap where a missing sub-caption would go.</summary>
        Rect PanelRect()
        {
            float h = StageTop + StageHeight + CaptionGap + (HasSubCaption ? SubCaptionGap : 0f) + HintRow;
            return new Rect((RefWidth - PanelWidth) * 0.5f, (RefHeight - h) * 0.5f - 10f, PanelWidth, h);
        }

        /// <summary>The 1920x1080 canvas, scaled to fit and centered on the screen.</summary>
        static Matrix4x4 CanvasMatrix()
        {
            float s = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            return Matrix4x4.TRS(new Vector3((Screen.width - RefWidth * s) * 0.5f, (Screen.height - RefHeight * s) * 0.5f, 0f),
                                 Quaternion.identity, new Vector3(s, s, 1f));
        }

        static Rect ToScreen(Matrix4x4 m, Rect r)
        {
            Vector2 min = m.MultiplyPoint3x4(r.min), max = m.MultiplyPoint3x4(r.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Shrinks a caption that's too long for the card instead of letting it spill out.</summary>
        static int FitFont(string line, int size, float maxWidth)
        {
            while (size > 18 && CardGui.InlineWidth(line, size) > maxWidth) size -= 2;
            return size;
        }

        static string Spaced(string s) => string.Join(" ", s.ToCharArray());
        static Rect Expand(Rect r, float by) => new(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
