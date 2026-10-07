using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// One of the Blue boss's tears. It hangs where it formed while the spot it will land on glows, then falls
    /// straight down and splashes on the first surface. It never hurts: touching it knocks the player down and
    /// away (it ends a jump). Any hit, a shot or a swing, pops it. Made by BlueBoss.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class BlueTear : MonoBehaviour, IDamageable
    {
        [Serializable]
        public class Settings
        {
            [Tooltip("Seconds the landing spot glows before the tear falls.")]
            public float warnTime = 0.75f;
            public float accel = 40f;
            public float maxSpeed = 20f;
            public float size = 0.5f;
            [Tooltip("Speed it throws the player at: sideways away from the tear, and down.")]
            public Vector2 knockback = new(4f, -7f);
            public Color color = new(0.55f, 0.75f, 1f, 0.95f);
        }

        Settings settings;
        Rigidbody2D body;
        SpriteRenderer drop, mark;
        float landY, age, speed;
        bool spent;

        public Team Team => Team.Enemy;

        /// <param name="landY">World height of the surface it splashes on (the boss finds it).</param>
        public static BlueTear Spawn(Vector2 from, float landY, Settings settings)
        {
            var go = new GameObject("Tear");
            go.transform.position = from;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().radius = settings.size * 0.5f; // solid, so shots and swings find it

            var tear = go.AddComponent<BlueTear>();
            tear.settings = settings;
            tear.body = rb;
            tear.landY = landY;
            tear.drop = IndigoShapes.Create("Drop", IndigoShapes.Disc, go.transform, Vector2.zero, 0.01f, settings.color, 9);
            tear.mark = IndigoShapes.Create("TearMark", IndigoShapes.Disc, null, new Vector2(from.x, landY), 1f, UnityEngine.Color.clear, 6);

            // The player passes through it; touching it is handled below.
            if (PlayerController.Instance)
                foreach (var c in PlayerController.Instance.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(go.GetComponent<Collider2D>(), c);
            return tear;
        }

        void Update()
        {
            age += Time.deltaTime;
            float warn = Mathf.Clamp01(age / settings.warnTime);

            // It swells where it formed, a teardrop that stretches as it falls.
            float stretch = 1f + Mathf.Clamp01(speed / settings.maxSpeed) * 0.8f;
            drop.transform.localScale = new Vector3(settings.size * Mathf.Sqrt(warn), settings.size * Mathf.Sqrt(warn) * stretch, 1f);

            // The landing spot: a soft glow that grows and pulses faster until it lands.
            var c = settings.color;
            c.a = (0.25f + 0.35f * warn) * (0.75f + 0.25f * Mathf.Sin(age * (8f + 22f * warn)));
            mark.color = c;
            mark.transform.localScale = new Vector3(Mathf.Lerp(0.5f, 1.3f, warn), 0.22f, 1f);
        }

        void FixedUpdate()
        {
            if (spent) return;
            if (age >= settings.warnTime)
            {
                speed = Mathf.Min(settings.maxSpeed, speed + settings.accel * Time.fixedDeltaTime);
                var next = body.position + Vector2.down * speed * Time.fixedDeltaTime;
                if (next.y <= landY) { body.position = new Vector2(next.x, landY); Splash(); return; }
                body.MovePosition(next);
            }

            float r = settings.size * 0.5f;
            if (BlueBoss.PlayerTouches(new Bounds(body.position, new Vector3(r * 2f, r * 2f, 1f)), out var pc))
            {
                float side = Mathf.Sign(pc.transform.position.x - body.position.x);
                if (side == 0f) side = Random.value < 0.5f ? -1f : 1f;
                var push = new Vector2(side * settings.knockback.x, settings.knockback.y);
                pc.Health.TakeDamage(new DamageInfo(0, Team.Enemy, push, gameObject));
                Splash();
            }
        }

        public bool TakeDamage(in DamageInfo info)
        {
            if (spent || !Combat.CanHurt(info.sourceTeam, Team)) return false;
            Splash();
            return true;
        }

        /// <summary>Bursts into droplets and goes away (it landed, touched the player, got popped, or was cleared).</summary>
        public void Splash()
        {
            if (spent) return;
            spent = true;
            Vector2 at = transform.position;
            for (int i = 0; i < 6; i++)
            {
                var v = new Vector2(Random.Range(-3f, 3f), Random.Range(1.5f, 4.5f));
                HeatPuff.Spawn(at, v, settings.size * 0.45f, 0.05f, settings.color, 0.4f, 9);
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mark) Destroy(mark.gameObject);
        }
    }
}
