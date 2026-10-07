using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A small IMGUI drawing kit for the instruction cards: boxes, rounded boxes, discs, soft glows, lines,
    /// sprites placed by their pivot (like a SpriteRenderer), text, keycaps and inline "text [Key] text" lines.
    /// Call it from OnGUI during Repaint. Every color is multiplied by Alpha (the card's fade-in).
    /// Coordinates are whatever GUI.matrix maps; rotation composes onto it, so it works under a scaled canvas.
    /// </summary>
    static class CardGui
    {
        public static float Alpha = 1f;

        static readonly Regex KeyToken = new(@"\[([^\[\]]+)\]");
        static Texture2D glow;
        static GUIStyle style;

        // Domain reload is off: drop the cached texture / style each Play session (they're rebuilt lazily).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Alpha = 1f;
            glow = null;
            style = null;
        }

        static Color Fade(Color c) { c.a *= Alpha; return c; }

        // ---------- Shapes ----------

        public static void Box(Rect r, Color c) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Fade(c), 0f, 0f);

        public static void Round(Rect r, Color c, float radius) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Fade(c), 0f, radius);

        /// <summary>Per-corner radii: x top-left, y top-right, z bottom-right, w bottom-left.</summary>
        public static void Round(Rect r, Color c, Vector4 radii) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Fade(c), Vector4.zero, radii);

        public static void Outline(Rect r, Color c, float width, float radius = 0f) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Fade(c), width, radius);

        public static void Disc(Vector2 center, float radius, Color c) =>
            Round(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), c, radius);

        public static void Ring(Vector2 center, float radius, float width, Color c) =>
            Outline(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), c, width, radius);

        /// <summary>A soft radial glow that fades out to the edge.</summary>
        public static void Glow(Vector2 center, float radius, Color c) =>
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), GlowTexture,
                ScaleMode.StretchToFill, true, 0f, Fade(c), 0f, 0f);

        /// <summary>The same soft glow stretched over `r` (an elliptical pool of light).</summary>
        public static void GlowRect(Rect r, Color c) =>
            GUI.DrawTexture(r, GlowTexture, ScaleMode.StretchToFill, true, 0f, Fade(c), 0f, 0f);

        /// <summary>A solid ellipse `width` x `height` (IndigoShapes' disc, stretched): eyes, lids, pools.</summary>
        public static void Ellipse(Vector2 center, float width, float height, Color c) =>
            Sprite(IndigoShapes.Disc, center, new Vector2(width, height), c, false, 0f);

        /// <summary>A square turned on its corner: `size` is its side, `degrees` turns it further.</summary>
        public static void Diamond(Vector2 center, float size, Color c, float degrees = 0f)
        {
            var prev = Rotate(center, 45f + degrees);
            Box(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), c);
            GUI.matrix = prev;
        }

        /// <summary>A diamond stretched to `width` x `height` (a gem, a lozenge): drawn as a squashed, turned square.</summary>
        public static void Lozenge(Vector2 center, float width, float height, Color c)
        {
            var prev = GUI.matrix;
            GUI.matrix = prev * Matrix4x4.TRS(center, Quaternion.identity, new Vector3(width / 1.41421f, height / 1.41421f, 1f))
                              * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 45f), Vector3.one);
            Box(new Rect(-0.5f, -0.5f, 1f, 1f), c);
            GUI.matrix = prev;
        }

        public static void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            var d = b - a;
            float length = d.magnitude;
            if (length < 0.01f) return;
            var prev = Rotate(a, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            Box(new Rect(a.x, a.y - width * 0.5f, length, width), c);
            GUI.matrix = prev;
        }

        /// <summary>Rotates everything drawn after it around `pivot`. Restore with GUI.matrix = (returned value).</summary>
        public static Matrix4x4 Rotate(Vector2 pivot, float degrees)
        {
            var prev = GUI.matrix;
            GUI.matrix = prev * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), Vector3.one)
                              * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            return prev;
        }

        static Texture2D GlowTexture
        {
            get
            {
                if (glow) return glow;
                const int size = 64;
                glow = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size, size) * 0.5f) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
                glow.SetPixels32(pixels);
                glow.Apply();
                return glow;
            }
        }

        // ---------- Sprites ----------

        /// <summary>
        /// Draws a sprite with its pivot at `anchor`, `unitPx` GUI pixels per world unit: the placement and size a
        /// SpriteRenderer would give it, at the art's own aspect ratio. Frames of one character share pixels-per-unit,
        /// so they get one scale and line up even when their canvases differ (idle 600x600, attack 800x600).
        /// Tint multiplies (black = silhouette).
        /// </summary>
        public static void Sprite(Sprite s, Vector2 anchor, float unitPx, Color tint, bool flipX = false, float degrees = 0f)
        {
            if (!s) return;
            float k = unitPx / s.pixelsPerUnit;
            var size = s.rect.size * k;
            var pivot = s.pivot * k; // from the rect's bottom-left
            float left = flipX ? anchor.x - (size.x - pivot.x) : anchor.x - pivot.x;
            var canvas = new Rect(left, anchor.y - (size.y - pivot.y), size.x, size.y);

            var prev = degrees != 0f ? Rotate(anchor, degrees) : GUI.matrix;
            DrawCanvas(s, canvas, tint, flipX);
            GUI.matrix = prev;
        }

        /// <summary>
        /// A sprite with its pivot at `anchor`, scaled separately along its own x and y (`unitPx` = GUI pixels per world
        /// unit on each axis) and turned by `degrees`. For runtime-built parts (Violet's king) that a SpriteRenderer
        /// would scale unevenly; the art itself never needs it.
        /// </summary>
        public static void Sprite(Sprite s, Vector2 anchor, Vector2 unitPx, Color tint, bool flipX, float degrees)
        {
            if (!s) return;
            var prev = GUI.matrix;
            GUI.matrix = prev * Matrix4x4.TRS(anchor, Quaternion.Euler(0f, 0f, degrees),
                new Vector3(unitPx.x / s.pixelsPerUnit, unitPx.y / s.pixelsPerUnit, 1f));
            var size = s.rect.size;
            var pivot = s.pivot;
            float left = flipX ? -(size.x - pivot.x) : -pivot.x;
            DrawCanvas(s, new Rect(left, -(size.y - pivot.y), size.x, size.y), tint, flipX);
            GUI.matrix = prev;
        }

        /// <summary>Same, but `feet` is the bottom of the canvas (our art stands on its canvas's bottom edge).</summary>
        public static void SpriteOnGround(Sprite s, Vector2 feet, float unitPx, Color tint, bool flipX = false, float degrees = 0f)
        {
            if (!s) return;
            Sprite(s, new Vector2(feet.x, feet.y - s.pivot.y * unitPx / s.pixelsPerUnit), unitPx, tint, flipX, degrees);
        }

        /// <summary>A prop (orb, shot) as big as fits in `box`, centered, at its own aspect ratio.</summary>
        public static void DrawSprite(Sprite s, Rect box, Color tint, bool flipX = false)
        {
            var r = s.rect;
            float k = Mathf.Min(box.width / r.width, box.height / r.height);
            var size = r.size * k;
            DrawCanvas(s, new Rect(box.center.x - size.x * 0.5f, box.center.y - size.y * 0.5f, size.x, size.y), tint, flipX);
        }

        /// <summary>
        /// `canvas` is where the sprite's whole rect goes (same aspect as sprite.rect). Unity trims the transparent
        /// border off imported sprites (Tight mesh), so textureRect is only the visible part: it's drawn at its own
        /// offset and size inside the canvas, never stretched over the whole canvas.
        /// </summary>
        static void DrawCanvas(Sprite s, Rect canvas, Color tint, bool flipX)
        {
            var tex = s.texture;
            var full = s.rect;
            var tr = s.textureRect;
            var offset = s.textureRectOffset; // visible part's bottom-left, from the full rect's bottom-left (pixels)
            float k = canvas.width / full.width;
            float x = flipX ? full.width - offset.x - tr.width : offset.x;
            var rect = new Rect(canvas.x + x * k, canvas.y + (full.height - offset.y - tr.height) * k, tr.width * k, tr.height * k);

            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            if (flipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            var prevColor = GUI.color;
            GUI.color = Fade(tint);
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
            GUI.color = prevColor;
        }

        // ---------- Text ----------

        static GUIStyle Style(int size, FontStyle fontStyle, TextAnchor align)
        {
            style ??= new GUIStyle(GUI.skin.label)
            {
                richText = true, wordWrap = false, clipping = TextClipping.Overflow,
                padding = new RectOffset(), margin = new RectOffset(),
            };
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.alignment = align;
            return style;
        }

        // Text is laid out in canvas units but rasterized in screen pixels: the font is set to its on-screen size and
        // drawn without the canvas scale, so glyphs stay sharp instead of being stretched from another size.
        // (So text can't be drawn inside Rotate; nothing needs that.)
        static float CanvasScale => Mathf.Max(0.01f, GUI.matrix.lossyScale.x);
        static int ScreenFontSize(int size, float scale) => Mathf.Max(1, Mathf.RoundToInt(size * scale));

        /// <summary>Size of `text` in canvas units.</summary>
        public static Vector2 Measure(string text, int size, FontStyle fontStyle = FontStyle.Normal)
        {
            float s = CanvasScale;
            return Style(ScreenFontSize(size, s), fontStyle, TextAnchor.MiddleLeft).CalcSize(new GUIContent(text)) / s;
        }

        public static void Text(Rect r, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, FontStyle fontStyle = FontStyle.Normal)
        {
            var canvas = GUI.matrix;
            float s = CanvasScale;
            Vector2 min = canvas.MultiplyPoint3x4(r.min), max = canvas.MultiplyPoint3x4(r.max);
            var st = Style(ScreenFontSize(size, s), fontStyle, align);
            // Same color in every state: GUI.Label draws the hover state under the mouse, and the skin's hover
            // color (near white) would otherwise make text change, or vanish on a light fill, when hovered.
            st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = c;
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Alpha); // also fades <color=...> runs, which ignore textColor
            GUI.matrix = Matrix4x4.identity;
            GUI.Label(Rect.MinMaxRect(Mathf.Round(min.x), Mathf.Round(min.y), Mathf.Round(max.x), Mathf.Round(max.y)), text, st);
            GUI.matrix = canvas;
            GUI.color = prevColor;
        }

        // ---------- Keys ----------

        static bool IsMouse(string key) => key is "LMB" or "RMB";

        static string KeyLabel(string key) => key switch
        {
            "Left" => "←", "Right" => "→", "Up" => "↑", "Down" => "↓",
            _ => key,
        };

        static int KeyFontSize(float height, string label, float labelScale) =>
            Mathf.RoundToInt(height * (label.Length <= 1 ? 0.5f : 0.36f) * labelScale);

        /// <summary>Width of a keycap `height` tall: square for one letter, wider for words, narrow for the mouse.</summary>
        /// <param name="labelScale">Bigger key text than usual (and a key wide enough for it).</param>
        public static float KeyWidth(string key, float height, float labelScale = 1f)
        {
            if (IsMouse(key)) return height * 0.74f;
            var label = KeyLabel(key);
            if (label.Length <= 1) return height;
            return Mathf.Max(height, Measure(label, KeyFontSize(height, label, labelScale), FontStyle.Bold).x + height * 0.55f);
        }

        /// <summary>A keycap (or mouse for LMB / RMB). press 0..1: sinks, darkens toward the accent and glows.</summary>
        public static void Key(Rect r, string key, float press, Color accent, float labelScale = 1f)
        {
            if (IsMouse(key)) { Mouse(r, key == "RMB", press, accent); return; }

            float depth = r.height * 0.13f;
            float radius = r.height * 0.2f;
            var face = new Rect(r.x, r.y + depth * press, r.width, r.height - depth);
            var dark = new Color(0.09f, 0.1f, 0.13f, 0.96f);

            if (press > 0f) Glow(face.center, r.height * 1.3f, new Color(accent.r, accent.g, accent.b, 0.55f * press));
            Round(new Rect(r.x, r.y + depth, r.width, r.height - depth), Color.Lerp(accent, Color.black, 0.6f), radius); // the side
            Round(face, Color.Lerp(dark, Color.Lerp(accent, Color.black, 0.35f), press), radius);
            Outline(face, Color.Lerp(accent, Color.white, 0.3f * press), Mathf.Max(1.5f, r.height * 0.05f), radius);
            var label = KeyLabel(key);
            Text(face, label, KeyFontSize(r.height, label, labelScale), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        static void Mouse(Rect r, bool right, float press, Color accent)
        {
            float radius = r.width * 0.5f;
            float splitY = r.y + r.height * 0.44f;
            float stroke = Mathf.Max(1.5f, r.height * 0.045f);
            var dark = new Color(0.09f, 0.1f, 0.13f, 0.96f);

            var button = new Rect(right ? r.center.x : r.x, r.y, r.width * 0.5f, splitY - r.y);
            if (press > 0f) Glow(button.center, r.height * 1.1f, new Color(accent.r, accent.g, accent.b, 0.55f * press));
            Round(r, dark, radius);
            // The button that matters is always lit a little, and fully while pressed.
            var lit = Color.Lerp(new Color(accent.r, accent.g, accent.b, 0.45f), Color.Lerp(accent, Color.white, 0.25f), press);
            Round(button, lit, right ? new Vector4(0f, radius, 0f, 0f) : new Vector4(radius, 0f, 0f, 0f));
            Outline(r, accent, stroke, radius);
            Box(new Rect(r.x, splitY - stroke * 0.5f, r.width, stroke), accent);
            Box(new Rect(r.center.x - stroke * 0.5f, r.y, stroke, splitY - r.y), accent);
        }

        /// <summary>Keycaps side by side, centered on `center`, with a small "/" between them.</summary>
        public static void KeyRow(IReadOnlyList<string> keys, Vector2 center, float height, Color accent, IReadOnlyDictionary<string, float> press)
        {
            if (keys == null || keys.Count == 0) return;
            float gap = height * 0.55f, width = gap * (keys.Count - 1);
            foreach (var k in keys) width += KeyWidth(k, height);
            float x = center.x - width * 0.5f;
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                {
                    Text(new Rect(x, center.y - height * 0.5f, gap, height), "/", Mathf.RoundToInt(height * 0.45f), new Color(0.4f, 0.4f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    x += gap;
                }
                float w = KeyWidth(keys[i], height);
                Key(new Rect(x, center.y - height * 0.5f, w, height), keys[i], PressOf(press, keys[i]), accent);
                x += w;
            }
        }

        // ---------- Inline "text [Key] text" lines ----------

        /// <summary>Width of a line with key tokens, for right-aligning.</summary>
        public static float InlineWidth(string line, int fontSize, float keyScale = 1.3f, float labelScale = 1f) =>
            Inline(line, Vector2.zero, fontSize, Color.white, Color.white, null, false, keyScale, labelScale);

        /// <summary>Draws a one-line caption centered on `center`; [Key] tokens become keycaps (pressed in sync with `press`).</summary>
        /// <param name="keyScale">Keycap height as a multiple of the font size.</param>
        /// <param name="labelScale">Text inside the keycaps, relative to the usual size.</param>
        public static float Inline(string line, Vector2 center, int fontSize, Color color, Color accent, IReadOnlyDictionary<string, float> press,
                                   bool draw = true, float keyScale = 1.3f, float labelScale = 1f)
        {
            if (string.IsNullOrEmpty(line)) return 0f;
            float keyHeight = fontSize * keyScale, pad = fontSize * 0.2f;

            float width = 0f;
            foreach (var part in Split(line))
                width += part.isKey ? KeyWidth(part.text, keyHeight, labelScale) + pad * 2f : Measure(part.text, fontSize).x;
            if (!draw) return width;

            float x = center.x - width * 0.5f;
            foreach (var part in Split(line))
            {
                if (part.isKey)
                {
                    float w = KeyWidth(part.text, keyHeight, labelScale);
                    Key(new Rect(x + pad, center.y - keyHeight * 0.5f, w, keyHeight), part.text, PressOf(press, part.text), accent, labelScale);
                    x += w + pad * 2f;
                }
                else
                {
                    float w = Measure(part.text, fontSize).x;
                    Text(new Rect(x, center.y - fontSize, w + 2f, fontSize * 2f), part.text, fontSize, color, TextAnchor.MiddleLeft);
                    x += w;
                }
            }
            return width;
        }

        static IEnumerable<(string text, bool isKey)> Split(string line)
        {
            int at = 0;
            foreach (Match m in KeyToken.Matches(line))
            {
                if (m.Index > at) yield return (line.Substring(at, m.Index - at), false);
                yield return (m.Groups[1].Value.Trim(), true);
                at = m.Index + m.Length;
            }
            if (at < line.Length) yield return (line.Substring(at), false);
        }

        static float PressOf(IReadOnlyDictionary<string, float> press, string key) =>
            press != null && press.TryGetValue(key, out var p) ? p : 0f;
    }
}
