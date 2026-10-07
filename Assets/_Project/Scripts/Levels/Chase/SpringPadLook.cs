using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE: dresses a SpringPad as a launch pad: a dark base plate, a zigzag spring, a green pad on top, and
    /// arrows drifting up above it so it reads as "step here, go up". When it fires, the pad kicks up and
    /// settles, the arrows flare and a puff blows out. Drawn from flat squares so art can replace it later.
    /// Purely visual; the trigger collider stays on the pad.
    /// </summary>
    [RequireComponent(typeof(SpringPad))]
    public class SpringPadLook : MonoBehaviour
    {
        const int Struts = 5, Arrows = 3;
        const float ArrowCycle = 1.1f, ArrowRise = 0.9f, KickTime = 0.3f, KickHeight = 0.35f;

        SpringPad pad;
        Vector2 size;
        Color green;
        Transform coil, top;
        SpriteRenderer[][] arrows;
        float coilHeight, topY, kick = 1f, flare;

        /// <summary>
        /// `spring` is a block made by ChaseCourse.Make (a flat square scaled to `size`): its own square is hidden,
        /// its scale reset and its BoxCollider2D sized directly, so the parts aren't stretched.
        /// </summary>
        public static SpringPadLook Dress(GameObject spring, Vector2 size, Color green)
        {
            spring.transform.localScale = Vector3.one;
            if (spring.TryGetComponent<BoxCollider2D>(out var box)) box.size = size;
            if (spring.TryGetComponent<SpriteRenderer>(out var square)) square.enabled = false;
            var look = spring.AddComponent<SpringPadLook>();
            look.Build(size, green);
            return look;
        }

        void Build(Vector2 padSize, Color padColor)
        {
            size = padSize;
            green = padColor;
            var root = new GameObject("SpringPad").transform;
            root.SetParent(transform, false);
            float bottom = -size.y * 0.5f;
            const float baseHeight = 0.08f, topHeight = 0.1f;

            var metal = new Color(0.16f, 0.2f, 0.19f);
            var steel = new Color(0.6f, 0.68f, 0.66f);
            Part(root, "Base", new Vector2(0f, bottom + baseHeight * 0.5f), new Vector2(size.x, baseHeight), metal, 0f, 2);

            // The spring: struts zigzagging between the base and the pad. Its pivot is its bottom, so it can stretch.
            coilHeight = size.y - baseHeight - topHeight;
            coil = new GameObject("Coil").transform;
            coil.SetParent(root, false);
            coil.localPosition = new Vector2(0f, bottom + baseHeight);
            float span = size.x * 0.75f, dx = span / Struts;
            for (int i = 0; i < Struts; i++)
            {
                var from = new Vector2(-span * 0.5f + dx * i, i % 2 == 0 ? 0f : coilHeight);
                var to = new Vector2(from.x + dx, i % 2 == 0 ? coilHeight : 0f);
                var along = to - from;
                Part(coil, "Strut", (from + to) * 0.5f, new Vector2(along.magnitude + 0.04f, 0.05f), steel, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 2);
            }

            top = new GameObject("Top").transform;
            top.SetParent(root, false);
            topY = bottom + baseHeight + coilHeight + topHeight * 0.5f;
            top.localPosition = new Vector2(0f, topY);
            Part(top, "Pad", Vector2.zero, new Vector2(size.x, topHeight), Shade(green, 0.75f), 0f, 3);
            Part(top, "PadTop", new Vector2(0f, topHeight * 0.25f), new Vector2(size.x - 0.06f, topHeight * 0.5f), Shade(green, 1.25f), 0f, 4);

            // Up arrows: chevrons drifting up off the pad, staggered, fading as they rise.
            arrows = new SpriteRenderer[Arrows][];
            for (int i = 0; i < Arrows; i++)
            {
                var arrow = new GameObject("Arrow").transform;
                arrow.SetParent(root, false);
                arrows[i] = new[]
                {
                    Part(arrow, "L", new Vector2(-0.13f, 0f), new Vector2(0.34f, 0.08f), green, 40f, 6),
                    Part(arrow, "R", new Vector2(0.13f, 0f), new Vector2(0.34f, 0.08f), green, -40f, 6),
                };
            }

            pad = GetComponent<SpringPad>();
            pad.Launched += OnLaunched;
        }

        void OnDestroy()
        {
            if (pad) pad.Launched -= OnLaunched;
        }

        static SpriteRenderer Part(Transform parent, string name, Vector2 position, Vector2 partSize, Color color, float angle, int order)
        {
            var sr = FlatSprite.Create(name, parent, Vector2.zero, partSize, color, order);
            sr.transform.localPosition = position;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return sr;
        }

        void OnLaunched()
        {
            kick = 0f;
            flare = 1f;
            var at = (Vector2)transform.position + Vector2.up * size.y * 0.5f;
            for (int i = 0; i < 8; i++)
                HeatPuff.Spawn(at, new Vector2(Random.Range(-4f, 4f), Random.Range(0.5f, 2.5f)), 0.25f, 0.5f, new Color(green.r, green.g, green.b, 0.6f), 0.45f, 7);
        }

        void Update()
        {
            if (!top) return;

            // Kick: the pad shoots up, then eases back down onto the spring.
            if (kick < 1f)
            {
                kick = Mathf.Min(1f, kick + Time.deltaTime / KickTime);
                float lift = KickHeight * Mathf.Sin(kick * Mathf.PI) * (1f - kick * 0.5f);
                top.localPosition = new Vector2(0f, topY + lift);
                coil.localScale = new Vector3(1f, 1f + lift / Mathf.Max(0.01f, coilHeight), 1f);
            }
            flare = Mathf.Max(0f, flare - Time.deltaTime * 2.5f);

            float padTop = topY + 0.05f;
            for (int i = 0; i < arrows.Length; i++)
            {
                float k = Mathf.Repeat(Time.time / ArrowCycle + i / (float)Arrows, 1f);
                arrows[i][0].transform.parent.localPosition = new Vector2(0f, padTop + 0.3f + ArrowRise * k);
                float alpha = Mathf.Sin(k * Mathf.PI) * (0.55f + 0.45f * flare);
                var c = Color.Lerp(green, Color.white, flare * 0.6f);
                foreach (var part in arrows[i]) part.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        static Color Shade(Color c, float k) => new(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
    }
}
