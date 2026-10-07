using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A strip of ground fire for the Red boss. First a thin pulsing warning strip, then flames that
    /// hurt on touch (Hazard) and flicker, then they sink away. With no burn time it is only the warning
    /// strip, which makes a landing marker for a lobbed fireball.
    /// Styles: Plain (the stretched-square placeholder; Indigo's star pillars and Violet's eruptions use it),
    /// Fire (Red: licking flame tongues and embers) and Thorns (Green: the same timing, drawn as a row of spikes).
    ///
    /// The root is scaled to the patch's width x current height, and the flames / spikes are its children in
    /// unit space, so they grow and sink with it. Drawn from runtime shapes; art can replace them without
    /// touching the timing.
    /// </summary>
    public class FirePatch : MonoBehaviour
    {
        public enum Style { Plain, Fire, Thorns }

        const float RiseTime = 0.06f, SinkTime = 0.15f, WarnHeight = 0.12f;
        static readonly Color HotTip = new(1f, 0.85f, 0.3f);
        static readonly Color WhiteHot = new(1f, 0.95f, 0.75f, 0.85f);

        SpriteRenderer sprite;
        Collider2D hurtZone;
        Style style;
        Transform look;
        SpriteRenderer[] outer, inner;
        float[] heights, seeds;
        Color color, tipColor;
        float x, groundY, width, height, warnTime, burnTime, age, seed, emberTimer;

        /// <param name="groundY">Top of the floor; the flames grow up from here.</param>
        /// <param name="burnTime">0 = warning strip only (a marker).</param>
        /// <param name="tip">What the flames flicker toward (the spikes' tips). Default: a hot yellow.</param>
        public static FirePatch Spawn(float x, float groundY, float width, float height, float warnTime, float burnTime, Color color,
                                      Color? tip = null, Style style = Style.Plain)
        {
            var sr = FlatSprite.Create("FirePatch", null, new Vector2(x, groundY), new Vector2(width, WarnHeight), color, 4);
            var go = sr.gameObject;

            // Kinematic so moving the trigger every frame is cheap; the player's dynamic body does the detecting.
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.85f); // a bit smaller than the flames, so near misses stay misses
            box.offset = new Vector2(0f, -0.075f);
            box.enabled = false;
            go.AddComponent<Hazard>();

            var patch = go.AddComponent<FirePatch>();
            patch.sprite = sr;
            patch.hurtZone = box;
            patch.style = style;
            patch.color = color;
            patch.tipColor = tip ?? HotTip;
            patch.x = x;
            patch.groundY = groundY;
            patch.width = width;
            patch.height = height;
            patch.warnTime = warnTime;
            patch.burnTime = burnTime;
            patch.seed = Random.value * 100f;
            if (burnTime > 0f && style != Style.Plain) patch.BuildLook();
            patch.Update();
            return patch;
        }

        // ---------- Look (unit space: x -0.5..0.5, the floor at y = -0.5, full height at y = 0.5) ----------

        void BuildLook()
        {
            look = new GameObject("Look").transform;
            look.SetParent(transform, false);
            bool fire = style == Style.Fire;
            int n = Mathf.Max(2, Mathf.RoundToInt(width / (fire ? 0.4f : 0.35f)));
            outer = new SpriteRenderer[n];
            inner = new SpriteRenderer[n];
            heights = new float[n];
            seeds = new float[n];
            for (int i = 0; i < n; i++)
            {
                float cx = -0.5f + (i + 0.5f) / n;
                seeds[i] = Random.value * 100f;
                heights[i] = fire ? Random.Range(0.75f, 1f) : Random.Range(0.7f, 1f);
                var shape = fire ? Flame(i % 2 == 0) : Spike();
                outer[i] = VioletShapes.Create(fire ? "Flame" : "Thorn", shape, look, new Vector2(cx, -0.5f), color, 4);
                outer[i].transform.localScale = new Vector3((fire ? 1.7f : 1.25f) / n, heights[i], 1f);
                inner[i] = VioletShapes.Create(fire ? "FlameCore" : "ThornTip", shape, look, new Vector2(cx, -0.5f), tipColor, 5);
                if (!fire) // the tip is the top part of the spike, in a lighter color
                {
                    inner[i].transform.localScale = new Vector3(0.55f / n, heights[i] * 0.45f, 1f);
                    inner[i].transform.localPosition = new Vector2(cx, -0.5f + heights[i] * 0.55f);
                }
            }
            var baseColor = fire ? WhiteHot : new Color(color.r * 0.45f, color.g * 0.4f, color.b * 0.3f);
            var strip = FlatSprite.Create(fire ? "Embers" : "Roots", look, Vector2.zero, new Vector2(1f, fire ? 0.12f : 0.1f), baseColor, 6);
            strip.transform.localPosition = new Vector2(0f, -0.45f);
            look.gameObject.SetActive(false);
        }

        static Sprite Flame(bool left) => left
            ? VioletShapes.Polygon("FireTongueL", Vector2.zero, 0.25f,
                new(-0.5f, 0f), new(-0.45f, 0.2f), new(-0.32f, 0.42f), new(-0.2f, 0.62f), new(-0.08f, 0.82f), new(-0.04f, 1f),
                new(0.1f, 0.78f), new(0.26f, 0.56f), new(0.4f, 0.34f), new(0.5f, 0.14f), new(0.5f, 0f))
            : VioletShapes.Polygon("FireTongueR", Vector2.zero, 0.25f,
                new(-0.5f, 0f), new(-0.5f, 0.14f), new(-0.4f, 0.34f), new(-0.26f, 0.56f), new(-0.1f, 0.78f), new(0.04f, 1f),
                new(0.08f, 0.82f), new(0.2f, 0.62f), new(0.32f, 0.42f), new(0.45f, 0.2f), new(0.5f, 0f));

        static Sprite Spike() => VioletShapes.Polygon("ThornSpike", Vector2.zero, 0.3f,
            new(-0.5f, 0f), new(-0.3f, 0.28f), new(-0.14f, 0.58f), new(0f, 1f), new(0.1f, 0.62f), new(0.25f, 0.3f), new(0.5f, 0f));

        void Update()
        {
            age += Time.deltaTime;

            if (age < warnTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(age * 25f);
                SetShape(WarnHeight, new Color(color.r, color.g, color.b, 0.3f + 0.4f * pulse));
                return;
            }

            float t = age - warnTime;
            if (t >= burnTime + SinkTime || burnTime <= 0f) { Destroy(gameObject); return; }

            hurtZone.enabled = t < burnTime;
            float rise = Mathf.Clamp01(t / RiseTime);
            float sink = t > burnTime ? 1f - (t - burnTime) / SinkTime : 1f;
            float flicker = style == Style.Thorns ? 1f : 1f + 0.35f * (Mathf.PerlinNoise(Time.time * 14f, seed) - 0.5f);
            float h = height * rise * sink * flicker;
            SetShape(h, Color.Lerp(color, tipColor, Mathf.PerlinNoise(seed, Time.time * 9f)));

            if (look)
            {
                sprite.enabled = false;
                look.gameObject.SetActive(true);
                if (style == Style.Fire) AnimateFlames(h, t < burnTime);
            }
        }

        /// <summary>Each tongue licks up and down on its own; the cores flicker toward the tip color; embers rise.</summary>
        void AnimateFlames(float h, bool burning)
        {
            float time = Time.time;
            for (int i = 0; i < outer.Length; i++)
            {
                float lick = Mathf.PerlinNoise(time * 10f, seeds[i]);
                var o = outer[i].transform;
                o.localScale = new Vector3(o.localScale.x, heights[i] * (0.7f + 0.45f * lick), 1f);
                outer[i].color = Color.Lerp(color, tipColor, 0.25f * Mathf.PerlinNoise(seeds[i], time * 6f));
                var c = inner[i].transform;
                c.localScale = new Vector3(o.localScale.x * 0.5f, o.localScale.y * (0.5f + 0.15f * lick), 1f);
                inner[i].color = Color.Lerp(tipColor, WhiteHot, 0.4f * lick);
            }

            emberTimer -= Time.deltaTime;
            if (!burning || emberTimer > 0f) return;
            emberTimer = Random.Range(0.04f, 0.1f);
            var at = new Vector2(x + Random.Range(-0.45f, 0.45f) * width, groundY + h * Random.Range(0.5f, 0.9f));
            HeatPuff.Spawn(at, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.5f, 3f)), 0.12f, 0.03f,
                Color.Lerp(tipColor, color, Random.value), Random.Range(0.3f, 0.55f), 7);
        }

        void SetShape(float h, Color c)
        {
            h = Mathf.Max(h, 0.01f);
            transform.localScale = new Vector3(width, h, 1f);
            transform.position = new Vector3(x, groundY + h * 0.5f, 0f);
            sprite.color = c;
        }
    }
}
