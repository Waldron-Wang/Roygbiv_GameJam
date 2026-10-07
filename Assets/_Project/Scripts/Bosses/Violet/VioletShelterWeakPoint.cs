using UnityEngine;

namespace Roygbiv
{
    /// <summary>A destructible anchor. Only an actual player Light Shot can release its slab.</summary>
    public sealed class VioletShelterWeakPoint : MonoBehaviour
    {
        [Tooltip("The slab this anchor holds up.")]
        [SerializeField] VioletSlab slab;
        [Tooltip("Set by the bake: sets itself up when the level starts.")]
        [SerializeField, HideInInspector] bool placedInScene;

        Health health;
        bool broken;

        void Awake()
        {
            if (placedInScene) Setup(slab);
        }

        public void Setup(VioletSlab shelter)
        {
            slab = shelter;
            health = gameObject.AddComponent<Health>();
            health.Configure(1);
            health.DamageFilter = Accepts;
            health.Damaged += OnDamaged;
            health.Died += Break;
        }

        internal static bool Accepts(DamageInfo hit) => hit.amount > 0 && hit.sourceTeam == Team.Player
            && hit.source && hit.source.TryGetComponent<LightShotProjectileSource>(out _)
            && hit.source.TryGetComponent<Projectile>(out var shot) && shot.Team == Team.Player;

        void OnDamaged(DamageInfo _) => VioletHits.Burst(transform.position, Color.yellow, 8, 4f, 0.2f);

        void Break()
        {
            if (broken) return;
            broken = true;
            foreach (var collider in GetComponents<Collider2D>()) collider.enabled = false;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
            if (slab) slab.Drop();
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
