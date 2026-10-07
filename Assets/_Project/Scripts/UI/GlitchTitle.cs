using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The game's title as the main menu (and the ending) shows it: colorless letters that glitch now and then, color
    /// bleeding into them for a split second as if it were trying to come back, then falling back to gray.
    ///   A glitch (GlitchClock): a brief RGB split, two or three horizontal slices knocked sideways, scanline noise and
    ///   one flicker, all inside the title; letters and heart bands flash their color while it lasts.
    ///   Progress: each restored color stays in its letters (and its band of the heart) a little more the more colors
    ///   are back; with all seven restored the title is fully in color and glitches only rarely.
    /// Letters take the spectrum in turn: R-E-C-O-L-O-R is red to violet, then it starts over.
    /// Comfortable on purpose: nothing full-screen, one flash per glitch, glitches at least ~1 s apart.
    /// </summary>
    sealed class GlitchTitle
    {
        readonly GlitchClock clock = new();
        readonly float[] stay = new float[7];
        float startedAt = -1f, intensity = 1f;
        bool revealed;

        /// <summary>Per frame, before drawing: reads the save, schedules glitches (rarer as colors come back).</summary>
        public void Tick(float now)
        {
            if (startedAt < 0f) startedAt = now; // no random glitch before the reveal below (Tick waits for it)
            int restored = 0;
            for (int i = 0; i < stay.Length; i++)
                if (UiKit.Restored(UiKit.Spectrum[i])) restored++;
            float k = restored / 7f;
            for (int i = 0; i < stay.Length; i++)
                stay[i] = UiKit.Restored(UiKit.Spectrum[i]) ? Mathf.Lerp(0.55f, 1f, k) : 0f;
            clock.MinGap = Mathf.Lerp(1.2f, 6f, k);
            clock.MaxGap = Mathf.Lerp(3.2f, 11f, k);
            intensity = Mathf.Lerp(1f, 0.5f, k);

            // Colorless at first; then the first glitch, the strongest, as it settles in.
            if (!revealed && now - startedAt >= 0.9f)
            {
                revealed = true;
                clock.Kick(now, 0.32f);
            }
            else if (revealed) clock.Tick(now);
        }

        /// <summary>How much the title fades in (0 -> 1 over its first moments).</summary>
        public float FadeIn(float now) => startedAt < 0f ? 0f : UiKit.Smooth((now - startedAt) / 0.7f);

        /// <summary>
        /// The title centered on `center` (canvas units of `view`, which must be the current GUI.matrix), `size` px.
        /// backdrop: the color behind it (a glitched slice is cut out with it).
        /// </summary>
        public void Draw(UiKit.View view, Vector2 center, int size, float now, Color backdrop)
        {
            string text = UiKit.GameTitle;
            float g = clock.Amount(now) * intensity;
            float tracking = size * 0.32f, spaceWidth = size * 0.55f;

            // Lay the letters out.
            var xs = new float[text.Length];
            var widths = new float[text.Length];
            float total = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                widths[i] = text[i] == ' ' ? spaceWidth : CardGui.Measure(text[i].ToString(), size, FontStyle.Bold).x;
                xs[i] = total;
                total += widths[i] + (i < text.Length - 1 ? tracking : 0f);
            }
            float x0 = center.x - total * 0.5f;
            var line = new Rect(x0 - 20f, center.y - size * 0.75f, total + 40f, size * 1.5f);
            var jolt = g > 0f ? new Vector2((clock.Rand(77) - 0.5f) * 6f * g, 0f) : Vector2.zero;

            float a = CardGui.Alpha;
            CardGui.Alpha *= FadeIn(now);
            if (g > 0.05f) // RGB split: red one way, cyan the other
            {
                float split = 3f + 8f * g;
                Letters(new Vector2(-split, 0f) + jolt, _ => new Color(1f, 0.2f, 0.35f, 0.5f * g));
                Letters(new Vector2(split, 0f) + jolt, _ => new Color(0.2f, 0.9f, 1f, 0.5f * g));
            }
            float flicker = g > 0.55f && clock.Rand(5) < 0.5f ? 0.6f : 1f;
            float main = CardGui.Alpha;
            CardGui.Alpha *= flicker;
            Letters(jolt, LetterColor);
            CardGui.Alpha = main;

            if (g > 0.2f) // slices knocked sideways
            {
                int slices = 2 + (clock.Rand(9) < 0.5f ? 1 : 0);
                for (int s = 0; s < slices; s++)
                {
                    float h = Mathf.Lerp(4f, size * 0.22f, clock.Rand(20 + s));
                    float y = Mathf.Lerp(line.y + size * 0.2f, line.yMax - size * 0.2f - h, clock.Rand(30 + s));
                    float dx = (clock.Rand(40 + s) < 0.5f ? -1f : 1f) * Mathf.Lerp(8f, 24f, clock.Rand(50 + s)) * g;
                    var slice = new Rect(line.x, y, line.width, h);
                    CardGui.Box(slice, backdrop);
                    Clipped(view, slice, new Vector2(dx, 0f) + jolt, () => Letters(Vector2.zero, LetterColor));
                }
            }
            if (g > 0f) // scanline noise
                for (int s = 0; s < 4; s++)
                {
                    float y = Mathf.Lerp(line.y, line.yMax, clock.Rand(60 + s));
                    float from = Mathf.Lerp(line.x, line.xMax, clock.Rand(70 + s) * 0.6f);
                    CardGui.Box(new Rect(from, y, line.width * Mathf.Lerp(0.15f, 0.5f, clock.Rand(80 + s)), 1.5f), new Color(1f, 1f, 1f, 0.3f * g));
                }
            CardGui.Alpha = a;

            Color LetterColor(int i)
            {
                int band = BandOf(text, i);
                var c = UiKit.Accent(UiKit.Spectrum[band]);
                float bleed = g > 0f && clock.Rand(100 + i) < 0.55f ? g * 0.85f : 0f;
                return Color.Lerp(new Color(0.84f, 0.85f, 0.88f), c, Mathf.Max(stay[band], bleed));
            }

            void Letters(Vector2 offset, System.Func<int, Color> color)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == ' ') continue;
                    var r = new Rect(x0 + xs[i] + offset.x - 4f, center.y - size + offset.y, widths[i] + 8f, size * 2f);
                    CardGui.Text(r, text[i].ToString(), size, color(i), TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }
        }

        /// <summary>
        /// The seven-band heart in `r`, matching the title: restored bands stay lit (as much as the title's letters),
        /// the others are gray and flash their color with each glitch. beat 0..1 brightens it a little.
        /// </summary>
        public void Heart(Rect r, float now, Color outline, float beat = 0f)
        {
            float g = clock.Amount(now) * intensity;
            var bands = new Color[7];
            for (int i = 0; i < 7; i++)
            {
                var c = UiKit.Accent(UiKit.Spectrum[i]);
                float bleed = g > 0f && clock.Rand(200 + i) < 0.6f ? g * 0.9f : 0f;
                bands[i] = Color.Lerp(new Color(0.24f, 0.25f, 0.29f), Color.Lerp(c, Color.white, 0.15f * beat), Mathf.Max(stay[i], bleed));
            }
            var jolt = g > 0f ? new Vector2((clock.Rand(210) - 0.5f) * 8f * g, 0f) : Vector2.zero;
            float a = CardGui.Alpha;
            CardGui.Alpha *= FadeIn(now);
            if (g > 0.05f)
            {
                UiKit.HeartShape(new Rect(r.x - 6f * g + jolt.x, r.y, r.width, r.height), new Color(1f, 0.2f, 0.35f, 0.35f * g));
                UiKit.HeartShape(new Rect(r.x + 6f * g + jolt.x, r.y, r.width, r.height), new Color(0.2f, 0.9f, 1f, 0.35f * g));
            }
            UiKit.Heart(new Rect(r.position + jolt, r.size), bands, outline);
            CardGui.Alpha = a;
        }

        /// <summary>Which spectrum color letter i takes: letters count up through red..violet, spaces skipped.</summary>
        static int BandOf(string text, int index)
        {
            int n = 0;
            for (int i = 0; i < index; i++) if (text[i] != ' ') n++;
            return n % 7;
        }

        /// <summary>
        /// Draws text-only content (`draw`) clipped to `clip` (canvas units) and shifted by `offset`. The clip is set in
        /// screen pixels; CardGui.Text draws in screen pixels too, so the canvas matrix just has to land it there.
        /// </summary>
        static void Clipped(UiKit.View view, Rect clip, Vector2 offset, System.Action draw)
        {
            var screen = view.ToScreen(clip);
            GUI.matrix = Matrix4x4.identity;
            GUI.BeginClip(screen);
            GUI.matrix = Matrix4x4.Translate(-(Vector3)screen.position) * view.Matrix * Matrix4x4.Translate(offset);
            draw();
            GUI.matrix = Matrix4x4.identity;
            GUI.EndClip();
            view.Begin();
        }
    }
}
