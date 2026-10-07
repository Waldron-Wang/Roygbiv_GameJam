using UnityEngine;

namespace Roygbiv
{
    /// <summary>Independent slab durability; damage never releases the anchor.</summary>
    public sealed class VioletDestructibleSlab : MonoBehaviour, IDamageable
    {
        [Tooltip("Hits (Light Shots or sword swings) it takes to break the slab: the way out of the shelter.")]
        [SerializeField, Min(1)] int maxHealth = 4;
        [Tooltip("Set by the bake: sets itself up when the level starts.")]
        [SerializeField, HideInInspector] bool placedInScene;

        Health health;
        VioletSlab shelter;
        SpriteRenderer visual;
        Color baseColor;
        float flashUntil;
        bool broken;
        public Team Team => Team.Neutral;

        void Awake()
        {
            if (placedInScene) Setup(GetComponent<VioletSlab>(), maxHealth);
        }

        public void Setup(VioletSlab owner, int maxHealth)
        {
            shelter = owner;
            visual = GetComponent<SpriteRenderer>();
            baseColor = visual.color;
            // Keep Health off the collider object so combat always finds this neutral facade.
            // Enemy projectiles are blocked without damaging the roof.
            var durability = new GameObject("Slab durability");
            durability.transform.SetParent(transform, false);
            health = durability.AddComponent<Health>();
            health.Configure(maxHealth, Team.Neutral, 0f);
            health.DamageFilter = Accepts;
            health.Damaged += OnDamaged;
            health.Died += Break;
        }

        bool Accepts(DamageInfo hit) => VioletShelterWeakPoint.Accepts(hit)
            || (hit.amount > 0 && hit.sourceTeam == Team.Player && hit.source
                && hit.source.TryGetComponent<Hitbox>(out var swing) && swing.team == Team.Player
                && hit.source.GetComponentInParent<PlayerCombat>()
                && hit.source.GetComponentInParent<PlayerController>());

        public bool TakeDamage(in DamageInfo hit)
        {
            if (broken || !health) return false;
            return health.TakeDamage(hit);
        }

        void OnDamaged(DamageInfo _)
        {
            flashUntil = Time.time + 0.12f;
            visual.color = Color.white;
            VioletHits.Burst(transform.position, baseColor, 6, 3f, 0.15f);
        }

        void Update()
        {
            if (visual && !broken && Time.time >= flashUntil) visual.color = baseColor;
        }

        void Break()
        {
            if (broken) return;
            broken = true;
            shelter.OnSlabDestroyed();
            var bounds = visual.bounds;
            visual.enabled = false;
            VioletHits.Burst(bounds.center, baseColor, 18, 6f, 0.3f);
            VioletHits.ShakeCamera(0.25f, 0.2f);
            for (int i = 0; i < 10; i++)
            {
                var at = new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, (i + 0.5f) / 10f), bounds.center.y);
                var shard = FlatSprite.Create("Slab fragment", null, at,
                    new Vector2(bounds.size.x / 12f, bounds.size.y * Random.Range(0.5f, 1f)), baseColor, visual.sortingOrder);
                shard.gameObject.AddComponent<VioletDebris>().Launch(new Vector2(Random.Range(-4f, 4f), Random.Range(1f, 5f)), 1.5f);
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!health) return;
            health.Damaged -= OnDamaged;
            health.Died -= Break;
        }
    }
}
