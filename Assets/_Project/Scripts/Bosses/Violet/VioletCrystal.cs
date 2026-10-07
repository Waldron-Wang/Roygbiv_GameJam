using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// THE CRYSTAL GATE: a violet crystal wall across the path. Only a fully charged Blaze Strike shatters it.
    /// The hit is recognized by its source being the Blaze Strike ability's own hitbox (which only opens on a
    /// charged release), never by guessing from damage numbers. Melee, shots, orbs and shockwaves clang off.
    /// Team.Neutral, so anything can hit it; it needs a solid (non-trigger) collider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class VioletCrystal : MonoBehaviour, IDamageable
    {
        [Tooltip("The crystal's color (its facets are drawn at runtime from the collider's size).")]
        [SerializeField] Color crystalColor = new(0.8f, 0.5f, 1f, 0.92f);

        SpriteRenderer[] facets;
        Color[] baseColors;
        float flash, shake;
        Transform look; // the facets' parent: shaken on a clang (never the solid collider)
        Vector3 lookPos;

        public Team Team => Team.Neutral;
        public bool Shattered { get; private set; }
        public event Action Broken;

        void Awake()
        {
            if (!transform.Find("Facets") && TryGetComponent<BoxCollider2D>(out var box)) BuildFacets(box.size.y);
            facets = GetComponentsInChildren<SpriteRenderer>();
            baseColors = new Color[facets.Length];
            for (int i = 0; i < facets.Length; i++) baseColors[i] = facets[i].color;
            look = transform.Find("Facets");
            if (look) lookPos = look.localPosition;
        }

        /// <summary>Placed in the scene: the facets are runtime sprites, so they're made here, to the collider's height.</summary>
        void BuildFacets(float height)
        {
            var root = new GameObject("Facets").transform;
            root.SetParent(transform, false);
            float[] heights = { 1f, 0.82f, 0.95f, 0.7f };
            float[] offsets = { 0f, -0.35f, 0.3f, 0.05f };
            float[] widths = { 1.3f, 1f, 0.9f, 0.7f };
            for (int i = 0; i < heights.Length; i++)
            {
                var f = VioletShapes.Create("Facet", VioletShapes.Crystal, root, new Vector2(offsets[i], 0f), Color.Lerp(crystalColor, Color.white, i * 0.12f), 6 + i);
                f.transform.localScale = new Vector3(widths[i], height * heights[i], 1f);
            }
        }

        public bool TakeDamage(in DamageInfo info)
        {
            if (Shattered) return false;
            if (IsBlazeStrike(info.source)) { Shatter(); return true; }
            if (info.sourceTeam == Team.Player) Clang(info.source ? (Vector2)info.source.transform.position : (Vector2)transform.position);
            return false;
        }

        /// <summary>The hit came from a Blaze Strike: the hitbox that a BlazeStrikeAbility opens on a full charge.</summary>
        static bool IsBlazeStrike(GameObject source)
        {
            if (!source || !source.TryGetComponent<Hitbox>(out var hitbox)) return false;
            var loadout = hitbox.GetComponentInParent<AbilityLoadout>();
            return loadout && loadout.Get(AbilityId.BlazeStrike) is BlazeStrikeAbility blaze && blaze.StrikeHitbox == hitbox;
        }

        void Clang(Vector2 from)
        {
            flash = 1f;
            shake = 1f;
            var c = GetComponent<Collider2D>();
            var at = c ? (Vector2)c.ClosestPoint(from) : (Vector2)transform.position;
            for (int i = 0; i < 6; i++)
                VioletHits.Puff(at, new Vector2(Mathf.Sign(from.x - at.x) * UnityEngine.Random.Range(2f, 5f), UnityEngine.Random.Range(-2f, 3f)),
                    0.15f, 0.04f, new Color(1f, 0.95f, 1f), 0.25f, 40);
            VioletHits.ShakeCamera(0.05f, 0.1f);
        }

        void Shatter()
        {
            Shattered = true;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var f in facets) if (f) bounds.Encapsulate(f.bounds);
            for (int i = 0; i < 28; i++)
            {
                var at = new Vector2(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), UnityEngine.Random.Range(bounds.min.y, Mathf.Min(bounds.max.y, bounds.min.y + 6f)));
                var shard = VioletShapes.Create("Shard", VioletShapes.Shard, null, at, Color.Lerp(VioletShapes.Bright, Color.white, UnityEngine.Random.value * 0.5f), 40);
                shard.gameObject.AddComponent<VioletDebris>().Launch(new Vector2(UnityEngine.Random.Range(1f, 9f), UnityEngine.Random.Range(1f, 8f)), UnityEngine.Random.Range(0.4f, 1f));
            }
            VioletHits.Burst(bounds.center, VioletShapes.Glow, 18, 9f, 0.6f);
            VioletHits.ShakeCamera(0.4f, 0.4f);
            if (ScreenWarp.Main is { } warp) warp.Flash(new Color(0.9f, 0.7f, 1f, 0.4f), 0.3f);
            Broken?.Invoke();
            foreach (var f in facets) if (f) f.enabled = false;
        }

        void Update()
        {
            if (Shattered) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 5f);
            float glow = 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f));
            for (int i = 0; i < facets.Length; i++)
                if (facets[i]) facets[i].color = Color.Lerp(baseColors[i], Color.white, Mathf.Max(flash, glow));
            if (look) look.localPosition = lookPos + new Vector3(Mathf.Sin(Time.time * 90f) * 0.06f * shake, 0f, 0f);
        }
    }
}
