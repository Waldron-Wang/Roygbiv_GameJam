using UnityEngine;

namespace Roygbiv
{
    /// <summary>Abstract outlines the Indigo boss's body can take. All are closed curves about one unit across.</summary>
    public enum IndigoShape
    {
        Diamond,     // a square on its corner
        Circle,
        Triangle,
        Infinity,    // Lissajous 1:2, mirror-symmetric both ways
        Knot,        // Lissajous 3:2, a weave that crosses itself
        Ripple,      // a ring with a standing wave on it
        Cusps,       // hypocycloid: five points, sides bowed inward
        Heptagram,   // a seven-point star polygon {7/3}
        Spirograph,  // hypotrochoid loops
    }

    /// <summary>
    /// The Indigo boss's body as line art: one glowing closed line traced through IndigoShape curves.
    /// Every shape is sampled to the same number of points, so a morph just slides each point to its new place,
    /// passing through tangled in-between forms. Chaos frays the line (wobble, spikes, slipped segments) and
    /// Echoes draws fading copies of where the line was a moment ago.
    /// The boss drives Morph / Chaos / Echoes and calls Draw every frame; this only draws.
    /// </summary>
    public class IndigoLineBody : MonoBehaviour
    {
        const int Count = 160;
        const int Snapshots = 32;
        const float EchoLag = 0.07f;

        readonly Vector3[] from = new Vector3[Count], to = new Vector3[Count], bare = new Vector3[Count], drawn = new Vector3[Count];
        readonly Vector3[][] history = new Vector3[Snapshots][];
        readonly float[] historyTime = new float[Snapshots];
        int historyHead;

        LineRenderer core, glow;
        LineRenderer[] echoLines = new LineRenderer[0];
        int order;
        float width, seed;

        /// <summary>The shape it is turning into (or is).</summary>
        public IndigoShape Shape { get; private set; }
        /// <summary>0 = still the old shape, 1 = the new one.</summary>
        public float Morph { get; set; } = 1f;
        /// <summary>0 = a clean line, 1 = barely holding together.</summary>
        public float Chaos { get; set; }
        /// <summary>Fading copies of the line trailing behind it (0-4).</summary>
        public int Echoes { get; set; }

        public static IndigoLineBody Create(Transform parent, IndigoShape shape, float width, int order)
        {
            var go = new GameObject("LineBody");
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<IndigoLineBody>();
            body.order = order;
            body.width = width;
            body.seed = Random.Range(0f, 100f);
            body.glow = MakeLine("Glow", go.transform, false, width * 4f, order - 1);
            body.core = MakeLine("Core", go.transform, false, width, order);
            for (int i = 0; i < Snapshots; i++) body.history[i] = new Vector3[Count];
            body.historyTime[0] = float.NegativeInfinity;
            Sample(shape, body.to);
            body.to.CopyTo(body.from, 0);
            body.to.CopyTo(body.bare, 0); // a morph can start before the first Draw
            body.Shape = shape;
            return body;
        }

        /// <summary>Starts turning into `shape` from wherever the line is now. Drive Morph from 0 to 1 to get there.</summary>
        public void MorphTo(IndigoShape shape)
        {
            bare.CopyTo(from, 0);
            Sample(shape, to);
            Shape = shape;
            Morph = 0f;
        }

        /// <summary>Copies another body's shape and morph exactly (the boss's illusions).</summary>
        public void CopyFrom(IndigoLineBody other)
        {
            other.from.CopyTo(from, 0);
            other.to.CopyTo(to, 0);
            other.bare.CopyTo(bare, 0);
            Shape = other.Shape;
            Morph = other.Morph;
            Chaos = other.Chaos;
            Echoes = other.Echoes;
        }

        /// <summary>Rebuilds the line for this frame. Call after the transform is set (LateUpdate).</summary>
        public void Draw(Color coreColor, Color glowColor, float alpha)
        {
            float m = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Morph));
            float chaos = Mathf.Clamp01(Chaos), t = Time.time;
            for (int i = 0; i < Count; i++)
            {
                var b = Vector3.LerpUnclamped(from[i], to[i], m);
                bare[i] = b;
                drawn[i] = chaos > 0.001f ? Fray(b, i, chaos, t) : b;
            }
            if (chaos > 0.4f && Random.value < chaos * 0.2f) SlipSegment(chaos);

            core.SetPositions(drawn);
            glow.SetPositions(drawn);
            SetColor(core, coreColor, alpha);
            SetColor(glow, glowColor, alpha * 0.35f);
            DrawEchoes(glowColor, alpha);
        }

        /// <summary>Wobble along and across the line, plus the odd point flung outward like a spike.</summary>
        Vector3 Fray(Vector3 b, int i, float chaos, float t)
        {
            float u = (float)i / Count;
            var radial = b.sqrMagnitude > 1e-6f ? b.normalized : Vector3.up;
            var tangent = new Vector3(-radial.y, radial.x, 0f);
            float along = Mathf.PerlinNoise(u * 9f + seed, t * 2.7f) - 0.5f;
            float across = Mathf.PerlinNoise(u * 23f + seed * 2f, t * 6.1f) - 0.5f;
            float spike = i % 11 == 0 ? Mathf.Max(0f, Mathf.PerlinNoise(t * 9f, i + seed) - 0.4f) * 2.2f : 0f;
            return b + radial * ((along * 0.9f + spike) * chaos) + tangent * (across * 0.5f * chaos);
        }

        /// <summary>A run of the line jumps sideways for a frame: a glitch in the shape itself.</summary>
        void SlipSegment(float chaos)
        {
            int start = Random.Range(0, Count), length = Random.Range(8, 26);
            var shift = (Vector3)(Random.insideUnitCircle * 0.45f * chaos);
            for (int k = 0; k < length; k++) drawn[(start + k) % Count] += shift;
        }

        void DrawEchoes(Color color, float alpha)
        {
            // Remember where the line is in the world, so echoes trail its movement as well as its shape.
            historyHead = (historyHead + 1) % Snapshots;
            var snap = history[historyHead];
            for (int i = 0; i < Count; i++) snap[i] = transform.TransformPoint(drawn[i]);
            historyTime[historyHead] = Time.time;

            int echoes = Mathf.Clamp(Echoes, 0, 4);
            if (echoLines.Length < echoes)
            {
                var grown = new LineRenderer[echoes];
                echoLines.CopyTo(grown, 0);
                for (int j = echoLines.Length; j < echoes; j++) grown[j] = MakeLine("Echo", transform, true, width * 0.8f, order - 1);
                echoLines = grown;
            }
            for (int j = 0; j < echoLines.Length; j++)
            {
                bool on = j < echoes;
                echoLines[j].enabled = on;
                if (!on) continue;
                echoLines[j].SetPositions(history[SnapshotAt(Time.time - (j + 1) * EchoLag)]);
                SetColor(echoLines[j], color, alpha * 0.55f / (j + 1));
            }
        }

        int SnapshotAt(float time)
        {
            int best = historyHead;
            for (int k = 1; k < Snapshots; k++)
            {
                int idx = (historyHead - k + Snapshots) % Snapshots;
                if (historyTime[idx] < time) break;
                best = idx;
            }
            return best;
        }

        static void SetColor(LineRenderer line, Color c, float alpha)
        {
            c.a *= alpha;
            line.startColor = line.endColor = c;
        }

        static LineRenderer MakeLine(string name, Transform parent, bool worldSpace, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = worldSpace;
            line.loop = true;
            line.positionCount = Count;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = IndigoShapes.LineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = order;
            return line;
        }

        // ---------- Shapes ----------

        /// <summary>Fills `points` with the shape, scaled so its farthest point is one unit out.</summary>
        public static void Sample(IndigoShape shape, Vector3[] points)
        {
            int n = points.Length;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n, a = u * 2f * Mathf.PI;
                points[i] = shape switch
                {
                    IndigoShape.Circle => new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                    IndigoShape.Infinity => new Vector2(Mathf.Sin(a) * 1.25f, Mathf.Sin(2f * a) * 0.6f),
                    IndigoShape.Knot => new Vector2(Mathf.Cos(3f * a), Mathf.Sin(2f * a)),
                    IndigoShape.Ripple => FromPolar(a + Mathf.PI * 0.5f, 0.78f + 0.22f * Mathf.Sin(7f * a)),
                    IndigoShape.Cusps => Hypocycloid(a - Mathf.PI * 0.5f),
                    IndigoShape.Spirograph => Hypotrochoid(a * 2f),
                    IndigoShape.Triangle => Polygon(u, Star(3, 1, -90f)),
                    IndigoShape.Heptagram => Polygon(u, Star(7, 3, 90f)),
                    _ => Polygon(u, Star(4, 1, 90f)), // Diamond
                };
            }

            float far = 0f;
            foreach (var p in points) far = Mathf.Max(far, p.magnitude);
            if (far > 0f) for (int i = 0; i < n; i++) points[i] /= far;
        }

        static Vector2 FromPolar(float angle, float radius) => new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

        /// <summary>Five cusps, sides curving inward. A cusp points along `a` = 0.</summary>
        static Vector2 Hypocycloid(float a) =>
            new(4f * Mathf.Cos(a) + Mathf.Cos(4f * a), 4f * Mathf.Sin(a) - Mathf.Sin(4f * a));

        /// <summary>Loops inside a ring; closes after two turns, so `a` runs over 0..4π.</summary>
        static Vector2 Hypotrochoid(float a) =>
            new(5f * Mathf.Cos(a) + 1.6f * Mathf.Cos(2.5f * a), 5f * Mathf.Sin(a) - 1.6f * Mathf.Sin(2.5f * a));

        /// <summary>Corners of a star polygon {points/step} (step 1 = a plain polygon), starting at `startDegrees`.</summary>
        static Vector2[] Star(int points, int step, float startDegrees)
        {
            var corners = new Vector2[points];
            for (int i = 0; i < points; i++)
                corners[i] = FromPolar((startDegrees + (i * step % points) * 360f / points) * Mathf.Deg2Rad, 1f);
            return corners;
        }

        /// <summary>A point `u` (0..1) of the way around the closed path through `corners`, evenly spaced by length.</summary>
        static Vector2 Polygon(float u, Vector2[] corners)
        {
            float total = 0f;
            for (int i = 0; i < corners.Length; i++) total += Vector2.Distance(corners[i], corners[(i + 1) % corners.Length]);
            float d = u * total;
            for (int i = 0; i < corners.Length; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Length];
                float len = Vector2.Distance(a, b);
                if (d <= len) return Vector2.Lerp(a, b, len > 0f ? d / len : 0f);
                d -= len;
            }
            return corners[0];
        }
    }
}
