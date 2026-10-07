using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The game's one UI look, shared by every IMGUI screen (Tip card, HUD, boss bar, dialogue, pause, main menu, hub,
    /// ending, title card, letterbox). It grew out of the Tip card: dark translucent panels with a thin border in the
    /// current color, L-notches on the corners, faint scanlines, header strips with letter-spaced titles, keycaps
    /// (CardGui), segmented bars, gems and the heart.
    ///
    /// Everything is laid out on a 1920x1080 virtual canvas scaled to the screen: Fit() keeps the whole canvas on screen
    /// (centered; cards and menus), Fill() uses the same scale but reaches every screen edge (HUD corners). Text is
    /// rasterized at its on-screen size, so it stays crisp at any resolution (CardGui.Text).
    ///
    /// Colors: one accent per color (Accent), the current level's (CurrentAccent), or Neutral outside levels.
    /// Every draw goes through CardGui, so CardGui.Alpha fades whatever is drawn; screens set it back to 1 when done.
    /// The kit only draws: screens read the game's state and listen to GameEvents, never drive gameplay.
    /// </summary>
    static class UiKit
    {
        /// <summary>The game's name, wherever it's shown (menu, ending, pause). ProjectSettings' product name is separate.</summary>
        public const string GameTitle = "RECOLOR THE HEART";
        /// <summary>The hub's title and the line under it.</summary>
        public const string HubTitle = "THE GRAY CITY";
        public const string HubSubtitle = "Choose a district";

        public const float RefWidth = 1920f, RefHeight = 1080f;

        // ---------- Palette ----------

        /// <summary>Panel fill: dark and a little see-through.</summary>
        public static readonly Color Ink = new(0.02f, 0.04f, 0.06f, 0.86f);
        /// <summary>Full-screen backdrops (menu, hub, ending).</summary>
        public static readonly Color Night = new(0.03f, 0.035f, 0.05f, 1f);
        public static readonly Color TextColor = new(0.95f, 0.96f, 0.98f);
        public static readonly Color TextDim = new(0.64f, 0.67f, 0.72f);
        /// <summary>The accent outside levels (menus, the hub's chrome) and the quiet gray of the Tip button.</summary>
        public static readonly Color Neutral = new(0.8f, 0.83f, 0.88f);
        public static readonly Color Gray = new(0.42f, 0.44f, 0.49f);
        public static readonly Color Danger = new(1f, 0.32f, 0.32f);

        // One accent per color, by ColorId (the same hexes the Tip cards' captions use).
        static readonly Color[] Accents =
        {
            Hex(0xFF4A3D), Hex(0xFF9A2E), Hex(0xFFD93B), Hex(0x4CD964), Hex(0x3D8BFF), Hex(0x7B6CFF), Hex(0xC266FF),
        };

        /// <summary>The seven colors in spectrum order (red at the top of the heart, violet at its tip).</summary>
        public static readonly ColorId[] Spectrum =
        {
            ColorId.Red, ColorId.Orange, ColorId.Yellow, ColorId.Green, ColorId.Blue, ColorId.Indigo, ColorId.Violet,
        };

        public static Color Accent(ColorId id) => (int)id >= 0 && (int)id < Accents.Length ? Accents[(int)id] : Neutral;

        /// <summary>The current level's color, or Neutral outside a level.</summary>
        public static Color CurrentAccent => LevelController.Current ? Accent(LevelController.Current.Color) : Neutral;

        /// <summary>Is `id` restored in the save (false before there is one)?</summary>
        public static bool Restored(ColorId id) => Game.Manager && Game.Progress != null && Game.Progress.IsRestored(id);

        /// <summary>The accents of the restored colors, in spectrum order (backdrops and dust pick them up).</summary>
        public static List<Color> RestoredColors()
        {
            var list = new List<Color>();
            foreach (var id in Spectrum) if (Restored(id)) list.Add(Accent(id));
            return list;
        }

        /// <summary>The average of `colors`, or `fallback` when there are none.</summary>
        public static Color Average(List<Color> colors, Color fallback)
        {
            if (colors == null || colors.Count == 0) return fallback;
            var sum = Color.clear;
            foreach (var c in colors) sum += c;
            return sum / colors.Count;
        }

        public static int RestoredCount
        {
            get
            {
                int n = 0;
                foreach (var c in Spectrum) if (Restored(c)) n++;
                return n;
            }
        }

        // ---------- Canvas ----------

        /// <summary>The virtual canvas for one OnGUI: where it sits on screen and where the mouse is on it.</summary>
        public readonly struct View
        {
            public readonly Matrix4x4 Matrix;
            /// <summary>The canvas in its own units: (0, 0, 1920, 1080) for Fit, the whole screen for Fill.</summary>
            public readonly Rect Rect;
            /// <summary>The mouse in canvas units.</summary>
            public readonly Vector2 Mouse;
            public readonly float Scale;

            public View(Matrix4x4 matrix, Rect rect, float scale)
            {
                Matrix = matrix;
                Rect = rect;
                Scale = scale;
                Mouse = matrix.inverse.MultiplyPoint3x4(ScreenMouse);
            }

            /// <summary>Draw on the canvas from here on (GUI.matrix). End() goes back to screen pixels.</summary>
            public void Begin() => GUI.matrix = Matrix;

            public Rect ToScreen(Rect r)
            {
                Vector2 min = Matrix.MultiplyPoint3x4(r.min), max = Matrix.MultiplyPoint3x4(r.max);
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        public static float CanvasScale => Mathf.Max(0.01f, Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight));

        /// <summary>The 1920x1080 canvas, scaled to fit and centered on the screen.</summary>
        public static View Fit()
        {
            float s = CanvasScale;
            var m = Matrix4x4.TRS(new Vector3((Screen.width - RefWidth * s) * 0.5f, (Screen.height - RefHeight * s) * 0.5f, 0f),
                                  Quaternion.identity, new Vector3(s, s, 1f));
            return new View(m, new Rect(0f, 0f, RefWidth, RefHeight), s);
        }

        /// <summary>The same scale as Fit, reaching every screen edge: the rect is wider (or taller) than 1920x1080 off 16:9.</summary>
        public static View Fill()
        {
            float s = CanvasScale;
            return new View(Matrix4x4.Scale(new Vector3(s, s, 1f)), new Rect(0f, 0f, Screen.width / s, Screen.height / s), s);
        }

        public static void End()
        {
            GUI.matrix = Matrix4x4.identity;
            CardGui.Alpha = 1f;
        }

        /// <summary>The mouse in screen GUI pixels (origin top-left), whatever GUI.matrix is set to.</summary>
        public static Vector2 ScreenMouse
        {
            get
            {
                if (Event.current == null) return new Vector2(-9999f, -9999f);
                var m = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                var p = Event.current.mousePosition;
                GUI.matrix = m;
                return p;
            }
        }

        // ---------- Panels ----------

        /// <summary>
        /// The card frame: a faint outer line, translucent ink with a wash and scanlines in the accent, an optional band
        /// sweeping down (time &gt;= 0, like a projector refreshing), a thin accent border and L-notches on the corners.
        /// notch: length of the notches' arms (38 on the Tip card; smaller panels use less).
        /// </summary>
        public static void Panel(Rect p, Color accent, float time = -1f, float notch = 38f, float fill = 1f)
        {
            float k = notch / 38f;
            accent.a = 1f;
            CardGui.Outline(Expand(p, 7f * k), WithAlpha(accent, 0.15f), 2f);
            CardGui.Box(p, WithAlpha(Ink, Ink.a * fill));
            CardGui.Box(p, WithAlpha(accent, 0.07f));
            Scanlines(p, WithAlpha(accent, 0.035f));
            if (time >= 0f)
            {
                float bandY = p.y + (Mathf.Repeat(time * 0.3f, 1.25f) - 0.1f) * p.height;
                float top = Mathf.Max(p.y, bandY), bottom = Mathf.Min(p.yMax, bandY + 70f * k);
                if (bottom > top) CardGui.Box(new Rect(p.x, top, p.width, bottom - top), WithAlpha(accent, 0.05f));
            }
            CardGui.Outline(p, WithAlpha(accent, 0.9f), 2f);
            Notches(p, accent, notch, Mathf.Max(2f, 6f * k));
        }

        /// <summary>L-brackets on the four corners of `p`, their arms running inward.</summary>
        public static void Notches(Rect p, Color c, float length, float thick)
        {
            Corner(new Vector2(p.x, p.y), 1f, 1f);
            Corner(new Vector2(p.xMax, p.y), -1f, 1f);
            Corner(new Vector2(p.x, p.yMax), 1f, -1f);
            Corner(new Vector2(p.xMax, p.yMax), -1f, -1f);

            void Corner(Vector2 at, float sx, float sy)
            {
                float x = sx > 0f ? at.x - thick * 0.5f : at.x - length + thick * 0.5f;
                float y = sy > 0f ? at.y - thick * 0.5f : at.y - length + thick * 0.5f;
                CardGui.Box(new Rect(x, at.y - thick * 0.5f, length, thick), c);
                CardGui.Box(new Rect(at.x - thick * 0.5f, y, thick, length), c);
            }
        }

        /// <summary>Thin horizontal lines every `spacing` units.</summary>
        public static void Scanlines(Rect r, Color c, float spacing = 6f, float thickness = 1.5f)
        {
            for (float y = r.y + spacing * 0.5f; y < r.yMax; y += spacing)
                CardGui.Box(new Rect(r.x, y, r.width, Mathf.Min(thickness, r.yMax - y)), c);
        }

        /// <summary>
        /// A header strip along the top of panel `p`: an accent wash, a line under it, a small tab and the title in
        /// spaced capitals. Returns the strip (for things on its right: a close X, a counter).
        /// </summary>
        public static Rect Header(Rect p, string title, Color accent, float height = 46f, int size = 24)
        {
            accent.a = 1f;
            var header = new Rect(p.x + 2f, p.y + 2f, p.width - 4f, height);
            CardGui.Box(header, WithAlpha(accent, 0.14f));
            CardGui.Box(new Rect(p.x, header.yMax, p.width, 2f), WithAlpha(accent, 0.6f));
            float tab = height * 0.435f;
            CardGui.Box(new Rect(p.x + 22f, header.y + (height - tab) * 0.5f, 8f, tab), accent);
            CardGui.Text(new Rect(p.x + 44f, header.y, p.width - 88f, header.height), Spaced(title.ToUpperInvariant()), size, accent,
                         TextAnchor.MiddleLeft, FontStyle.Bold);
            return header;
        }

        /// <summary>Spaced capitals, the titles' look: "PAUSED" -> "P A U S E D" (words end up three spaces apart).</summary>
        public static string Spaced(string s) => string.IsNullOrEmpty(s) ? s : string.Join(" ", s.ToCharArray());

        /// <summary>
        /// A menu row: a dark slab with a gray line, or, selected, lit in the accent with a tab, notches and a chevron.
        /// The caller hit-tests `r` against View.Mouse.
        /// </summary>
        public static void Button(Rect r, string label, Color accent, bool selected, bool enabled = true, int size = 26, float time = 0f)
        {
            accent.a = 1f;
            float a = CardGui.Alpha;
            if (!enabled) CardGui.Alpha *= 0.45f;
            CardGui.Box(r, new Color(0f, 0f, 0f, 0.4f));
            if (selected)
            {
                CardGui.Box(r, WithAlpha(accent, 0.16f + 0.04f * Mathf.Sin(time * 4f)));
                CardGui.Box(new Rect(r.x, r.y, 6f, r.height), accent);
                CardGui.Outline(r, WithAlpha(accent, 0.7f), 1.5f);
                Notches(r, accent, 12f, 3f);
                Chevron(new Vector2(r.x + 30f, r.center.y), r.height * 0.16f, accent);
            }
            else CardGui.Outline(r, WithAlpha(Gray, 0.7f), 1.5f);
            CardGui.Text(new Rect(r.x + 56f, r.y, r.width - 70f, r.height), Spaced(label.ToUpperInvariant()), size,
                         selected ? TextColor : TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            CardGui.Alpha = a;
        }

        /// <summary>A small "&gt;" pointing right, centered on `c`.</summary>
        public static void Chevron(Vector2 c, float size, Color color, float width = 3f)
        {
            CardGui.Line(c + new Vector2(-size * 0.5f, -size), c + new Vector2(size * 0.5f, 0f), width, color);
            CardGui.Line(c + new Vector2(-size * 0.5f, size), c + new Vector2(size * 0.5f, 0f), width, color);
        }

        /// <summary>A line of key hints ("[Left] [Right] Select   [Z] Enter"), centered on `center`; returns its width.</summary>
        public static float Hint(Vector2 center, string line, Color accent, int size = 22, float alpha = 1f)
        {
            float a = CardGui.Alpha;
            CardGui.Alpha *= alpha;
            float w = CardGui.Inline(line, center, size, TextDim, accent, null, true, 1.45f, 1.1f);
            CardGui.Alpha = a;
            return w;
        }

        public static float HintWidth(string line, int size = 22) => CardGui.InlineWidth(line, size, 1.45f, 1.1f);

        // ---------- Bars, pips, gems ----------

        /// <summary>
        /// A bar cut into `segments` (one per hit point, or a fixed count): `fraction` of it lit in `fill`. ghost &gt;
        /// fraction shows recent damage trailing behind in `ghostColor`. ticks: fractions marked with a line across the
        /// bar (phase thresholds).
        /// </summary>
        public static void SegmentBar(Rect r, float fraction, int segments, Color fill, Color empty, float gap = 3f,
                                      IReadOnlyList<float> ticks = null, Color? tickColor = null, float ghost = -1f, Color? ghostColor = null)
        {
            segments = Mathf.Max(1, segments);
            float w = (r.width - gap * (segments - 1)) / segments;
            float lit = Mathf.Clamp01(fraction) * segments, trail = Mathf.Clamp01(ghost) * segments;
            for (int i = 0; i < segments; i++)
            {
                var s = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                CardGui.Box(s, empty);
                float g = Mathf.Clamp01(trail - i), part = Mathf.Clamp01(lit - i);
                if (g > part && ghostColor.HasValue) CardGui.Box(new Rect(s.x, s.y, s.width * g, s.height), ghostColor.Value);
                if (part > 0f) CardGui.Box(new Rect(s.x, s.y, s.width * part, s.height), fill);
            }
            if (ticks == null) return;
            foreach (var t in ticks)
            {
                float x = r.x + r.width * Mathf.Clamp01(t);
                CardGui.Box(new Rect(x - 1.5f, r.y - 7f, 3f, r.height + 14f), tickColor ?? TextColor);
                CardGui.Diamond(new Vector2(x, r.y - 9f), 7f, tickColor ?? TextColor);
            }
        }

        /// <summary>A box leaning forward by `skew` degrees (the HP pips' slant).</summary>
        public static void Slanted(Rect r, Color c, float skew = 14f)
        {
            var prev = GUI.matrix;
            var shear = Matrix4x4.identity;
            shear.m01 = -Mathf.Tan(skew * Mathf.Deg2Rad); // GUI y points down: the top moves right
            var center = (Vector3)r.center;
            GUI.matrix = prev * Matrix4x4.Translate(center) * shear * Matrix4x4.Translate(-center);
            CardGui.Box(r, c);
            GUI.matrix = prev;
        }

        /// <summary>
        /// A gem (a turned square, `size` tall): lit = its color with a glow and a highlight facet; unlit = hollow gray.
        /// pulse 0..1 adds glow (just restored, the current level).
        /// </summary>
        public static void Gem(Vector2 c, float size, Color color, bool lit, float pulse = 0f)
        {
            float side = size / 1.41421f;
            if (lit)
            {
                CardGui.Glow(c, size * (0.9f + 0.6f * pulse), WithAlpha(color, 0.3f + 0.35f * pulse));
                CardGui.Diamond(c, side + 4f, new Color(0f, 0f, 0f, 0.6f));
                CardGui.Diamond(c, side, color);
                CardGui.Diamond(c + new Vector2(-size * 0.12f, -size * 0.12f), side * 0.35f, WithAlpha(Color.white, 0.55f));
            }
            else
            {
                CardGui.Diamond(c, side, WithAlpha(Gray, 0.85f));
                CardGui.Diamond(c, side - 4f, new Color(0.06f, 0.07f, 0.09f, 0.95f));
            }
        }

        // ---------- The heart ----------

        static Texture2D heart;
        static float heartBottom, heartTop; // the shape's rows in the texture (0 = bottom)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => heart = null;

        /// <summary>A white heart on a transparent square, anti-aliased (made once).</summary>
        static Texture2D HeartTexture
        {
            get
            {
                if (heart) return heart;
                const int size = 256, sub = 3;
                heart = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "UiHeart",
                };
                var pixels = new Color32[size * size];
                int minRow = size, maxRow = 0;
                for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < sub; sy++)
                    for (int sx = 0; sx < sub; sx++)
                    {
                        // The classic heart curve, (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0, framed to fill the square.
                        float x = ((px + (sx + 0.5f) / sub) / size - 0.5f) * 2.5f;
                        float y = ((py + (sy + 0.5f) / sub) / size - 0.5f) * 2.5f + 0.1f;
                        float a = x * x + y * y - 1f;
                        if (a * a * a - x * x * y * y * y <= 0f) inside++;
                    }
                    byte alpha = (byte)(255 * inside / (sub * sub));
                    pixels[py * size + px] = new Color32(255, 255, 255, alpha);
                    if (alpha > 0) { minRow = Mathf.Min(minRow, py); maxRow = Mathf.Max(maxRow, py); }
                }
                heart.SetPixels32(pixels);
                heart.Apply();
                heartBottom = minRow / (float)size;
                heartTop = (maxRow + 1) / (float)size;
                return heart;
            }
        }

        /// <summary>A heart filling `r` in one color (a backing, an outline: draw it a little bigger first).</summary>
        public static void HeartShape(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * CardGui.Alpha);
            GUI.DrawTexture(r, HeartTexture, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>
        /// The heart in seven bands, top to bottom in spectrum order (bands[0] = red ... bands[6] = violet), each in the
        /// color given (lit, gray, or flickering: the caller decides), with thin dark seams and an outline.
        /// </summary>
        public static void Heart(Rect r, IReadOnlyList<Color> bands, Color outline, float seam = 3f)
        {
            var tex = HeartTexture;
            HeartShape(Expand(r, Mathf.Max(3f, r.width * 0.025f)), outline);
            HeartShape(r, new Color(0.05f, 0.06f, 0.08f, 0.95f));
            float v0 = heartBottom, v1 = heartTop;
            float top = r.y + (1f - v1) * r.height, height = (v1 - v0) * r.height;
            int n = bands.Count;
            var prev = GUI.color;
            for (int i = 0; i < n; i++)
            {
                float y0 = top + height * i / n, y1 = top + height * (i + 1) / n - (i < n - 1 ? seam : 0f);
                if (y1 <= y0) continue;
                // Texture rows for this slice (v grows upward, screen y grows downward).
                float vHigh = 1f - (y0 - r.y) / r.height, vLow = 1f - (y1 - r.y) / r.height;
                var c = bands[i];
                GUI.color = new Color(c.r, c.g, c.b, c.a * CardGui.Alpha);
                GUI.DrawTextureWithTexCoords(new Rect(r.x, y0, r.width, y1 - y0), tex, new Rect(0f, vLow, 1f, vHigh - vLow), true);
            }
            GUI.color = prev;
        }

        /// <summary>The middle of band `i` of a heart drawn in `r` (for sparks and markers).</summary>
        public static Vector2 HeartBand(Rect r, int i, int count = 7)
        {
            _ = HeartTexture; // makes sure heartTop / heartBottom are known
            float top = r.y + (1f - heartTop) * r.height, height = (heartTop - heartBottom) * r.height;
            return new Vector2(r.center.x, top + height * (i + 0.5f) / count);
        }

        // ---------- Ambience ----------

        /// <summary>A vertical gradient over `r`, top to bottom, in `bands` steps.</summary>
        public static void Gradient(Rect r, Color top, Color bottom, int bands = 24)
        {
            float h = r.height / bands;
            for (int i = 0; i < bands; i++)
                CardGui.Box(new Rect(r.x, r.y + i * h, r.width, h + 1f), Color.Lerp(top, bottom, (i + 0.5f) / bands));
        }

        /// <summary>Slow dust drifting up through `r`: little diamonds that twinkle, tinted from `palette` (or `tint`).</summary>
        public static void Dust(Rect r, float time, int count, Color tint, IReadOnlyList<Color> palette = null, float alpha = 0.2f)
        {
            for (int i = 0; i < count; i++)
            {
                float speed = Mathf.Lerp(6f, 22f, Hash(i * 3.1f));
                float x = r.x + Mathf.Repeat(Hash(i * 1.3f) * r.width + Mathf.Sin(time * 0.25f + i) * 24f, r.width);
                float y = r.y + Mathf.Repeat(Hash(i * 2.7f) * r.height - time * speed, r.height);
                var c = palette != null && palette.Count > 0 ? palette[i % palette.Count] : tint;
                float twinkle = 0.45f + 0.55f * Mathf.Sin(time * (0.6f + Hash(i * 5.3f)) + i * 1.7f);
                CardGui.Diamond(new Vector2(x, y), Mathf.Lerp(2f, 6f, Hash(i * 4.7f)), WithAlpha(c, alpha * Mathf.Max(0f, twinkle)), time * 20f + i * 37f);
            }
        }

        /// <summary>The night backdrop tinted a little toward `tint` (opaque): the bottom of the menus' gradients.</summary>
        public static Color Backdrop(Color tint, float amount)
        {
            var c = Color.Lerp(Night, tint, amount);
            c.a = 1f;
            return c;
        }

        /// <summary>A slow heartbeat for the heart: two soft bumps every ~1.3 s, 0..1.</summary>
        public static float Heartbeat(float time)
        {
            float k = Mathf.Repeat(time, 1.3f);
            return Mathf.Max(Bump(k, 0f, 0.18f), 0.6f * Bump(k, 0.24f, 0.2f));

            static float Bump(float t, float at, float width)
            {
                float x = (t - at) / width;
                return x < 0f || x > 1f ? 0f : Mathf.Sin(x * Mathf.PI);
            }
        }

        /// <summary>Edges darkened toward the middle.</summary>
        public static void Vignette(Rect r, Color c, int steps = 8, float width = 18f)
        {
            for (int i = 0; i < steps; i++)
            {
                float inset = i * width;
                CardGui.Outline(new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f),
                                WithAlpha(c, c.a * (1f - i / (float)steps) * 0.4f), width);
            }
        }

        // ---------- Abilities ----------

        /// <summary>What the HUD and menus show for an ability: its keys (as InputReader binds them), name and a tag.</summary>
        public readonly struct AbilityInfo
        {
            public readonly string[] Keys;
            public readonly string Name, Short, Tag;
            public AbilityInfo(string[] keys, string name, string shortName, string tag) { Keys = keys; Name = name; Short = shortName; Tag = tag; }
        }

        public static AbilityInfo Ability(AbilityId id) => id switch
        {
            AbilityId.LightShot => new AbilityInfo(new[] { "RMB" }, "LIGHT SHOT", "SHOT", null),
            AbilityId.Dash => new AbilityInfo(new[] { "Shift" }, "DASH", "DASH", null),
            AbilityId.BlazeStrike => new AbilityInfo(new[] { "LMB" }, "BLAZE STRIKE", "BLAZE", "HOLD"),
            AbilityId.DoubleJump => new AbilityInfo(new[] { "Space" }, "DOUBLE JUMP", "2X JUMP", null),
            AbilityId.DownDash => new AbilityInfo(new[] { "Down", "Shift" }, "DOWN DASH", "DOWN DASH", null),
            AbilityId.Serenity => new AbilityInfo(new[] { "Q" }, "SERENITY", "SERENITY", null),
            AbilityId.HeavySlam => new AbilityInfo(new[] { "Down", "LMB" }, "HEAVY SLAM", "SLAM", null),
            _ => new AbilityInfo(new string[0], "", "", null),
        };

        /// <summary>Keycaps side by side with a "+" between (Down + Shift), left edge at x; returns the width.</summary>
        public static float Keys(IReadOnlyList<string> keys, float x, float centerY, float height, float press, Color accent, bool draw = true)
        {
            float gap = height * 0.45f, at = x;
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                {
                    if (draw) CardGui.Text(new Rect(at, centerY - height * 0.5f, gap, height), "+", Mathf.RoundToInt(height * 0.5f), TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                    at += gap;
                }
                float w = CardGui.KeyWidth(keys[i], height);
                if (draw) CardGui.Key(new Rect(at, centerY - height * 0.5f, w, height), keys[i], press, accent);
                at += w;
            }
            return at - x;
        }

        // ---------- Helpers ----------

        public static Rect Expand(Rect r, float by) => new(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        public static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
        public static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>A stable pseudo-random 0..1 for a seed (no flicker between frames).</summary>
        public static float Hash(float n)
        {
            float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }

    /// <summary>
    /// Times short glitches at random intervals (the title, the heart's next band, a card being selected): Amount(now)
    /// snaps up and decays over each one. Comfortable by design: one flash per glitch, at least MinGap seconds apart,
    /// so never more than ~3 flashes a second. Unscaled time.
    /// </summary>
    sealed class GlitchClock
    {
        float nextAt, startAt = -100f, length = 0.2f;

        /// <summary>Seconds between glitches (random in between).</summary>
        public float MinGap = 1.2f, MaxGap = 3.5f;
        /// <summary>Changes every glitch: use it (Rand) to pick which slices / letters it hits.</summary>
        public int Seed { get; private set; }

        /// <summary>Starts a glitch now.</summary>
        public void Kick(float now, float seconds = 0.2f)
        {
            startAt = now;
            length = Mathf.Max(0.05f, seconds);
            Seed = Random.Range(1, 100000);
            nextAt = Mathf.Max(nextAt, now + length + MinGap);
        }

        /// <summary>The next glitch waits at least this long.</summary>
        public void Hold(float now, float seconds) => nextAt = Mathf.Max(nextAt, now + seconds);

        /// <summary>Call every frame: starts the next glitch when it's due.</summary>
        public void Tick(float now)
        {
            if (now < nextAt) return;
            Kick(now, Random.Range(0.14f, 0.26f));
            nextAt = now + length + Random.Range(Mathf.Max(0.35f, MinGap), Mathf.Max(MinGap, MaxGap));
        }

        /// <summary>0 between glitches; snaps to 1 and fades out over one.</summary>
        public float Amount(float now)
        {
            float k = (now - startAt) / length;
            if (k < 0f || k >= 1f) return 0f;
            return k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
        }

        /// <summary>A 0..1 number fixed for this glitch and this index.</summary>
        public float Rand(int i) => UiKit.Hash(Seed * 0.0137f + i * 1.371f);
    }

    /// <summary>Menu steps from a direction held on the keyboard / stick: one on the press, then repeating while held.</summary>
    sealed class MenuNav
    {
        int lastDir;
        float repeatAt;

        /// <summary>-1, 0 or +1 along `axis` this frame. Unscaled time, so it works while the game is paused.</summary>
        public int Step(float axis)
        {
            int dir = axis > 0.5f ? 1 : axis < -0.5f ? -1 : 0;
            float now = Time.unscaledTime;
            if (dir == 0) { lastDir = 0; return 0; }
            if (dir != lastDir)
            {
                lastDir = dir;
                repeatAt = now + 0.38f;
                return dir;
            }
            if (now < repeatAt) return 0;
            repeatAt = now + 0.13f;
            return dir;
        }
    }
}
