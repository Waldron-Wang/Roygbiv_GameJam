using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The spell circle the Indigo boss draws while it casts its next curse: two counter-turning rings,
    /// orbiting runes and motes of light drawn in from all around, with the curse's name and what it does
    /// spelled out underneath. It charges up as Charge goes 0 -> 1, then Release() flings it outward and fades it.
    /// Placeholder look made of runtime shapes; art can replace it without touching the boss's timing.
    /// </summary>
    public class IndigoSigil : MonoBehaviour
    {
        const int Runes = 6;
        const float RuneRadius = 2.1f, MoteRange = 7f, ReleaseTime = 0.5f;

        readonly List<(SpriteRenderer sr, Vector2 from, float age, float life)> motes = new();
        SpriteRenderer outer, inner, glow, plate;
        readonly SpriteRenderer[] runes = new SpriteRenderer[Runes];
        TextMesh[] texts;
        Color accent;
        float age, spin, nextMote, released = -1f;

        /// <summary>0 = just started, 1 = ready to release. The boss sets it every frame.</summary>
        public float Charge { get; set; }

        public static IndigoSigil Spawn(Transform follow, Color accent, string title, string hint, float labelOffset)
        {
            var go = new GameObject("IndigoSigil");
            go.transform.SetParent(follow, false);
            var s = go.AddComponent<IndigoSigil>();
            s.accent = accent;

            var faint = new Color(accent.r, accent.g, accent.b, 0f);
            s.glow = IndigoShapes.Create("Glow", IndigoShapes.Disc, go.transform, Vector2.zero, 0.1f, faint, 3);
            s.outer = IndigoShapes.Create("Outer", IndigoShapes.ThinRing, go.transform, Vector2.zero, 0.1f, faint, 4);
            s.inner = IndigoShapes.Create("Inner", IndigoShapes.Ring, go.transform, Vector2.zero, 0.1f, faint, 4);
            for (int i = 0; i < Runes; i++)
                s.runes[i] = FlatSprite.Create("Rune", go.transform, follow.position, Vector2.one * 0.35f, faint, 5);

            // A dark plate behind the words, so they read over platforms and whatever else is back there.
            bool hasHint = !string.IsNullOrEmpty(hint);
            float width = Mathf.Max(title.Length * 1.1f, hasHint ? hint.Length * 0.5f : 0f) * 0.6f + 1f;
            float top = 0.75f, bottom = hasHint ? -1.35f : -0.75f;
            s.plate = FlatSprite.Create("Plate", go.transform, Vector2.zero, new Vector2(width, top - bottom), new Color(0f, 0f, 0f, 0f), 38);
            s.plate.transform.localPosition = new Vector2(0f, -labelOffset + (top + bottom) * 0.5f);

            var titleText = IndigoShapes.Label("Title", go.transform, new Vector2(0f, -labelOffset), title, 1.1f, Color.white, 40);
            if (hasHint)
                IndigoShapes.Label("Hint", titleText.transform, new Vector2(0f, -0.95f), hint, 0.5f, Color.white, 40);
            s.texts = go.GetComponentsInChildren<TextMesh>();
            s.Update();
            return s;
        }

        /// <summary>The curse goes off: everything flies outward and fades, then the sigil removes itself.</summary>
        public void Release()
        {
            if (released >= 0f) return;
            released = 0f;
            transform.SetParent(null, true); // stays where it went off while the boss flies on
            foreach (var m in motes) if (m.sr) Destroy(m.sr.gameObject);
            motes.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            if (released >= 0f)
            {
                released += dt;
                float r = Mathf.Clamp01(released / ReleaseTime);
                if (r >= 1f) { Destroy(gameObject); return; }
                float fade = (1f - r) * (1f - r);
                Draw(1f + 2.5f * Mathf.Sqrt(r), fade, 1f);
                SetTextAlpha(fade);
                return;
            }

            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.45f), 3f);
            Draw(grow, 1f, Charge);
            SetTextAlpha(Mathf.Clamp01(age / 0.25f));

            // Motes of light drawn in from all around, faster and thicker as it charges.
            float interval = Mathf.Lerp(0.09f, 0.025f, Charge);
            for (; nextMote <= age; nextMote += interval)
            {
                var from = Random.insideUnitCircle.normalized * Random.Range(MoteRange * 0.6f, MoteRange);
                var c = Color.Lerp(accent, Color.white, Random.value * 0.6f);
                var sr = FlatSprite.Create("Mote", transform, (Vector2)transform.position + from, Vector2.one * Random.Range(0.12f, 0.25f), c, 6);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                motes.Add((sr, from, 0f, Random.Range(0.4f, 0.7f)));
            }
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                m.age += dt;
                float t = m.age / m.life;
                if (t >= 1f || !m.sr) { if (m.sr) Destroy(m.sr.gameObject); motes.RemoveAt(i); continue; }
                m.sr.transform.localPosition = m.from * (1f - t * t);
                var c = m.sr.color;
                c.a = Mathf.Sin(t * Mathf.PI);
                m.sr.color = c;
                motes[i] = m;
            }
        }

        /// <param name="scale">Size of the whole circle (1 = full).</param>
        /// <param name="alpha">Overall opacity.</param>
        /// <param name="charge">0..1: brighter, faster and whiter as it charges.</param>
        void Draw(float scale, float alpha, float charge)
        {
            spin += Time.deltaTime * Mathf.Lerp(50f, 260f, charge * charge);
            var hot = Color.Lerp(accent, Color.white, charge * 0.5f);

            outer.transform.localScale = Vector3.one * 5.2f * scale;
            outer.transform.localRotation = Quaternion.Euler(0f, 0f, spin * 0.6f);
            outer.color = WithAlpha(hot, alpha * 0.9f);

            float throb = 1f + 0.06f * Mathf.Sin(age * Mathf.Lerp(6f, 22f, charge));
            inner.transform.localScale = Vector3.one * 3.3f * scale * throb;
            inner.transform.localRotation = Quaternion.Euler(0f, 0f, -spin);
            inner.color = WithAlpha(hot, alpha * 0.5f);

            glow.transform.localScale = Vector3.one * Mathf.Lerp(2f, 5f, charge) * scale;
            glow.color = WithAlpha(hot, alpha * Mathf.Lerp(0.05f, 0.2f, charge)); // faint, so the boss's outline reads in front of it

            for (int i = 0; i < Runes; i++)
            {
                float a = (spin * 0.8f + i * 360f / Runes) * Mathf.Deg2Rad;
                runes[i].transform.localPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RuneRadius * scale;
                runes[i].transform.localRotation = Quaternion.Euler(0f, 0f, 45f + spin * 2f);
                runes[i].color = WithAlpha(i % 2 == 0 ? hot : Color.white, alpha);
            }
        }

        void SetTextAlpha(float a)
        {
            plate.color = new Color(0f, 0f, 0f, 0.55f * a);
            foreach (var t in texts)
            {
                if (!t) continue;
                var c = t.color;
                c.a = t.name.EndsWith("Shadow") ? 0.75f * a : a;
                t.color = c;
            }
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
