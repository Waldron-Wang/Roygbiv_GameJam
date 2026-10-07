using UnityEngine;

namespace Roygbiv
{
    /// <summary>Independent slab durability; damage never releases the anchor.</summary>
    public sealed class VioletDestructibleSlab : MonoBehaviour, IDamageable
    {
        Health health;
        VioletSlab shelter;
        SpriteRenderer visual;
        Color baseColor;
        float flashUntil;
        bool broken;
        public Team Team => Team.Neutral;

        public void Setup(VioletSlab owner)
        {
            shelter = owner;
            visual = GetComponent<SpriteRenderer>();
            baseColor = visual.color;
            // Keep Health off the collider object so combat always finds this neutral facade.
            // Enemy shots shatter against the roof, but their damage is rejected by the filter.
            var durability = new GameObject("Slab durability");
            durability.transform.SetParent(transform, false);
            health = durability.AddComponent<Health>();
            health.Configure(4);
            health.DamageFilter = VioletShelterWeakPoint.Accepts;
            health.Damaged += OnDamaged;
            health.Died += Break;
        }

        public bool TakeDamage(in DamageInfo hit) => !broken && health && health.TakeDamage(hit);

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
