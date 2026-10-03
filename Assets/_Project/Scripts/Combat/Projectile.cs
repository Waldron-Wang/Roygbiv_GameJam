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
        [Tooltip("Reflected shots steer toward whoever fired them, so a parry always lands (Yellow tutorial).")]
        public bool reflectHomesOnShooter;
        [Tooltip("Destroy when touching solid ground/walls.")]
        public bool destroyOnWorld = true;
        [Tooltip("Bounce off ground/walls this many times before destroyOnWorld applies.")]
        public int bounces;

        Rigidbody2D rb;
        Collider2D col;
        Team team;
        Transform shooter;
        int bouncesLeft;
        float dieAt = float.PositiveInfinity;

        public Team Team => team;
        public bool CanBeReflected => reflectable;
        public bool WasReflected { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        /// <param name="shooter">Who fired it; reflected shots can home back onto it.</param>
        public void Launch(Vector2 direction, Team owner, Transform shooter = null)
        {
            team = owner;
            this.shooter = shooter;
            bouncesLeft = bounces;
            rb.linearVelocity = direction.normalized * speed;
            dieAt = Time.time + lifetime;
        }

        /// <summary>Call after Launch for a lobbed shot: turns on gravity and replaces the velocity.</summary>
        public void Arc(Vector2 velocity, float gravityScale)
        {
            rb.gravityScale = gravityScale;
            rb.linearVelocity = velocity;
        }

        public void Reflect(Team newTeam, Vector2 direction)
        {
            team = newTeam;
            WasReflected = true;
            rb.gravityScale = 0f;
            if (reflectHomesOnShooter && shooter) direction = (Vector2)(shooter.position - transform.position);
            rb.linearVelocity = direction.normalized * speed * reflectSpeedMultiplier;
            dieAt = Time.time + lifetime;
        }

        void FixedUpdate()
        {
            if (Time.time >= dieAt) { Destroy(gameObject); return; }

            if (WasReflected && reflectHomesOnShooter && shooter)
                rb.linearVelocity = ((Vector2)(shooter.position - transform.position)).normalized * rb.linearVelocity.magnitude;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return; // ignore other hitboxes / projectiles / zones

            var target = other.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                if (bouncesLeft > 0) Bounce(other);
                else if (destroyOnWorld) Destroy(gameObject);
                return;
            }
            if (!Combat.CanHurt(team, target.Team)) return; // passes through friendlies

            target.TakeDamage(new DamageInfo(damage, team, rb.linearVelocity.normalized * 3f, gameObject));
            Destroy(gameObject);
        }

        void Bounce(Collider2D surface)
        {
            bouncesLeft--;
            rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, col.Distance(surface).normal);
        }
    }
}
