using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The fair warning for an attack. Four kinds:
    ///   Edge: a glowing bar pinned to the RIGHT EDGE of the screen at the exact height band the attack will travel,
    ///         with chevrons pointing in and a faint lane across the screen, brightening as it charges.
    ///   Top:  a band along the top of the screen over a stretch of the course (something will rain down there).
    ///   Spot: a pulsing marker at a fixed place in the world (a ripple or a sword is about to come from here).
    ///   Lane: a faint band with bright edges across a stretch at a height band (a wave or lunge will sweep through here).
    /// It charges for `time` seconds (scaled: Serenity slows it like the attack) and then removes itself.
    /// Runs late so it sticks to the camera after the camera has moved this frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class VioletTelegraph : MonoBehaviour
    {
        enum Kind { Edge, Top, Spot, Lane }

        Kind kind;
        SpriteRenderer bar, lane, glyph, edge2;
        SpriteRenderer[] chevrons;
        Color color;
        float y0, y1, x0, x1, time, age;
        Vector2 spot, size;

        public static VioletTelegraph Edge(float bottom, float top, float time, Color color)
        {
            var t = Make("TelegraphEdge", Kind.Edge, time, color);
            t.y0 = bottom;
            t.y1 = top;
            t.chevrons = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                t.chevrons[i] = VioletShapes.Create("Chevron", VioletShapes.Gem, t.transform, Vector2.zero, color, 61);
                t.chevrons[i].transform.localScale = new Vector3(0.45f, 0.7f, 1f);
            }
            t.LateUpdate();
            return t;
        }

        public static VioletTelegraph Top(float fromX, float toX, float time, Color color)
        {
            var t = Make("TelegraphTop", Kind.Top, time, color);
            t.x0 = fromX;
            t.x1 = toX;
            t.LateUpdate();
            return t;
        }

        public static VioletTelegraph Spot(Vector2 at, Vector2 size, float time, Color color)
        {
            var t = Make("TelegraphSpot", Kind.Spot, time, color);
            t.spot = at;
            t.size = size;
            t.LateUpdate();
            return t;
        }

        public static VioletTelegraph Lane(float fromX, float toX, float bottom, float top, float time, Color color)
        {
            var t = Make("TelegraphLane", Kind.Lane, time, color);
            t.x0 = Mathf.Min(fromX, toX);
            t.x1 = Mathf.Max(fromX, toX);
            t.y0 = bottom;
            t.y1 = top;
            t.edge2 = FlatSprite.Create("Edge2", t.transform, Vector2.zero, Vector2.one, color, 60);
            t.LateUpdate();
            return t;
        }

        static VioletTelegraph Make(string name, Kind kind, float time, Color color)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<VioletTelegraph>();
            t.kind = kind;
            t.time = Mathf.Max(0.05f, time);
            t.color = color;
            t.bar = FlatSprite.Create("Bar", go.transform, Vector2.zero, Vector2.one, color, 60);
            t.lane = FlatSprite.Create("Lane", go.transform, Vector2.zero, Vector2.one, Color.clear, 3);
            t.glyph = IndigoShapes.Create("Glyph", IndigoShapes.Ring, go.transform, Vector2.zero, 1f, color, 61);
            return t;
        }

        /// <summary>Removes it early (the attack was called off).</summary>
        public void Cancel()
        {
            if (this) Destroy(gameObject);
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= time) { Destroy(gameObject); return; }
            float k = age / time;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * Mathf.Lerp(10f, 40f, k));
            float a = Mathf.Lerp(0.35f, 1f, k) * (0.7f + 0.3f * pulse);
            var cam = Camera.main;

            switch (kind)
            {
                case Kind.Edge:
                {
                    if (!cam) return;
                    float half = cam.orthographicSize * cam.aspect;
                    float right = cam.transform.position.x + half;
                    float x = right - 0.35f;
                    float h = Mathf.Max(0.2f, y1 - y0), cy = (y0 + y1) * 0.5f;
                    Place(bar, new Vector2(x, cy), new Vector2(0.28f + 0.25f * k, h), new Color(color.r, color.g, color.b, a));
                    Place(lane, new Vector2(x - half, cy), new Vector2(half * 2f, h), new Color(color.r, color.g, color.b, 0.07f + 0.1f * k));
                    glyph.transform.position = new Vector2(x - 0.6f, cy);
                    glyph.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.7f, k);
                    glyph.color = new Color(1f, 1f, 1f, a * 0.8f);
                    for (int i = 0; i < chevrons.Length; i++)
                    {
                        float slide = Mathf.Repeat(age * 3f + i / 3f, 1f);
                        chevrons[i].transform.position = new Vector2(x - 1f - slide * 2.2f, cy);
                        chevrons[i].color = new Color(color.r, color.g, color.b, a * (1f - slide));
                    }
                    break;
                }
                case Kind.Top:
                {
                    float top = cam ? cam.transform.position.y + cam.orthographicSize : 10f;
                    float w = x1 - x0;
                    Place(bar, new Vector2((x0 + x1) * 0.5f, top - 0.3f), new Vector2(w, 0.25f + 0.35f * k), new Color(color.r, color.g, color.b, a));
                    Place(lane, new Vector2((x0 + x1) * 0.5f, top - 3f), new Vector2(w, 5.4f), new Color(color.r, color.g, color.b, 0.05f + 0.12f * k));
                    glyph.color = Color.clear;
                    break;
                }
                case Kind.Lane:
                {
                    float w = x1 - x0, cx = (x0 + x1) * 0.5f;
                    var edge = new Color(color.r, color.g, color.b, a * 0.9f);
                    Place(lane, new Vector2(cx, (y0 + y1) * 0.5f), new Vector2(w, y1 - y0), new Color(color.r, color.g, color.b, 0.06f + 0.16f * k));
                    Place(bar, new Vector2(cx, y1), new Vector2(w, 0.06f + 0.06f * k), edge);
                    Place(edge2, new Vector2(cx, y0), new Vector2(w, 0.06f + 0.06f * k), edge);
                    glyph.color = Color.clear;
                    break;
                }
                case Kind.Spot:
                    Place(bar, spot, new Vector2(size.x, size.y * (0.6f + 0.4f * pulse)), new Color(color.r, color.g, color.b, a));
                    lane.color = Color.clear;
                    glyph.transform.position = spot;
                    glyph.transform.localScale = Vector3.one * Mathf.Lerp(size.x * 2.5f, size.x * 0.8f, k);
                    glyph.color = new Color(color.r, color.g, color.b, a * 0.7f);
                    break;
            }
        }

        static void Place(SpriteRenderer sr, Vector2 center, Vector2 scale, Color c)
        {
            sr.transform.position = center;
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            sr.color = c;
        }
    }
}
