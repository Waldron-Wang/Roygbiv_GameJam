using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The level's Tip button and how-to card (IMGUI). It only draws and reports clicks: InstructionRunner decides
    /// when the button may show (CanOpen), owns open / close and the frozen time, and raises InstructionShown /
    /// InstructionClosed, which this listens to.
    ///
    /// Tip button: a small, quiet gray "? Tip" pill in the top-right corner,
    /// half see-through until hovered. Clicking it toggles the card. Its rect (and the card's X) is registered with
    /// InputReader as a pointer blocker, so the click never reaches gameplay as an attack or a shot.
    ///
    /// Card: the UiKit panel and header in the accent around a light "screen" playing the demo (InstructionDemos),
    /// the caption with keycaps, an X to close and a close hint. At the Nudge stage (InstructionRunner.Stage) it's a
    /// smaller card with only the nudge line and how many more tries until the full one. While the button offers
    /// something new (HasUnread) it carries a small glowing dot in the level's color. Laid out on UiKit's 1920x1080 canvas (Fit),
    /// animated on unscaled time (the level is frozen underneath).
    /// To reskin: change UiKit (every screen follows), or this, CardGui and InstructionDemos; InstructionRunner and the data stay.
    /// </summary>
    public class InstructionView : MonoBehaviour
    {
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
        const float TipTop = 10f;
        const float NudgeBody = 150f; // header -> hint row on a nudge card

        static readonly Color Paper = new(0.93f, 0.93f, 0.91f, 0.97f);

        InstructionData data;
        ColorData colorData;
        float openedAt;
        bool nudgeOnly;

        // Screen space (IMGUI: origin top-left), as last laid out; InputReader hit-tests against these.
        Rect tipButton, closeButton;
        bool tipVisible, closeVisible;
        Func<Vector2, bool> pointerBlocker;

        void OnEnable()
        {
            GameEvents.InstructionShown += Show;
            GameEvents.InstructionClosed += Hide;
            RebindPointerBlocker();
        }

        internal void RebindPointerBlocker()
        {
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
            nudgeOnly = Game.Instructions && Game.Instructions.OpenStage == InstructionRunner.TipStage.Nudge;
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
            if (!runner)
            {
                tipVisible = closeVisible = false;
                return;
            }
            var e = Event.current;

            // Lay out on every event, so clicks and InputReader's hit test match what's on screen.
            bool open = runner.IsOpen && data;
            var card = open ? data : runner.LevelCard;
            tipVisible = card && (open || runner.CanOpen);
            closeVisible = open;
            if (!tipVisible) return;

            float ui = Mathf.Clamp(Screen.height / UiKit.RefHeight, 0.6f, 2f);
            tipButton = new Rect(Screen.width - (TipWidth + TipMargin) * ui, TipTop, TipWidth * ui, TipHeight * ui);
            var canvas = UiKit.Fit().Matrix;
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
            DrawTipButton(tipButton, open || tipButton.Contains(e.mousePosition), ui);
            if (!open && runner.HasUnread) DrawUnread(tipButton, accent);
        }

        /// <summary>New help to read: a small dot in the level's color on the button's corner, breathing softly.</summary>
        static void DrawUnread(Rect button, Color accent)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            var c = new Vector2(button.xMax - button.height * 0.18f, button.y + button.height * 0.18f);
            float r = button.height * 0.16f;
            CardGui.Glow(c, r * (3f + pulse), WithAlpha(accent, 0.35f + 0.25f * pulse));
            CardGui.Disc(c, r, accent);
        }

        // ---------- Tip button ----------

        /// <summary>
        /// Neutral and quiet, so it doesn't draw the eye: all gray, about half see-through while idle; on hover or
        /// while the card is open, fully opaque and a little brighter. No color, no glow, nothing moving.
        /// The whole pill is one hover target with one look: the "?" disc only brightens a touch with the rest,
        /// and its glyph stays dark, so it reads the same in both states.
        /// </summary>
        static void DrawTipButton(Rect r, bool active, float ui)
        {
            float radius = r.height * 0.5f;
            CardGui.Alpha = active ? 1f : 0.55f;
            CardGui.Round(r, active ? new Color(0.22f, 0.22f, 0.23f, 0.92f) : new Color(0.14f, 0.14f, 0.15f, 0.88f), radius);
            CardGui.Outline(r, active ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.52f, 0.52f, 0.52f), Mathf.Max(1f, 1.5f * ui), radius);

            var icon = new Vector2(r.x + radius, r.center.y);
            CardGui.Disc(icon, r.height * 0.3f, active ? new Color(0.66f, 0.66f, 0.66f) : new Color(0.58f, 0.58f, 0.58f));
            CardGui.Text(new Rect(icon.x - radius, r.y, radius * 2f, r.height), "?", Mathf.RoundToInt(r.height * 0.44f),
                         new Color(0.13f, 0.13f, 0.14f), TextAnchor.MiddleCenter, FontStyle.Bold);
            CardGui.Text(new Rect(icon.x + radius * 0.6f, r.y, r.xMax - icon.x - radius * 0.6f - radius * 0.4f, r.height), "Tip",
                         Mathf.RoundToInt(r.height * 0.42f), active ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.68f, 0.68f, 0.68f),
                         TextAnchor.MiddleCenter, FontStyle.Bold);
            CardGui.Alpha = 1f;
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
            UiKit.Panel(frame, accent, t);

            CardGui.Alpha = Smooth((t - 0.18f) / 0.2f);
            if (CardGui.Alpha > 0f) DrawContent(panel, accent, t - 0.18f, closeHover);

            CardGui.Alpha = 1f;
            GUI.matrix = screenMatrix;
        }

        void DrawContent(Rect p, Color accent, float t, bool closeHover)
        {
            // Header: tab and the spaced-out color name; the X on the right.
            UiKit.Header(p, colorData ? colorData.displayName : data.name, accent);
            DrawClose(CloseRect(p), accent, closeHover);
            if (nudgeOnly) DrawNudge(p, accent);
            else DrawDemo(p, accent, t);

            // Close hint, bottom right, once the keys are accepted (InstructionRunner.minShowTime). A gentle blink.
            float alpha = CardGui.Alpha;
            CardGui.Alpha *= Smooth((t - 0.25f) / 0.3f) * (0.75f + 0.25f * Mathf.Sin(t * 4f));
            float width = CardGui.InlineWidth(Hint, HintSize, HintKeyScale, HintLabelScale);
            CardGui.Inline(Hint, new Vector2(p.xMax - 34f - width * 0.5f, p.yMax - 40f), HintSize, new Color(1f, 1f, 1f, 0.88f), accent, null,
                           true, HintKeyScale, HintLabelScale);
            CardGui.Alpha = alpha;
        }

        // The nudge: one vague line, and how far the full card is.
        void DrawNudge(Rect p, Color accent)
        {
            float maxWidth = p.width - 80f, y = p.y + StageTop + NudgeBody * 0.38f;
            CardGui.Inline(data.nudge, new Vector2(p.center.x, y), FitFont(data.nudge, CaptionSize, maxWidth), Color.white, accent, null);
            int left = Game.Instructions ? Game.Instructions.DeathsUntilDemo : 0;
            if (left > 0)
            {
                string more = left == 1 ? "Still stuck? The full tip unlocks after 1 more try." : $"Still stuck? The full tip unlocks after {left} more tries.";
                CardGui.Inline(more, new Vector2(p.center.x, y + SubCaptionGap + 8f), SubCaptionSize, new Color(1f, 1f, 1f, 0.6f), accent, null);
            }
        }

        void DrawDemo(Rect p, Color accent, float t)
        {
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
            float h = nudgeOnly ? StageTop + NudgeBody + HintRow
                                : StageTop + StageHeight + CaptionGap + (HasSubCaption ? SubCaptionGap : 0f) + HintRow;
            return new Rect((UiKit.RefWidth - PanelWidth) * 0.5f, (UiKit.RefHeight - h) * 0.5f - 10f, PanelWidth, h);
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

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
