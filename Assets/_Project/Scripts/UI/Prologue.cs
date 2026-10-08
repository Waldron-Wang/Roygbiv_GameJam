using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The story before the first level (IMGUI, UiKit look), played by MainMenuScreen on START for a save that hasn't
    /// seen it (GameManager.ShouldPlayPrologue). Its words are GameConfig.prologue (a DialogueData: edit the lines in
    /// the Inspector); the pictures are the HUD's own heart and seven gems, large, and follow the lines in beats:
    ///   Whole     the heart in full color, beating
    ///   Named     the seven gems appear around it, one by one
    ///   Decree    a violet shockwave; the heart drains to gray, top to bottom
    ///   Scatter   the heart cracks; the gems fly off to the edges and go dark
    ///   Spark     dark and gray, but a spark of yellow still glows in its band
    ///   Go        the spark swells with the heartbeat, then everything fades
    /// The last line is always Go and the one before it Spark; the lines before those share out the first four beats,
    /// so lines can be added or cut freely. Z / Enter / click: next line (after it has faded in). Esc: skip it all.
    /// A line also moves on by itself after a while. Unscaled time.
    /// </summary>
    public class Prologue
    {
        enum Beat { Whole, Named, Decree, Scatter, Spark, Go }

        const float FadeIn = 0.6f, MinRead = 0.8f, AutoNext = 7f, OutroTime = 1.4f;
        const string Hint = "[Z] / [Enter] next      [Esc] skip";

        List<DialogueLine> lines;
        int index;
        float lineAt, startedAt, outroAt = -1f;

        public bool Playing { get; private set; }
        /// <summary>The last line has faded out (or Esc): time to start the level.</summary>
        public bool Finished => Playing && outroAt >= 0f && Now - outroAt >= OutroTime;

        static float Now => Time.unscaledTime;

        public void Begin(DialogueData data)
        {
            lines = data ? data.lines : new List<DialogueLine>();
            index = 0;
            startedAt = lineAt = Now;
            outroAt = lines.Count == 0 ? Now - OutroTime : -1f;
            Playing = true;
        }

        public void Stop() => Playing = false;

        /// <summary>Call every frame while playing: reads Z / Enter / Esc.</summary>
        public void Tick()
        {
            if (!Playing || outroAt >= 0f || !Game.Input) return;
            var intent = Game.Input.Intent;
            if (intent.pausePressed) { Skip(); return; }
            if ((intent.confirmPressed && Now - lineAt > MinRead) || Now - lineAt > AutoNext) Next();
        }

        /// <summary>A click on the screen: the same as Z.</summary>
        public void Click()
        {
            if (Playing && outroAt < 0f && Now - lineAt > MinRead) Next();
        }

        void Next()
        {
            if (index >= lines.Count - 1) { outroAt = Now; return; }
            index++;
            lineAt = Now;
        }

        void Skip() => outroAt = Now - OutroTime * 0.5f; // a quick fade, not a cut

        Beat BeatOf(int i)
        {
            int n = lines.Count;
            if (i >= n - 1) return Beat.Go;
            if (i == n - 2) return Beat.Spark;
            int story = Mathf.Max(1, n - 2);
            return (Beat)Mathf.Min(3, i * 4 / story);
        }

        // ---------- Drawing ----------

        public void Draw()
        {
            if (!Playing) return;
            var full = UiKit.Fill();
            full.Begin();
            float fadeAll = 1f - (outroAt >= 0f ? Mathf.Clamp01((Now - outroAt) / OutroTime) : 0f);
            CardGui.Alpha = 1f;
            UiKit.Gradient(full.Rect, UiKit.Night, UiKit.Backdrop(UiKit.Gray, 0.08f));
            UiKit.Dust(full.Rect, Now, 40, UiKit.Gray, null, 0.1f * fadeAll);
            UiKit.Vignette(full.Rect, new Color(0f, 0f, 0f, 0.7f));
            UiKit.End();

            var view = UiKit.Fit();
            view.Begin();
            CardGui.Alpha = fadeAll * UiKit.Smooth((Now - startedAt) / 1f);
            var beat = lines.Count > 0 ? BeatOf(index) : Beat.Go;
            float t = Now - lineAt;
            DrawPicture(beat, t);
            DrawLine(t);
            DrawHint();
            CardGui.Alpha = 1f;
            UiKit.End();
        }

        static readonly Vector2 HeartCenter = new(960f, 420f);
        static readonly Color Ash = new(0.24f, 0.25f, 0.28f); // a drained band: opaque, darker than UiKit.Gray
        const float HeartWidth = 300f, HeartHeight = 280f;

        void DrawPicture(Beat beat, float t)
        {
            float pulse = UiKit.Heartbeat(Now);
            float swell = beat == Beat.Go ? pulse * 6f + Mathf.Min(t, 2f) * 4f : beat <= Beat.Named ? pulse * 5f : 0f;
            var heart = new Rect(HeartCenter.x - HeartWidth * 0.5f - swell, HeartCenter.y - HeartHeight * 0.5f - swell,
                                 HeartWidth + swell * 2f, HeartHeight + swell * 2f);

            // How much of each band still has its color.
            var bands = new Color[7];
            float drained = beat == Beat.Decree ? Mathf.Clamp01(t / 1.8f) : beat >= Beat.Scatter ? 1f : 0f;
            for (int i = 0; i < 7; i++)
            {
                var id = UiKit.Spectrum[i];
                // Drains top to bottom: band i goes gray between i/7 and (i+1)/7 of the way.
                float gray = Mathf.Clamp01(drained * 7f - i);
                var c = Color.Lerp(UiKit.Accent(id), Ash, gray);
                if (id == ColorId.Yellow && beat >= Beat.Spark)
                    c = Color.Lerp(Ash, UiKit.Accent(id), UiKit.Smooth(t / 1.2f) * (0.55f + 0.45f * pulse) + (beat == Beat.Go ? 0.3f : 0f));
                bands[i] = c;
            }

            // A glow behind it: all the colors while it's whole, then nothing, then yellow.
            if (drained < 1f)
                CardGui.Glow(HeartCenter, 320f, UiKit.WithAlpha(Color.white, 0.06f * (1f - drained) * (1f + pulse)));
            if (beat >= Beat.Spark)
                CardGui.Glow(UiKit.HeartBand(heart, 2), 120f + (beat == Beat.Go ? 80f * Mathf.Min(t, 2f) : 0f),
                             UiKit.WithAlpha(UiKit.Accent(ColorId.Yellow), 0.25f * UiKit.Smooth(t / 1.2f) * (0.7f + 0.3f * pulse)));

            UiKit.Heart(heart, bands, UiKit.WithAlpha(UiKit.Neutral, 0.55f));

            // The decree: a violet shockwave out of the heart.
            if (beat == Beat.Decree && t < 1.6f)
            {
                float k = t / 1.6f;
                CardGui.Ring(HeartCenter, 60f + 700f * k, 10f * (1f - k) + 2f, UiKit.WithAlpha(UiKit.Accent(ColorId.Violet), 0.8f * (1f - k)));
                CardGui.Glow(HeartCenter, 260f, UiKit.WithAlpha(UiKit.Accent(ColorId.Violet), 0.25f * (1f - k)));
            }

            // The crack, once it's broken: a jagged line down the middle, drawn in during Scatter.
            if (beat >= Beat.Scatter) DrawCrack(heart, beat == Beat.Scatter ? Mathf.Clamp01(t / 0.5f) : 1f);

            DrawGems(beat, t);
        }

        static readonly Vector2[] Crack =
        {
            new(0.5f, 0.17f), new(0.46f, 0.32f), new(0.55f, 0.45f), new(0.47f, 0.6f), new(0.53f, 0.74f), new(0.5f, 0.88f),
        };

        static void DrawCrack(Rect heart, float amount)
        {
            int segments = Crack.Length - 1;
            for (int i = 0; i < segments; i++)
            {
                float k = Mathf.Clamp01(amount * segments - i);
                if (k <= 0f) break;
                var a = new Vector2(heart.x + Crack[i].x * heart.width, heart.y + Crack[i].y * heart.height);
                var b = new Vector2(heart.x + Crack[i + 1].x * heart.width, heart.y + Crack[i + 1].y * heart.height);
                CardGui.Line(a, Vector2.Lerp(a, b, k), 7f, new Color(0.02f, 0.02f, 0.03f, 0.95f));
            }
        }

        // Seven gems on an arc around the heart; on Scatter they fly off and go dark.
        static void DrawGems(Beat beat, float t)
        {
            if (beat < Beat.Named) return;
            for (int i = 0; i < 7; i++)
            {
                var id = UiKit.Spectrum[i];
                float angle = Mathf.Lerp(200f, -20f, i / 6f) * Mathf.Deg2Rad;
                var home = HeartCenter + new Vector2(Mathf.Cos(angle) * 300f, -Mathf.Sin(angle) * 230f + 40f);

                if (beat == Beat.Named)
                {
                    float appear = UiKit.Smooth((t - i * 0.35f) / 0.4f);
                    if (appear <= 0f) continue;
                    float a = CardGui.Alpha;
                    CardGui.Alpha *= appear;
                    UiKit.Gem(home, 40f, UiKit.Accent(id), true, 1f - appear);
                    CardGui.Alpha = a;
                }
                else if (beat == Beat.Decree)
                {
                    // Still there, losing their light as the heart drains.
                    UiKit.Gem(home, 40f, Color.Lerp(UiKit.Accent(id), UiKit.Gray, Mathf.Clamp01(t / 1.8f * 7f - i)), true);
                }
                else if (beat == Beat.Scatter)
                {
                    float k = UiKit.Smooth((t - 0.3f) / 1.6f);
                    var dir = (home - HeartCenter).normalized;
                    var at = home + dir * 900f * k + new Vector2(0f, 60f * Mathf.Sin(k * Mathf.PI + i));
                    float a = CardGui.Alpha;
                    CardGui.Alpha *= 1f - k;
                    UiKit.Gem(at, 40f, UiKit.Gray, true);
                    CardGui.Alpha = a;
                }
            }
        }

        void DrawLine(float t)
        {
            if (lines.Count == 0) return;
            float a = CardGui.Alpha;
            CardGui.Alpha *= UiKit.Smooth(t / FadeIn);
            var r = new Rect(260f, 740f, 1400f, 120f);
            CardGui.Text(r, lines[index].text, 36, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, true, UiKit.TextShadow);
            CardGui.Alpha = a;
        }

        void DrawHint()
        {
            float a = CardGui.Alpha;
            CardGui.Alpha *= 0.55f * UiKit.Smooth((Now - startedAt - 1.5f) / 0.6f);
            float width = CardGui.InlineWidth(Hint, 22);
            CardGui.Inline(Hint, new Vector2(1920f - 40f - width * 0.5f, 1080f - 44f), 22, UiKit.TextColor, UiKit.Neutral, null);
            CardGui.Alpha = a;
        }
    }
}
