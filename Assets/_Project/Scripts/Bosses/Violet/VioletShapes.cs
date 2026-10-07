using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Placeholder sprites for Violet made at runtime: polygons (in world units) rasterized to white, anti-aliased
    /// textures with a soft top-lit shade, tinted by the SpriteRenderer. The king's parts (crown, helm, armor, cape,
    /// greatsword) and his attacks (crescent waves, needles, spectral swords) are all built from these, so the
    /// silhouette reads with no art at all. Art can replace any of them later without touching the timing.
    /// Each shape is made once per session and cached.
    /// </summary>
    public static class VioletShapes
    {
        const int PixelsPerUnit = 64;
        const int MaxSize = 512;

        static readonly Dictionary<string, Sprite> Cache = new();
        static readonly List<float> Crossings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Cache.Clear();

        // ---------- Violet palette (deep #3A1A5C up to #C266FF, magic glow #E2B8FF) ----------
        public static readonly Color Deep = new(0.227f, 0.102f, 0.361f);
        public static readonly Color Dark = new(0.33f, 0.16f, 0.5f);
        public static readonly Color Mid = new(0.5f, 0.25f, 0.74f);
        public static readonly Color Bright = new(0.76f, 0.4f, 1f);
        public static readonly Color Glow = new(0.886f, 0.722f, 1f);
        public static readonly Color Steel = new(0.85f, 0.78f, 0.98f);

        /// <summary>A sprite renderer for a shape, at `localPosition` under `parent`.</summary>
        public static SpriteRenderer Create(string name, Sprite sprite, Transform parent, Vector2 localPosition, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// Rasterizes one or more polygons (world units) into one sprite. `pivot` is in the same units.
        /// shade: 0 = flat white, 0.3 = 30% darker at the bottom than the top.
        /// </summary>
        public static Sprite Polygons(string key, Vector2 pivot, float shade, params Vector2[][] polygons)
        {
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;

            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var poly in polygons)
                foreach (var p in poly) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            const float pad = 2f / PixelsPerUnit;
            min -= Vector2.one * pad;
            max += Vector2.one * pad;
            int w = Mathf.Clamp(Mathf.CeilToInt((max.x - min.x) * PixelsPerUnit), 4, MaxSize);
            int h = Mathf.Clamp(Mathf.CeilToInt((max.y - min.y) * PixelsPerUnit), 4, MaxSize);
            float ppuX = w / (max.x - min.x), ppuY = h / (max.y - min.y);

            var coverage = new float[w * h];
            const int sub = 4;
            foreach (var poly in polygons)
            {
                var layer = new float[w * h];
                for (int py = 0; py < h; py++)
                for (int s = 0; s < sub; s++)
                {
                    float y = min.y + (py + (s + 0.5f) / sub) / ppuY;
                    Crossings.Clear();
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    {
                        Vector2 a = poly[i], b = poly[j];
                        if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                            Crossings.Add(a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
                    }
                    Crossings.Sort();
                    for (int c = 0; c + 1 < Crossings.Count; c += 2)
                    {
                        float x0 = (Crossings[c] - min.x) * ppuX, x1 = (Crossings[c + 1] - min.x) * ppuX;
                        for (int px = Mathf.Max(0, Mathf.FloorToInt(x0)); px < Mathf.Min(w, Mathf.CeilToInt(x1)); px++)
                        {
                            float overlap = Mathf.Min(px + 1f, x1) - Mathf.Max(px, x0);
                            if (overlap > 0f) layer[py * w + px] += overlap / sub;
                        }
                    }
                }
                for (int i = 0; i < coverage.Length; i++) coverage[i] = Mathf.Max(coverage[i], layer[i]);
            }

            var pixels = new Color32[w * h];
            for (int py = 0; py < h; py++)
            {
                byte v = (byte)(255f * Mathf.Lerp(1f - shade, 1f, (py + 0.5f) / h));
                for (int px = 0; px < w; px++)
                    pixels[py * w + px] = new Color32(v, v, v, (byte)(255f * Mathf.Clamp01(coverage[py * w + px])));
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Violet_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(pixels);
            tex.Apply();
            var pivot01 = new Vector2((pivot.x - min.x) / (max.x - min.x), (pivot.y - min.y) / (max.y - min.y));
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, (ppuX + ppuY) * 0.5f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Polygon(string key, Vector2 pivot, float shade, params Vector2[] points) => Polygons(key, pivot, shade, points);

        static Vector2[] Ellipse(Vector2 center, Vector2 radius, int steps = 20, float from = 0f, float to = 360f)
        {
            var pts = new Vector2[steps];
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)(Mathf.Approximately(to - from, 360f) ? steps : steps - 1)) * Mathf.Deg2Rad;
                pts[i] = center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
            }
            return pts;
        }

        // ---------- The king (facing right; arms and swords hang along -y from their pivot) ----------

        /// <summary>Greatsword blade from the hand (pivot) down to the point: long, wide at the base.</summary>
        public static Sprite Blade => Polygon("Blade", Vector2.zero, 0.15f,
            new(-0.17f, -0.2f), new(0.17f, -0.2f), new(0.14f, -2.5f), new(0f, -2.85f), new(-0.14f, -2.5f));

        /// <summary>A bright line down the middle of the blade (the telegraph glow runs along it).</summary>
        public static Sprite BladeEdge => Polygon("BladeEdge", Vector2.zero, 0f,
            new(-0.045f, -0.25f), new(0.045f, -0.25f), new(0.03f, -2.5f), new(0f, -2.7f), new(-0.03f, -2.5f));

        /// <summary>Wide crossguard, grip and pommel, pivot at the hand.</summary>
        public static Sprite Hilt => Polygons("Hilt", Vector2.zero, 0.25f,
            new Vector2[] { new(-0.55f, -0.08f), new(-0.4f, -0.02f), new(0.4f, -0.02f), new(0.55f, -0.08f), new(0.42f, -0.24f), new(-0.42f, -0.24f) },
            new Vector2[] { new(-0.07f, 0.28f), new(0.07f, 0.28f), new(0.07f, -0.04f), new(-0.07f, -0.04f) },
            Ellipse(new Vector2(0f, 0.33f), new Vector2(0.11f, 0.11f), 10));

        /// <summary>One whole arm, shoulder (pivot) to gauntlet.</summary>
        public static Sprite Arm => Polygon("Arm", Vector2.zero, 0.3f,
            new(-0.2f, 0.08f), new(0.2f, 0.08f), new(0.18f, -0.5f), new(0.15f, -0.82f), new(0.2f, -0.86f), new(0.19f, -1.08f),
            new(-0.19f, -1.08f), new(-0.2f, -0.86f), new(-0.15f, -0.82f), new(-0.18f, -0.5f));

        /// <summary>An armored leg, hip (pivot) to the boot, toe forward.</summary>
        public static Sprite Leg => Polygon("Leg", Vector2.zero, 0.35f,
            new(-0.22f, 0.05f), new(0.22f, 0.05f), new(0.19f, -0.62f), new(0.16f, -0.72f), new(0.17f, -1.18f),
            new(0.42f, -1.26f), new(0.44f, -1.38f), new(-0.2f, -1.38f), new(-0.18f, -0.72f), new(-0.2f, -0.62f));

        /// <summary>Broad-shouldered breastplate over a narrow waist, waist (pivot) up to the neck, with the armored skirt below.</summary>
        public static Sprite Torso => Polygons("Torso", Vector2.zero, 0.3f,
            new Vector2[] { new(-0.42f, 0f), new(0.42f, 0f), new(0.62f, 0.62f), new(0.72f, 1.02f), new(0.3f, 1.16f), new(-0.3f, 1.16f), new(-0.72f, 1.02f), new(-0.62f, 0.62f) },
            new Vector2[] { new(-0.46f, 0.06f), new(0.46f, 0.06f), new(0.58f, -0.5f), new(-0.58f, -0.5f) });

        /// <summary>A round, layered shoulder plate, pivot at its center.</summary>
        public static Sprite Pauldron => Polygons("Pauldron", Vector2.zero, 0.25f,
            Ellipse(new Vector2(0f, 0.02f), new Vector2(0.42f, 0.3f), 22, 0f, 180f),
            new Vector2[] { new(-0.42f, 0.04f), new(0.42f, 0.04f), new(0.36f, -0.16f), new(-0.36f, -0.16f) });

        /// <summary>A closed helm, pivot at the neck.</summary>
        public static Sprite Helm => Polygons("Helm", Vector2.zero, 0.25f,
            new Vector2[] { new(-0.24f, 0f), new(0.27f, 0f), new(0.3f, 0.28f), new(0.24f, 0.52f), new(0f, 0.6f), new(-0.24f, 0.52f), new(-0.3f, 0.28f) });

        /// <summary>The glowing visor slit, pivot at its center.</summary>
        public static Sprite Visor => Polygon("Visor", Vector2.zero, 0f,
            new(-0.06f, 0.035f), new(0.26f, 0.05f), new(0.24f, -0.035f), new(-0.06f, -0.035f));

        /// <summary>A pointed royal crown, five points (the middle one tallest), pivot at the bottom center.</summary>
        public static Sprite Crown => Polygon("Crown", Vector2.zero, 0.15f,
            new(-0.34f, 0f), new(0.34f, 0f), new(0.36f, 0.16f), new(0.38f, 0.42f), new(0.24f, 0.22f), new(0.16f, 0.56f),
            new(0.07f, 0.24f), new(0f, 0.7f), new(-0.07f, 0.24f), new(-0.16f, 0.56f), new(-0.24f, 0.22f), new(-0.38f, 0.42f), new(-0.36f, 0.16f));

        /// <summary>A small cut gem / glint diamond, pivot at its center.</summary>
        public static Sprite Gem => Polygon("Gem", Vector2.zero, 0f, new(0f, 0.5f), new(0.35f, 0f), new(0f, -0.5f), new(-0.35f, 0f));

        /// <summary>A four-pointed sparkle (the glint on the crown), 1 unit across.</summary>
        public static Sprite Glint => Polygon("Glint", Vector2.zero, 0f,
            new(0f, 0.5f), new(0.07f, 0.07f), new(0.5f, 0f), new(0.07f, -0.07f), new(0f, -0.5f), new(-0.07f, -0.07f), new(-0.5f, 0f), new(-0.07f, 0.07f));

        /// <summary>One cape segment, pivot at the top center: `top` wide at the top, `bottom` wide at the bottom, `length` long.</summary>
        public static Sprite CapeSegment(float top, float bottom, float length, bool tattered)
        {
            string key = $"Cape_{top:0.00}_{bottom:0.00}_{length:0.00}_{tattered}";
            if (!tattered)
                return Polygon(key, Vector2.zero, 0.2f, new(-top * 0.5f, 0.04f), new(top * 0.5f, 0.04f), new(bottom * 0.5f, -length), new(-bottom * 0.5f, -length));
            float b = bottom * 0.5f;
            return Polygon(key, Vector2.zero, 0.2f,
                new(-top * 0.5f, 0.04f), new(top * 0.5f, 0.04f), new(b, -length), new(b * 0.55f, -length * 0.82f), new(b * 0.15f, -length * 1.05f),
                new(-b * 0.25f, -length * 0.8f), new(-b * 0.6f, -length * 1.02f), new(-b, -length * 0.85f));
        }

        // ---------- Attacks ----------

        /// <summary>A crescent slash, 1 unit tall, bulging toward -x (flip it for waves going right). Pivot at its center.</summary>
        public static Sprite Crescent
        {
            get
            {
                if (Cache.TryGetValue("Crescent", out var s) && s) return s;
                const int steps = 18;
                var pts = new Vector2[steps * 2];
                for (int i = 0; i < steps; i++)
                {
                    float t = Mathf.Lerp(-1f, 1f, i / (steps - 1f));
                    pts[i] = new Vector2(-0.45f * (1f - t * t), t * 0.5f);                          // outer edge
                    pts[steps * 2 - 1 - i] = new Vector2(-0.12f * (1f - t * t) + 0.02f, t * 0.47f); // inner edge
                }
                return Polygon("Crescent", Vector2.zero, 0f, pts);
            }
        }

        /// <summary>A needle along +x, 1 unit long, pivot at its center.</summary>
        public static Sprite Needle => Polygon("Needle", Vector2.zero, 0f,
            new(-0.5f, 0f), new(-0.2f, 0.09f), new(0.3f, 0.06f), new(0.5f, 0f), new(0.3f, -0.06f), new(-0.2f, -0.09f));

        /// <summary>A whole spectral sword pointing down (tip at -y), 2 units long, pivot at its center.</summary>
        public static Sprite SpectralSword => Polygons("SpectralSword", Vector2.zero, 0.1f,
            new Vector2[] { new(-0.11f, 0.55f), new(0.11f, 0.55f), new(0.09f, -0.75f), new(0f, -1f), new(-0.09f, -0.75f) },
            new Vector2[] { new(-0.34f, 0.62f), new(0.34f, 0.62f), new(0.28f, 0.52f), new(-0.28f, 0.52f) },
            new Vector2[] { new(-0.05f, 0.95f), new(0.05f, 0.95f), new(0.05f, 0.6f), new(-0.05f, 0.6f) });

        /// <summary>A sharp triangular shard (crystal debris, shattered swords), pivot at its center.</summary>
        public static Sprite Shard => Polygon("Shard", Vector2.zero, 0.2f, new(-0.3f, -0.25f), new(0.35f, -0.1f), new(-0.05f, 0.4f));

        /// <summary>A tall faceted crystal, 1 x 1 unit (scale it), pivot at the bottom center.</summary>
        public static Sprite Crystal => Polygon("Crystal", Vector2.zero, 0.35f,
            new(-0.5f, 0f), new(0.5f, 0f), new(0.46f, 0.82f), new(0.2f, 1f), new(-0.12f, 0.95f), new(-0.46f, 0.78f));
    }
}
