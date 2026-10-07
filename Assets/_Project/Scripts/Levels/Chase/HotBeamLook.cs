using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE: dresses a hot beam (a Hazard) as a red-hot iron bar: dark casing and end brackets, a glowing core
    /// with a white-hot line through it, a soft halo that breathes, and sparks dripping off its underside so it
    /// reads as "don't touch" from across the screen. Drawn from flat squares so art can replace it later.
    /// Purely visual; the trigger collider stays on the beam.
    /// Built by ChaseCourse (Dress), or placed in a scene with a BoxCollider2D + Hazard and `placedSize` set (Red's
    /// forge path): it dresses itself on Awake.
    /// </summary>
    public class HotBeamLook : MonoBehaviour
    {
        const float SparkEvery = 0.09f;

        [Tooltip("Placed in a scene (not built by ChaseCourse): the beam's size. It dresses itself on Awake. Zero = built from code.")]
        [SerializeField] Vector2 placedSize;
        [SerializeField] Color placedHeat = new(1f, 0.3f, 0.1f);

        Vector2 size;
        Color heat;
        SpriteRenderer halo, core, line;
        Color haloColor, coreColor, lineColor;
        float sparkTimer, seed;

        /// <summary>
        /// `beam` is a block made by ChaseCourse.Make (a flat square scaled to `size`): its own square is hidden,
        /// its scale reset and its BoxCollider2D sized directly, so the parts aren't stretched.
        /// </summary>
        public static HotBeamLook Dress(GameObject beam, Vector2 size, Color heat)
        {
            beam.transform.localScale = Vector3.one;
            if (beam.TryGetComponent<BoxCollider2D>(out var box)) box.size = size;
            if (beam.TryGetComponent<SpriteRenderer>(out var square)) square.enabled = false;
            var look = beam.AddComponent<HotBeamLook>();
            look.Build(size, heat);
            return look;
        }

        void Awake()
        {
            if (placedSize == Vector2.zero) return;
            if (TryGetComponent<BoxCollider2D>(out var box)) box.size = placedSize;
            if (TryGetComponent<SpriteRenderer>(out var square)) square.enabled = false;
            Build(placedSize, placedHeat);
        }

        void Build(Vector2 beamSize, Color heatColor)
        {
            size = beamSize;
            heat = heatColor;
            seed = Random.value * 100f;
            var root = new GameObject("HotBeam").transform;
            root.SetParent(transform, false);

            haloColor = new Color(heat.r, heat.g, heat.b, 0.28f);
            coreColor = heat;
            lineColor = new Color(1f, 0.92f, 0.65f);
            var casing = new Color(0.22f, 0.07f, 0.05f);
            var bracket = new Color(0.12f, 0.05f, 0.04f);

            halo = Part(root, "Halo", Vector2.zero, size + new Vector2(0.5f, 0.6f), haloColor, 1);
            Part(root, "Casing", Vector2.zero, size, casing, 2);
            core = Part(root, "Core", Vector2.zero, new Vector2(size.x - 0.2f, size.y * 0.5f), coreColor, 3);
            line = Part(root, "WhiteHot", Vector2.zero, new Vector2(size.x - 0.4f, size.y * 0.14f), lineColor, 4);
            var cap = new Vector2(0.28f, size.y + 0.16f);
            Part(root, "BracketL", new Vector2(-size.x * 0.5f + cap.x * 0.5f, 0f), cap, bracket, 5);
            Part(root, "BracketR", new Vector2(size.x * 0.5f - cap.x * 0.5f, 0f), cap, bracket, 5);
        }

        static SpriteRenderer Part(Transform root, string name, Vector2 position, Vector2 partSize, Color color, int order)
        {
            var sr = FlatSprite.Create(name, root, Vector2.zero, partSize, color, order);
            sr.transform.localPosition = position;
            return sr;
        }

        void Update()
        {
            if (!core) return;
            float t = Time.time;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 5f + seed);
            float flicker = Mathf.PerlinNoise(t * 9f, seed);

            halo.color = new Color(haloColor.r, haloColor.g, haloColor.b, haloColor.a * (0.6f + 0.8f * breathe));
            core.color = Color.Lerp(coreColor, new Color(1f, 0.6f, 0.15f), flicker * 0.6f);
            line.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.65f + 0.35f * flicker);

            sparkTimer -= Time.deltaTime;
            if (sparkTimer > 0f) return;
            sparkTimer = SparkEvery * Random.Range(0.6f, 1.4f);
            var from = (Vector2)transform.position + new Vector2(Random.Range(-0.45f, 0.45f) * size.x, -size.y * 0.5f);
            var spark = new Color(1f, Random.Range(0.55f, 0.9f), 0.2f);
            HeatPuff.Spawn(from, new Vector2(Random.Range(-0.6f, 0.6f), -Random.Range(1.5f, 3.5f)), 0.1f, 0.03f, spark, Random.Range(0.35f, 0.6f), 6);
        }
    }
}
