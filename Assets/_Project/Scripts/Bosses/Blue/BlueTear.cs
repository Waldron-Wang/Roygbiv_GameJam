using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// One of the Blue boss's tears, a lobbed projectile:
    ///   Forming  it swells beside the boss while a dotted arc shows its flight and a glow marks where it will land.
    ///            The aim follows the player the whole time.
    ///   Locked   the arc and glow flash white and stop following: the moment to move.
    ///   Flying   thrown along a real parabola at where the player was, a stretched teardrop turning with its path and
    ///            trailing droplets. It splashes on the first rock it ENTERS (it can fly out of rock it formed in, so the
    ///            boss can float over the walls), on the water, or on the player.
    /// A tear never hurts: touching it knocks the player down and away (it ends a jump). Any hit, a shot or a swing,
    /// pops it. Without an aim it's simply dropped (the summit's tears from the sky). Made by BlueBoss.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class BlueTear : MonoBehaviour, IDamageable
    {
        [Serializable]
        public class Settings
        {
            [Header("Wind-up")]
            [Tooltip("Seconds it forms beside the boss while its arc follows the player.")]
            public float warnTime = 0.7f;
            [Tooltip("Seconds the arc holds still, flashing, before the throw: the moment to move.")]
            public float lockTime = 0.2f;
            [Tooltip("Show the dotted flight path while it forms.")]
            public bool showArc = true;
            public int arcDots = 10;

            [Header("Flight")]
            public float gravity = 16f;
            [Tooltip("Sets the flight time from the distance: higher = flatter, quicker throws.")]
            public float flightSpeed = 10f;
            public float minFlightTime = 0.5f;
            public float maxFlightTime = 1.3f;
            [Tooltip("Seconds in the air before it's gone anyway.")]
            public float maxLife = 4f;

            [Header("Hit")]
            [Tooltip("Speed it throws the player at: sideways the way it was flying, and down.")]
            public Vector2 knockback = new(4f, -7f);

            [Header("Look")]
            public float size = 0.25f;
            public Color color = new(0.55f, 0.75f, 1f, 0.95f);
        }

        enum Phase { Forming, Locked, Flying }

        const float PredictStep = 1f / 30f;
        static readonly List<RaycastHit2D> Hits = new();

        Settings settings;
        Rigidbody2D body;
        SpriteRenderer drop, mark;
        SpriteRenderer[] dots;
        Func<Vector2> origin, aim;
        BlueWater water;
        Transform passThrough;

        readonly List<Vector2> path = new();
        Phase phase;
        Vector2 velocity, impact;
        bool hasImpact, spent;
        float age, trail;

        public Team Team => Team.Enemy;

        /// <summary>Raised the moment it's thrown (the boss leans into it).</summary>
        public Action Launched { get; set; }

        /// <param name="origin">Where it forms, read every frame until the throw (it rides along with the boss).</param>
        /// <param name="aim">What it's thrown at, read every frame until it locks. Null = dropped straight down.</param>
        /// <param name="water">Splashes on its surface. May be null.</param>
        /// <param name="passThrough">Colliders under this are ignored (the summit while the player is still climbing).</param>
        public static BlueTear Throw(Func<Vector2> origin, Func<Vector2> aim, Settings settings, BlueWater water, Transform passThrough)
        {
            var go = new GameObject("Tear");
            go.transform.position = origin();
            go.transform.localScale = Vector3.one * 0.01f;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().radius = 0.5f; // scales with the drop; solid, so shots and swings find it

            var tear = go.AddComponent<BlueTear>();
            tear.settings = settings;
            tear.body = rb;
            tear.origin = origin;
            tear.aim = aim;
            tear.water = water;
            tear.passThrough = passThrough;
            tear.drop = IndigoShapes.Create("Drop", IndigoShapes.Disc, go.transform, Vector2.zero, 1f, settings.color, 9);
            tear.mark = IndigoShapes.Create("TearMark", IndigoShapes.Disc, null, go.transform.position, 1f, UnityEngine.Color.clear, 6);
            if (settings.showArc && aim != null)
            {
                tear.dots = new SpriteRenderer[Mathf.Max(0, settings.arcDots)];
                for (int i = 0; i < tear.dots.Length; i++)
                    tear.dots[i] = IndigoShapes.Create("ArcDot", IndigoShapes.Disc, null, go.transform.position, 0.07f, UnityEngine.Color.clear, 8);
            }

            // The player passes through it; touching it is handled below.
            if (PlayerController.Instance)
                foreach (var c in PlayerController.Instance.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(go.GetComponent<Collider2D>(), c);
            return tear;
        }

        // ---------- Wind-up and look ----------

        void Update()
        {
            if (spent) return;
            age += Time.deltaTime;
            float warn = Mathf.Clamp01(age / Mathf.Max(0.01f, settings.warnTime));

            if (phase == Phase.Forming)
            {
                transform.position = origin();
                velocity = LaunchVelocity();
                Predict();
                if (age >= settings.warnTime) phase = Phase.Locked; // the aim stops following here
            }
            if (phase == Phase.Locked && age >= settings.warnTime + settings.lockTime)
            {
                phase = Phase.Flying;
                body.position = transform.position;
                HideDots();
                Launched?.Invoke();
            }

            AnimateDrop(warn);
            AnimateTelegraph(warn);
        }

        void AnimateDrop(float warn)
        {
            float s = settings.size;
            if (phase == Phase.Flying)
            {
                // A teardrop stretched along its flight, turning with it, shedding droplets.
                float speed = velocity.magnitude;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
                transform.localScale = new Vector3(s * (1f + 0.7f * Mathf.Clamp01(speed / 15f)), s * 0.8f, 1f);
                drop.color = settings.color;
                trail -= Time.deltaTime;
                if (trail <= 0f)
                {
                    trail = 0.035f;
                    HeatPuff.Spawn(transform.position, -velocity * 0.08f + Random.insideUnitCircle * 0.3f, s * 0.4f, 0.02f, settings.color, 0.3f, 8);
                }
                return;
            }

            // Forming: it swells and wobbles. Locked: it trembles, flashing.
            bool locked = phase == Phase.Locked;
            float grow = Mathf.Sqrt(warn) * (1f + (locked ? 0.15f : 0.08f) * Mathf.Sin(age * (locked ? 60f : 18f)));
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(s * grow, s * grow * 1.1f, 1f);
            drop.color = locked ? UnityEngine.Color.Lerp(settings.color, UnityEngine.Color.white, 0.5f + 0.5f * Mathf.Sin(age * 50f)) : settings.color;
        }

        void AnimateTelegraph(float warn)
        {
            bool locked = phase == Phase.Locked;
            var c = locked ? UnityEngine.Color.white : settings.color;

            // The dotted flight path, fading toward the far end.
            if (dots != null && phase != Phase.Flying)
                for (int i = 0; i < dots.Length; i++)
                {
                    float t = (i + 1f) / (dots.Length + 1f);
                    int k = Mathf.Min(path.Count - 1, Mathf.RoundToInt(t * (path.Count - 1)));
                    if (k < 0) { dots[i].color = UnityEngine.Color.clear; continue; }
                    dots[i].transform.position = path[k];
                    var dc = c;
                    dc.a = (locked ? 0.9f : 0.55f) * warn * (1f - 0.6f * t) * (0.75f + 0.25f * Mathf.Sin(age * 12f - i));
                    dots[i].color = dc;
                }

            // The landing spot: a soft glow that grows and pulses faster until it lands.
            if (!hasImpact) { mark.color = UnityEngine.Color.clear; return; }
            float k2 = settings.size * 2f; // the glow is sized for a 0.5 tear
            float strength = phase == Phase.Flying ? 1f : warn;
            var mc = c;
            mc.a = (0.25f + 0.35f * strength) * (0.75f + 0.25f * Mathf.Sin(age * (8f + 22f * strength)));
            mark.color = mc;
            mark.transform.position = impact;
            mark.transform.localScale = new Vector3(Mathf.Lerp(0.5f, 1.3f, strength) * k2, 0.22f * k2, 1f);
        }

        void HideDots()
        {
            if (dots == null) return;
            foreach (var d in dots)
                if (d) Destroy(d.gameObject);
            dots = null;
        }

        // ---------- Flight ----------

        /// <summary>The throw that reaches the aim point, its flight time set by the distance. Zero without an aim (a drop).</summary>
        Vector2 LaunchVelocity()
        {
            if (aim == null) return Vector2.zero;
            Vector2 from = transform.position, to = aim();
            float time = Mathf.Clamp((to - from).magnitude / Mathf.Max(0.1f, settings.flightSpeed), settings.minFlightTime, settings.maxFlightTime);
            return (to - from) / time + 0.5f * settings.gravity * time * Vector2.up;
        }

        /// <summary>Steps the flight ahead: the dotted path and where it will land.</summary>
        void Predict()
        {
            path.Clear();
            Vector2 p = transform.position, v = velocity;
            path.Add(p);
            hasImpact = false;
            for (float t = 0f; t < settings.maxLife; t += PredictStep)
            {
                v.y -= settings.gravity * PredictStep;
                var next = p + v * PredictStep;
                if (Blocked(p, next, v, out var hit)) { path.Add(hit); impact = hit; hasImpact = true; return; }
                p = next;
                path.Add(p);
            }
        }

        void FixedUpdate()
        {
            if (spent || phase != Phase.Flying) return;
            float dt = Time.fixedDeltaTime;
            velocity.y -= settings.gravity * dt;
            var pos = body.position;
            var next = pos + velocity * dt;

            if (Blocked(pos, next, velocity, out var hit))
            {
                body.position = hit;
                transform.position = hit;
                if (water && hit.y <= water.HeightAt(hit.x) + 0.05f) water.Ripple(hit.x, 0.5f);
                Splash();
                return;
            }
            body.MovePosition(next);

            float r = settings.size * 0.5f;
            if (BlueBoss.PlayerTouches(new Bounds(next, new Vector3(r * 2f, r * 2f, 1f)), out var pc))
            {
                float side = Mathf.Abs(velocity.x) > 0.1f ? Mathf.Sign(velocity.x) : Mathf.Sign(pc.transform.position.x - next.x);
                pc.Health.TakeDamage(new DamageInfo(0, Team.Enemy, new Vector2(side * settings.knockback.x, settings.knockback.y), gameObject));
                Splash();
                return;
            }
            if (age > settings.warnTime + settings.lockTime + settings.maxLife) Splash();
        }

        /// <summary>
        /// The first surface the segment from `a` to `b` runs INTO: rock it starts inside is ignored (it flies out of it),
        /// one-way platforms only stop it on the way down, and the water's surface counts.
        /// </summary>
        bool Blocked(Vector2 a, Vector2 b, Vector2 v, out Vector2 point)
        {
            point = b;
            var d = b - a;
            float len = d.magnitude, best = float.PositiveInfinity;
            if (len > 0f)
            {
                Physics2D.Raycast(a, d / len, ContactFilter2D.noFilter, Hits, len);
                foreach (var h in Hits)
                {
                    var c = h.collider;
                    if (!c || c.isTrigger || h.distance <= 0f) continue;              // distance 0: it started inside this
                    if (passThrough && c.transform.IsChildOf(passThrough)) continue;
                    if (c.usedByEffector && v.y > 0f) continue;                      // one-way: only from above
                    if (c.GetComponentInParent<PlayerController>() || c.GetComponentInParent<IDamageable>() != null) continue;
                    if (h.distance < best) { best = h.distance; point = h.point; }
                }
            }
            if (water && a.y > water.Surface && b.y <= water.Surface)
            {
                float f = (a.y - water.Surface) / Mathf.Max(0.0001f, a.y - b.y);
                if (f * len < best) { best = f * len; point = Vector2.Lerp(a, b, f); }
            }
            return !float.IsPositiveInfinity(best);
        }

        // ---------- Hits ----------

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
                HeatPuff.Spawn(at, v * settings.size * 2f, settings.size * 0.45f, 0.05f, settings.color, 0.4f, 9);
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mark) Destroy(mark.gameObject);
            HideDots();
        }
    }
}
