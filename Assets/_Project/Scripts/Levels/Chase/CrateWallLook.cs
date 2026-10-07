using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE: dresses a Breakable wall as a stack of wooden crates (dark frame, panel, diagonal brace, lit top
    /// edge), drawn from flat squares so art can replace it later. Hits read: each one flashes and jolts the
    /// stack, and when it breaks, planks fly and dust puffs out. Purely visual; the collider stays on the wall.
    /// Built by ChaseCourse (Dress), or placed in a scene with a BoxCollider2D + Breakable and `placedSize` set
    /// (Yellow's crates): it dresses itself on Awake.
    /// </summary>
    [RequireComponent(typeof(Breakable))]
    public class CrateWallLook : MonoBehaviour
    {
        const float Gap = 0.05f, Border = 0.13f, Plank = 0.15f;
        const float FlashTime = 0.12f, JoltTime = 0.15f, JoltDistance = 0.08f;

        [Tooltip("Placed in a scene (not built by ChaseCourse): the wall's size. It dresses itself on Awake. Zero = built from code.")]
        [SerializeField] Vector2 placedSize;
        [SerializeField] Color placedWood = new(0.75f, 0.45f, 0.2f);

        Breakable breakable;
        Transform look;
        SpriteRenderer[] parts;
        Color[] colors;
        Vector2 size;
        Color wood;
        float flash, jolt;

        /// <summary>
        /// `wall` is a block made by ChaseCourse.Make (a flat square scaled to `size`): its own square is hidden,
        /// its scale reset and its BoxCollider2D sized directly, so the crates aren't stretched.
        /// </summary>
        public static CrateWallLook Dress(GameObject wall, Vector2 size, Color wood)
        {
            wall.transform.localScale = Vector3.one;
            if (wall.TryGetComponent<BoxCollider2D>(out var box)) box.size = size;
            if (wall.TryGetComponent<SpriteRenderer>(out var square)) square.enabled = false;
            if (!wall.TryGetComponent<Breakable>(out _)) wall.AddComponent<Breakable>();
            var crates = wall.AddComponent<CrateWallLook>();
            crates.Build(size, wood);
            return crates;
        }

        void Awake()
        {
            if (placedSize == Vector2.zero) return;
            if (TryGetComponent<SpriteRenderer>(out var square)) square.enabled = false;
            Build(placedSize, placedWood);
        }

        void Build(Vector2 wallSize, Color woodColor)
        {
            size = wallSize;
            wood = woodColor;
            look = new GameObject("Crates").transform;
            look.SetParent(transform, false);

            var frame = Shade(wood, 0.55f);
            var brace = Shade(wood, 0.78f);
            var lit = Shade(wood, 1.2f);
            int count = Mathf.Max(1, Mathf.RoundToInt(size.y / size.x));
            float height = size.y / count;
            for (int i = 0; i < count; i++)
            {
                var center = new Vector2(0f, -size.y * 0.5f + height * (i + 0.5f));
                var outer = new Vector2(size.x, height - Gap);
                var inner = outer - Vector2.one * (Border * 2f);
                Part("Frame", center, outer, frame, 0f, 1);
                Part("Panel", center, inner, wood, 0f, 2);
                float angle = Mathf.Atan2(inner.y, inner.x) * Mathf.Rad2Deg * (i % 2 == 0 ? 1f : -1f);
                Part("Brace", center, new Vector2(inner.magnitude, Plank), brace, angle, 3);
                Part("TopEdge", center + new Vector2(0f, outer.y * 0.5f - Border * 0.25f), new Vector2(outer.x, Border * 0.5f), lit, 0f, 3);
            }

            parts = look.GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) colors[i] = parts[i].color;

            breakable = GetComponent<Breakable>();
            breakable.Damaged += OnHit;
            breakable.Broken += OnBroken;
        }

        void OnDestroy()
        {
            if (!breakable) return;
            breakable.Damaged -= OnHit;
            breakable.Broken -= OnBroken;
        }

        void Part(string name, Vector2 position, Vector2 partSize, Color color, float angle, int order)
        {
            var sr = FlatSprite.Create(name, look, Vector2.zero, partSize, color, order);
            sr.transform.localPosition = position;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        void OnHit()
        {
            flash = 1f;
            jolt = 1f;
            Dust(4, 3f);
        }

        void OnBroken()
        {
            Vector2 center = transform.position;
            int planks = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * 3f), 6, 16);
            for (int i = 0; i < planks; i++)
            {
                var at = center + new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
                var color = i % 3 == 0 ? Shade(wood, 0.55f) : wood;
                var plank = FlatSprite.Create("Plank", null, at, new Vector2(Random.Range(0.35f, 0.8f), Plank), color, 12);
                plank.gameObject.AddComponent<VioletDebris>().Launch(new Vector2(Random.Range(2f, 8f), Random.Range(2f, 9f)), Random.Range(0.6f, 1f));
            }
            Dust(10, 5f);
        }

        void Dust(int count, float speed) =>
            VioletHits.Burst((Vector2)transform.position + Vector2.down * size.y * 0.25f, new Color(0.85f, 0.7f, 0.5f, 0.7f), count, speed, 0.45f);

        void Update()
        {
            if (flash <= 0f && jolt <= 0f) return;
            flash = Mathf.Max(0f, flash - Time.deltaTime / FlashTime);
            jolt = Mathf.Max(0f, jolt - Time.deltaTime / JoltTime);
            for (int i = 0; i < parts.Length; i++) parts[i].color = Color.Lerp(colors[i], Color.white, flash * 0.6f);
            look.localPosition = jolt > 0f ? (Vector3)(Random.insideUnitCircle * JoltDistance * jolt) : Vector3.zero;
        }

        static Color Shade(Color c, float k) => new(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
    }
}
