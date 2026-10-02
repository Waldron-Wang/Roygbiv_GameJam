using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Generic bullet / orb. Spawn it, then call Launch(direction, team).
    /// Used by Light Shot, the Yellow boss's orbs, and anything else that flies.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IReflectable
    {
        public int damage = 1;
        public float speed = 10f;
        public float lifetime = 4f;
        public bool reflectable;
        public float reflectSpeedMultiplier = 1.5f;
        [Tooltip("Destroy when touching solid ground/walls.")]
        public bool destroyOnWorld = true;

        Rigidbody2D rb;
        Team team;

        public Team Team => team;
        public bool CanBeReflected => reflectable;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            GetComponent<Collider2D>().isTrigger = true;
        }

        public void Launch(Vector2 direction, Team owner)
        {
            team = owner;
            rb.linearVelocity = direction.normalized * speed;
            Destroy(gameObject, lifetime);
        }

        public void Reflect(Team newTeam, Vector2 direction)
        {
            team = newTeam;
            rb.linearVelocity = direction.normalized * speed * reflectSpeedMultiplier;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return; // ignore other hitboxes / projectiles / zones

            var target = other.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                if (destroyOnWorld) Destroy(gameObject);
                return;
            }
            if (!Combat.CanHurt(team, target.Team)) return; // passes through friendlies

            target.TakeDamage(new DamageInfo(damage, team, rb.linearVelocity.normalized * 3f, gameObject));
            Destroy(gameObject);
        }
    }
}
