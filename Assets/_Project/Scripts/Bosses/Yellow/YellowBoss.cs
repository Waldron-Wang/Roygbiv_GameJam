using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// YELLOW — warmth, joy. Tutorial boss: a cheerful sun-construct.
    /// Fires slow, bouncy light orbs; the player punches them back (basic melee reflects) to deal damage.
    /// Reflected orbs home onto the boss, and nothing else hurts it, so the fight teaches exactly one thing.
    ///
    /// Each cycle: drift to the player's other side -> wind up (telegraph) -> volley -> rest.
    /// Phase 0 aims single orbs; phase 1 adds a spread fan; phase 2 adds lobbed, bouncing orbs.
    /// Reward: Light Shot.
    /// </summary>
    public class YellowBoss : BossBase
    {
        public enum Attack { Aimed, Spread, Arc }

        // Each phase opens with its new pattern so the player sees it right away.
        static readonly Attack[][] Rotation =
        {
            new[] { Attack.Aimed },
            new[] { Attack.Spread, Attack.Aimed },
            new[] { Attack.Arc, Attack.Spread, Attack.Aimed },
        };

        [Header("Orbs")]
        [SerializeField] Projectile orbPrefab;   // must have reflectable = true; bounces + reflectHomesOnShooter recommended
        [SerializeField] int spreadCount = 3;
        [Tooltip("Total angle of the spread fan, in degrees.")]
        [SerializeField] float spreadAngle = 50f;
        [SerializeField] int arcCount = 3;
        [Tooltip("Lobbed orbs land this far apart, centered on the player.")]
        [SerializeField] float arcLandingSpacing = 3f;
        [SerializeField] float arcFlightTime = 1.4f;
        [SerializeField] float arcGravityScale = 1f;
        [SerializeField] float arcStagger = 0.3f;

        [Header("Movement")]
        [Tooltip("World X range it hovers in. Defaults fit the generated arena (walls at ±20).")]
        [SerializeField] float arenaMinX = -16f;
        [SerializeField] float arenaMaxX = 16f;
        [Tooltip("World Y it hovers at: out of jump + melee reach from the ground, still on screen.")]
        [SerializeField] float hoverY = 4f;
        [Tooltip("How far beside the player it settles each cycle.")]
        [SerializeField] float sideOffset = 7f;
        [SerializeField] float driftTime = 1.2f;
        [Tooltip("Extra height at the middle of a drift, so it swoops over the player.")]
        [SerializeField] float driftArc = 1.5f;

        [Header("Timing")]
        [Tooltip("Telegraph before each volley. Art: hook onWindUp and read CurrentAttack.")]
        [SerializeField] float windUpTime = 0.7f;
        [SerializeField] float restTime = 1.5f;
        [SerializeField] UnityEvent onWindUp = new();

        [Tooltip("Placeholder telegraph swells this transform. Defaults to the child named Visual.")]
        [SerializeField] Transform visual;

        Vector3 visualScale;
        int cycle;

        public Attack CurrentAttack { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (!visual) visual = transform.Find("Visual");
            if (visual) visualScale = visual.localScale;
            Health.DamageFilter = info => info.source && info.source.TryGetComponent<Projectile>(out var p) && p.WasReflected;
        }

        protected override void OnPhaseChanged(int newPhase) => cycle = 0;

        protected override IEnumerator RunPhase(int phase)
        {
            yield return DriftTo(NextHoverPoint());

            var rotation = Rotation[Mathf.Min(phase, Rotation.Length - 1)];
            CurrentAttack = rotation[cycle++ % rotation.Length];
            yield return WindUp();

            if (orbPrefab && Player)
            {
                switch (CurrentAttack)
                {
                    case Attack.Aimed: Fire(orbPrefab, AimDirection); break;
                    case Attack.Spread: FireSpread(); break;
                    case Attack.Arc: yield return FireArcs(); break;
                }
            }
            yield return Wait(restTime);
        }

        protected override void OnDefeated()
        {
            if (visual) visual.localScale = visualScale;
        }

        // ---------- Movement ----------

        Vector2 NextHoverPoint()
        {
            float px = Player ? Player.position.x : 0f;
            // Cross to the player's other side so they have to turn around and reposition.
            float side = transform.position.x > px ? -1f : 1f;
            float x = px + side * sideOffset;
            if (x < arenaMinX || x > arenaMaxX) x = px - side * sideOffset; // no room there
            return new Vector2(Mathf.Clamp(x, arenaMinX, arenaMaxX), hoverY);
        }

        IEnumerator DriftTo(Vector2 target)
        {
            Vector2 start = transform.position;
            for (float t = 0f; t < 1f;)
            {
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / driftTime);
                var pos = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                pos.y += Mathf.Sin(t * Mathf.PI) * driftArc;
                if (Body) Body.MovePosition(pos);
                else transform.position = pos;
            }
        }

        // ---------- Attacks ----------

        IEnumerator WindUp()
        {
            onWindUp.Invoke();
            // Placeholder until art hooks onWindUp: swell up, snap back as it fires.
            for (float t = 0f; t < windUpTime; t += Time.deltaTime)
            {
                if (visual) visual.localScale = visualScale * (1f + 0.3f * t / windUpTime);
                yield return null;
            }
            if (visual) visual.localScale = visualScale;
        }

        void FireSpread()
        {
            for (int i = 0; i < spreadCount; i++)
            {
                float angle = spreadCount > 1 ? Mathf.Lerp(-0.5f, 0.5f, i / (spreadCount - 1f)) * spreadAngle : 0f;
                Fire(orbPrefab, Quaternion.Euler(0f, 0f, angle) * AimDirection);
            }
        }

        IEnumerator FireArcs()
        {
            for (int i = 0; i < arcCount; i++)
            {
                if (!Player) yield break;
                float offset = (i - (arcCount - 1) * 0.5f) * arcLandingSpacing;
                Lob((Vector2)Player.position + Vector2.right * offset);
                yield return Wait(arcStagger);
            }
        }

        /// <summary>Lobs an orb that lands on `target` after arcFlightTime.</summary>
        void Lob(Vector2 target)
        {
            var delta = target - (Vector2)transform.position;
            float g = Physics2D.gravity.y * arcGravityScale;
            var velocity = new Vector2(delta.x / arcFlightTime, delta.y / arcFlightTime - 0.5f * g * arcFlightTime);
            Fire(orbPrefab, velocity).Arc(velocity, arcGravityScale);
        }
    }
}
