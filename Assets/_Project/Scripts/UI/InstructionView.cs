using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The pre-boss instruction card (IMGUI). Listens to InstructionShown / InstructionClosed only.
    /// A dark holographic frame in the color's accent around a light "screen" that plays the demo
    /// (InstructionDemos), then the caption with keycaps and a continue hint. Laid out on a 1920x1080
    /// canvas scaled to fit the screen, animated on unscaled time (the level is frozen underneath).
    /// To reskin: replace this, CardGui and InstructionDemos; InstructionRunner and the data stay.
    /// </summary>
    public class InstructionView : MonoBehaviour
    {
        const float RefWidth = 1920f, RefHeight = 1080f;
        const string Hint = "Press [Z] / [Enter] to continue";

        // Card layout on the 1920x1080 canvas, top to bottom.
        const float PanelWidth = 1120f, StageTop = 76f, StageHeight = 370f;
        const float CaptionGap = 54f;    // stage bottom -> caption center
        const float SubCaptionGap = 52f; // caption -> sub-caption center, only when there is one
        const float HintRow = 100f;      // last caption line -> panel bottom: room for the continue hint
        const int CaptionSize = 34, SubCaptionSize = 25;
        // The hint reads at sub-caption size, with keycaps big enough for "Enter".
        const int HintSize = 25;
        const float HintKeyScale = 1.55f, HintLabelScale = 1.25f;

        static readonly Color Ink = new(0.02f, 0.04f, 0.06f, 0.86f);
        static readonly Color Paper = new(0.93f, 0.93f, 0.91f, 0.97f);

        InstructionData data;
        ColorData colorData;
        float openedAt;

        void OnEnable()
        {
            GameEvents.InstructionShown += Show;
            GameEvents.InstructionClosed += Hide;
        }

        void OnDisable()
        {
            GameEvents.InstructionShown -= Show;
            GameEvents.InstructionClosed -= Hide;
        }

        void Show(ColorId color, InstructionData d)
        {
            data = d;
            colorData = Game.Config.Get(color);
            openedAt = Time.unscaledTime;
        }

        void Hide() => data = null;

        void OnGUI()
        {
            GUI.depth = -100; // over the HUD, under the scene fade (-1000)
            if (!data || Event.current.type != EventType.Repaint) return;

            float t = Time.unscaledTime - openedAt;
            float open = Smooth(t / 0.25f);
            var accent = data.accent;
            accent.a = 1f;

            var screenMatrix = GUI.matrix;
            CardGui.Alpha = 1f;
            CardGui.Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f * open));

            float s = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            GUI.matrix = screenMatrix * Matrix4x4.TRS(
                new Vector3((Screen.width - RefWidth * s) * 0.5f, (Screen.height - RefHeight * s) * 0.5f, 0f),
                Quaternion.identity, new Vector3(s, s, 1f));

            // The hologram unfolds from its middle line, then the content fades in.
            var panel = PanelRect();
            var frame = panel;
            float h = Mathf.Max(4f, frame.height * open);
            frame.y += (frame.height - h) * 0.5f;
            frame.height = h;
            DrawFrame(frame, accent, t);

            CardGui.Alpha = Smooth((t - 0.18f) / 0.2f);
            if (CardGui.Alpha > 0f) DrawContent(panel, accent, t - 0.18f);

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

        void DrawContent(Rect p, Color accent, float t)
        {
            // Header: tab, spaced-out color name and its feeling.
            var header = new Rect(p.x + 2f, p.y + 2f, p.width - 4f, 46f);
            CardGui.Box(header, WithAlpha(accent, 0.14f));
            CardGui.Box(new Rect(p.x, header.yMax, p.width, 2f), WithAlpha(accent, 0.6f));
            CardGui.Box(new Rect(p.x + 22f, header.y + 13f, 8f, 20f), accent);

            string title = Spaced((colorData ? colorData.displayName : data.name).ToUpperInvariant());
            CardGui.Text(new Rect(p.x + 44f, header.y, 500f, header.height), title, 24, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (colorData && !string.IsNullOrEmpty(colorData.emotion))
            {
                float x = p.x + 44f + CardGui.Measure(title, 24, FontStyle.Bold).x + 26f;
                CardGui.Text(new Rect(x, header.y, 600f, header.height), colorData.emotion.ToUpperInvariant(), 16, WithAlpha(accent, 0.65f), TextAnchor.MiddleLeft);
            }

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

            // Continue hint, bottom right, once confirm is accepted (InstructionRunner.minShowTime). A gentle blink.
            float alpha = CardGui.Alpha;
            CardGui.Alpha *= Smooth((t - 0.25f) / 0.3f) * (0.75f + 0.25f * Mathf.Sin(t * 4f));
            float width = CardGui.InlineWidth(Hint, HintSize, HintKeyScale, HintLabelScale);
            CardGui.Inline(Hint, new Vector2(p.xMax - 34f - width * 0.5f, p.yMax - 40f), HintSize, new Color(1f, 1f, 1f, 0.88f), accent, null,
                           true, HintKeyScale, HintLabelScale);
            CardGui.Alpha = alpha;
        }

        bool HasSubCaption => !string.IsNullOrEmpty(data.subCaption);

        /// <summary>Centered, and only as tall as its content: no empty gap where a missing sub-caption would go.</summary>
        Rect PanelRect()
        {
            float h = StageTop + StageHeight + CaptionGap + (HasSubCaption ? SubCaptionGap : 0f) + HintRow;
            return new Rect((RefWidth - PanelWidth) * 0.5f, (RefHeight - h) * 0.5f - 10f, PanelWidth, h);
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
