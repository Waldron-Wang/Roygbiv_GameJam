using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Dresses a projectile's placeholder circle at runtime, drawn from flat shapes so art can replace it later.
    /// Everything is built in the circle's own unit space, so the prefab's scale (and Yellow's speed-based resize)
    /// carries over. The root SpriteRenderer stays the body: bosses tint it after firing (Yellow by speed, Indigo
    /// by its accent), and the glow, core and trail follow its color every frame. Purely visual: the collider,
    /// physics and damage are untouched.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ProjectileLook : MonoBehaviour
    {
        public enum Style
        {
            Orb,         // enemy energy orb: glow, bright core, glossy highlight, fading trail
            LightShot,   // the player's shot: white-hot core, pulsing halo, long streak
            Fireball,    // flame: yellow-white core, flickering tail shading to red, embers
            Firecracker, // a tumbling red stick with gold bands and a sparking fuse (the circle is hidden)
            Barrel,      // a rolling barrel seen end-on: iron hoops, plank seams, dust
            Thorn,       // Green's shot: a barbed thorn pointing where it flies, with a leafy trail (the circle is hidden)
        }

        [SerializeField] Style style;

        static readonly Color DeepRed = new(0.75f, 0.08f, 0.04f);

        SpriteRenderer body, glow, core, halo, spark;
        SpriteRenderer[] trail = { };
        Transform spin;
        Rigidbody2D rb;
        int order;
        float seed, emitTimer, glowAlpha, trailSpacing;

        void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            order = body.sortingOrder;
            seed = Random.value * 10f;
            switch (style)
            {
                case Style.Orb: BuildOrb(); break;
                case Style.LightShot: BuildLightShot(); break;
                case Style.Fireball: BuildFireball(); break;
                case Style.Firecracker: BuildFirecracker(); break;
                case Style.Barrel: BuildBarrel(); break;
                case Style.Thorn: BuildThorn(); break;
            }
        }

        // ---------- Builders (unit space: the body circle is 1 across) ----------

        void BuildOrb()
        {
            glowAlpha = 0.22f;
            glow = Disc("Glow", transform, Vector2.zero, 1.9f, Color.clear, -2);
            Trail(4, 0.85f, 0.4f, 0.32f);
            core = Disc("Core", transform, Vector2.zero, 0.55f, Color.white, 1);
            Disc("Shine", transform, new Vector2(-0.18f, 0.18f), 0.18f, new Color(1f, 1f, 1f, 0.85f), 2);
        }

        void BuildLightShot()
        {
            glowAlpha = 0.3f;
            glow = Disc("Glow", transform, Vector2.zero, 2.2f, Color.clear, -2);
            Trail(5, 0.8f, 0.25f, 0.42f);
            halo = Shape("Halo", IndigoShapes.ThinRing, transform, Vector2.zero, 1.4f, Color.clear, 1);
            core = Disc("Core", transform, Vector2.zero, 0.6f, Color.white, 2);
        }

        void BuildFireball()
        {
            glowAlpha = 0.25f;
            glow = Disc("Glow", transform, Vector2.zero, 2f, Color.clear, -2);
            Trail(5, 0.95f, 0.35f, 0.3f);
            core = Disc("Core", transform, Vector2.zero, 0.62f, new Color(1f, 0.85f, 0.3f), 1);
            Disc("WhiteHot", core.transform, Vector2.zero, 0.5f, new Color(1f, 0.97f, 0.85f), 2);
        }

        void BuildFirecracker()
        {
            body.enabled = false;
            spin = new GameObject("Firecracker").transform;
            spin.SetParent(transform, false);
            var red = new Color(0.85f, 0.12f, 0.1f);
            var gold = new Color(1f, 0.8f, 0.25f);
            Box("Stick", spin, Vector2.zero, new Vector2(0.5f, 1.1f), red, 0);
            Box("Stripe", spin, Vector2.zero, new Vector2(0.5f, 0.08f), gold, 1);
            Box("BandTop", spin, new Vector2(0f, 0.45f), new Vector2(0.56f, 0.14f), gold, 1);
            Box("BandBottom", spin, new Vector2(0f, -0.45f), new Vector2(0.56f, 0.14f), gold, 1);
            Box("Fuse", spin, new Vector2(0f, 0.68f), new Vector2(0.07f, 0.3f), new Color(0.25f, 0.18f, 0.12f), 0);
            glowAlpha = 0.35f;
            glow = Disc("SparkGlow", spin, new Vector2(0f, 0.86f), 0.9f, Color.clear, -1);
            spark = Disc("Spark", spin, new Vector2(0f, 0.86f), 0.32f, new Color(1f, 0.95f, 0.6f), 2);
        }

        void BuildBarrel()
        {
            var iron = new Color(0.2f, 0.15f, 0.12f);
            var seam = new Color(0.35f, 0.2f, 0.09f);
            spin = new GameObject("Lid").transform;
            spin.SetParent(transform, false);
            for (int i = -1; i <= 1; i++)
            {
                float y = i * 0.22f;
                Box("Seam", spin, new Vector2(0f, y), new Vector2(0.9f * Mathf.Sqrt(1f - 4f * y * y), 0.05f), seam, 1);
            }
            Shape("InnerHoop", IndigoShapes.ThinRing, spin, Vector2.zero, 0.66f, iron, 2);
            Disc("Bolt", spin, new Vector2(0.3f, 0f), 0.09f, iron, 2);
            Shape("Hoop", IndigoShapes.Ring, transform, Vector2.zero, 1.04f, iron, 3);
        }

        void BuildThorn()
        {
            body.enabled = false;
            glowAlpha = 0.25f;
            glow = Disc("Glow", transform, Vector2.zero, 1.7f, Color.clear, -2);
            Trail(3, 0.55f, 0.25f, 0.4f);
            spin = new GameObject("Thorn").transform;
            spin.SetParent(transform, false);
            var thorn = VioletShapes.Polygon("ShotThorn", Vector2.zero, 0.25f,
                new(0.75f, 0f), new(-0.1f, 0.2f), new(-0.02f, 0.08f), new(-0.6f, 0.14f), new(-0.45f, 0f),
                new(-0.6f, -0.14f), new(-0.02f, -0.08f), new(-0.1f, -0.2f));
            core = VioletShapes.Create("Body", thorn, spin, Vector2.zero, body.color, order + 1);
            var tip = VioletShapes.Create("Tip", thorn, spin, new Vector2(0.3f, 0f), Color.Lerp(body.color, Color.white, 0.5f), order + 2);
            tip.transform.localScale = Vector3.one * 0.5f;
        }

        void Trail(int count, float from, float to, float spacing)
        {
            trailSpacing = spacing;
            trail = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
                trail[i] = Disc("Trail", transform, Vector2.zero, Mathf.Lerp(from, to, i / (float)Mathf.Max(1, count - 1)), Color.clear, -1);
        }

        SpriteRenderer Disc(string name, Transform parent, Vector2 position, float size, Color color, int orderOffset) =>
            Shape(name, IndigoShapes.Disc, parent, position, size, color, orderOffset);

        SpriteRenderer Shape(string name, Sprite sprite, Transform parent, Vector2 position, float size, Color color, int orderOffset) =>
            IndigoShapes.Create(name, sprite, parent, position, size, color, order + orderOffset);

        SpriteRenderer Box(string name, Transform parent, Vector2 position, Vector2 size, Color color, int orderOffset)
        {
            var sr = FlatSprite.Create(name, parent, Vector2.zero, size, color, order + orderOffset);
            sr.transform.localPosition = position;
            return sr;
        }

        // ---------- Animation ----------

        void LateUpdate()
        {
            Vector2 velocity = rb ? rb.linearVelocity : Vector2.zero;
            float speed = velocity.magnitude;
            Vector2 back = speed > 0.1f ? -(Vector2)transform.InverseTransformDirection(velocity / speed) : Vector2.zero;
            float scale = transform.lossyScale.x;
            float t = Time.time + seed;
            var c = body.color;

            if (glow)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 8f);
                var tint = style == Style.Firecracker ? new Color(1f, 0.7f, 0.2f) : c;
                glow.color = new Color(tint.r, tint.g, tint.b, glowAlpha * pulse);
            }

            for (int i = 0; i < trail.Length; i++)
            {
                float k = (i + 1f) / (trail.Length + 1f);
                var at = back * trailSpacing * (i + 1);
                if (style == Style.Fireball) at += new Vector2(-back.y, back.x) * (Mathf.PerlinNoise(t * 12f, i) - 0.5f) * 0.25f; // flicker
                trail[i].transform.localPosition = at;
                var tc = style == Style.Fireball ? Color.Lerp(c, DeepRed, k) : c;
                trail[i].color = new Color(tc.r, tc.g, tc.b, back == Vector2.zero ? 0f : c.a * 0.7f * (1f - k));
            }

            switch (style)
            {
                case Style.Orb:
                    core.color = Color.Lerp(c, Color.white, 0.75f);
                    break;

                case Style.LightShot:
                    core.color = Color.Lerp(c, Color.white, 0.85f);
                    float ring = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                    halo.transform.localScale = Vector3.one * (1.3f + 0.25f * ring);
                    halo.color = new Color(c.r, c.g, c.b, 0.35f + 0.35f * ring);
                    break;

                case Style.Fireball:
                    core.transform.localPosition = -back * 0.08f; // the hot side leads
                    core.transform.localScale = Vector3.one * (0.62f + 0.08f * Mathf.PerlinNoise(t * 15f, 0.5f));
                    if (Due(0.05f))
                        HeatPuff.Spawn((Vector2)transform.position + (Vector2)transform.TransformDirection(back) * 0.4f * scale,
                            Random.insideUnitCircle * 1.2f + Vector2.up * 0.8f, 0.14f * scale, 0.04f * scale,
                            new Color(1f, Random.Range(0.4f, 0.8f), 0.15f), Random.Range(0.25f, 0.45f), order + 2);
                    break;

                case Style.Firecracker:
                    // Tumbles the way it flies; the fuse spits sparks.
                    spin.localRotation *= Quaternion.Euler(0f, 0f, -velocity.x * 90f * Time.deltaTime);
                    spark.transform.localScale = Vector3.one * (0.32f * (0.7f + 0.6f * Mathf.PerlinNoise(t * 25f, 0f)));
                    if (Due(0.04f))
                        HeatPuff.Spawn(spark.transform.position, Random.insideUnitCircle * 2.5f, 0.2f * scale, 0.02f,
                            new Color(1f, Random.Range(0.7f, 1f), 0.35f), Random.Range(0.2f, 0.35f), order + 3);
                    break;

                case Style.Thorn:
                    if (back != Vector2.zero) spin.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-back.y, -back.x) * Mathf.Rad2Deg);
                    break;

                case Style.Barrel:
                    // Rolls without slipping: one turn per circumference travelled.
                    float radius = Mathf.Max(0.01f, 0.5f * scale);
                    spin.localRotation *= Quaternion.Euler(0f, 0f, -velocity.x / radius * Mathf.Rad2Deg * Time.deltaTime);
                    if (Mathf.Abs(velocity.x) > 0.5f && Due(0.12f))
                        HeatPuff.Spawn((Vector2)transform.position + new Vector2(0f, -0.45f * scale),
                            new Vector2(-Mathf.Sign(velocity.x) * Random.Range(0.5f, 1.5f), Random.Range(0.3f, 0.9f)), 0.25f * scale, 0.6f * scale,
                            new Color(0.75f, 0.62f, 0.45f, 0.5f), 0.5f, order - 1);
                    break;
            }
        }

        /// <summary>True about every `every` seconds (jittered), for sparks, embers and dust.</summary>
        bool Due(float every)
        {
            emitTimer -= Time.deltaTime;
            if (emitTimer > 0f) return false;
            emitTimer = every * Random.Range(0.6f, 1.4f);
            return true;
        }
    }
}
