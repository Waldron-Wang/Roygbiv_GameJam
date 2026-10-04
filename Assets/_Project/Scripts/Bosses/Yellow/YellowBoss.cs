using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// YELLOW — warmth, joy. Tutorial boss: a cheerful sun-construct.
    /// Fires bouncy light orbs; the player punches them back (basic melee reflects) to deal damage.
    /// Reflected orbs home onto the boss, and nothing else hurts it, so the fight teaches exactly one thing.
    ///
    /// Each cycle: drift to the player's other side -> wind up (telegraph) -> volley -> rest.
    /// Phase 0 aims single orbs; phase 1 adds a spread fan; phase 2 adds lobbed, bouncing orbs.
    /// Phase 3 (the finale) uses everything, faster: orbs fly at mixed speeds (fast = small and white-hot,
    /// slow = big and orange), aimed shots come as a fast-to-slow rhythm, and it can swat a reflected orb
    /// back at the player (a rally), faster each time.
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
            new[] { Attack.Aimed, Attack.Arc, Attack.Spread },
        };

        [Header("Orbs")]
        [SerializeField] Projectile orbPrefab;   // must have reflectable = true; bounces + reflectHomesOnShooter recommended
        [Tooltip("Aimed volley, per phase. With mixed speeds the orbs go down one line from fast to slow, so they arrive one after another.")]
        [SerializeField] int[] aimedCountPerPhase = { 1, 1, 1, 3 };
        [SerializeField] int spreadCount = 3;
        [Tooltip("Total angle of the spread fan, in degrees.")]
        [SerializeField] float spreadAngle = 50f;
        [SerializeField] int arcCount = 3;
        [Tooltip("Lobbed orbs land this far apart, centered on the player.")]
        [SerializeField] float arcLandingSpacing = 3f;
        [SerializeField] float arcFlightTime = 1.4f;
        [Tooltip("With mixed speeds, each lob picks a flight time in this range instead: short = fast, flat lob; long = floaty.")]
        [SerializeField] Vector2 arcFlightTimeRange = new(0.8f, 1.8f);
        [SerializeField] float arcGravityScale = 1f;
        [SerializeField] float arcStagger = 0.3f;

        [Header("Orb speeds")]
        [Tooltip("Per phase: orbs fly at a mix of speeds instead of all at the prefab's speed.")]
        [SerializeField] bool[] mixedSpeedsPerPhase = { false, false, false, true };
        [Tooltip("Multiplier on the orb prefab's speed for the slowest / fastest orbs.")]
        [SerializeField] float slowSpeedScale = 0.5f;
        [SerializeField] float fastSpeedScale = 2.4f;
        [Tooltip("Placeholder speed tell: fast orbs shrink toward white, slow orbs grow toward this.")]
        [SerializeField] Color slowOrbTint = new(1f, 0.6f, 0.2f);
        [SerializeField] float slowOrbSize = 1.3f;
        [SerializeField] float fastOrbSize = 0.75f;

        [Header("Rally (hitting reflected orbs back)")]
        [Tooltip("Per phase: how many times one orb can be swatted back before the boss lets it hit.")]
        [SerializeField] int[] maxRalliesPerPhase = { 0, 0, 0, 2 };
        [Tooltip("Chance it swats each reflected orb (while it still has rallies left on it).")]
        [SerializeField, Range(0f, 1f)] float rallyChance = 0.5f;
        [Tooltip("Swats when a reflected orb gets this close. Keep it well past body + orb radius: fast reflected orbs move ~0.6 units per physics step.")]
        [SerializeField] float swatRadius = 3f;
        [SerializeField] float returnSpeed = 9f;
        [Tooltip("Added to returnSpeed for each earlier rally on the same orb.")]
        [SerializeField] float returnSpeedStep = 3f;
        [Tooltip("Art: play a swat/bat animation.")]
        [SerializeField] UnityEvent onSwat = new();

        [Header("Movement")]
        [Tooltip("World X range it hovers in. Defaults fit the generated arena (walls at ±20).")]
        [SerializeField] float arenaMinX = -16f;
        [SerializeField] float arenaMaxX = 16f;
        [Tooltip("World Y it hovers at: out of jump + melee reach from the ground, still on screen.")]
        [SerializeField] float hoverY = 4f;
        [Tooltip("How far beside the player it settles each cycle.")]
        [SerializeField] float sideOffset = 7f;
        [Tooltip("Per phase.")]
        [SerializeField] float[] driftTimePerPhase = { 1.2f, 1.2f, 1.2f, 0.8f };
        [Tooltip("Extra height at the middle of a drift, so it swoops over the player.")]
        [SerializeField] float driftArc = 1.5f;

        [Header("Timing")]
        [Tooltip("Telegraph before each volley, per phase. Art: hook onWindUp and read CurrentAttack.")]
        [SerializeField] float[] windUpTimePerPhase = { 0.7f, 0.7f, 0.7f, 0.45f };
        [Tooltip("Per phase.")]
        [SerializeField] float[] restTimePerPhase = { 1.5f, 1.5f, 1.5f, 0.8f };
        [SerializeField] UnityEvent onWindUp = new();

        [Tooltip("Placeholder telegraph swells this transform. Defaults to the child named Visual.")]
        [SerializeField] Transform visual;

        readonly List<Projectile> orbs = new();
        readonly HashSet<Projectile> judged = new();   // reflected orbs already rolled for a swat
        readonly HashSet<Projectile> toSwat = new();
        Vector3 visualScale;
        float windUp01, swatPulse;
        int cycle;

        public Attack CurrentAttack { get; private set; }
        bool MixedSpeeds => PerPhase(mixedSpeedsPerPhase, Phase);

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
            yield return DriftTo(NextHoverPoint(), PerPhase(driftTimePerPhase, phase));

            var rotation = Rotation[Mathf.Min(phase, Rotation.Length - 1)];
            CurrentAttack = rotation[cycle++ % rotation.Length];
            yield return WindUp(PerPhase(windUpTimePerPhase, phase));

            if (orbPrefab && Player)
            {
                switch (CurrentAttack)
                {
                    case Attack.Aimed: FireAimed(PerPhase(aimedCountPerPhase, phase)); break;
                    case Attack.Spread: FireSpread(); break;
                    case Attack.Arc: yield return FireArcs(); break;
                }
            }
            yield return Wait(PerPhase(restTimePerPhase, phase));
        }

        protected override void OnDefeated()
        {
            windUp01 = swatPulse = 0f;
            if (visual) visual.localScale = visualScale;
        }

        void FixedUpdate()
        {
            if (!IsFighting) return;
            orbs.RemoveAll(o => !o);
            judged.RemoveWhere(o => !o);
            toSwat.RemoveWhere(o => !o);

            int maxRallies = PerPhase(maxRalliesPerPhase, Phase);
            foreach (var orb in orbs)
            {
                if (!orb.WasReflected) continue;

                // Roll once per reflect, so the player can't tell from distance alone.
                if (judged.Add(orb) && orb.Rallies < maxRallies && Random.value < rallyChance)
                    toSwat.Add(orb);

                if (toSwat.Contains(orb) && Player &&
                    ((Vector2)(orb.transform.position - transform.position)).sqrMagnitude < swatRadius * swatRadius)
                    Swat(orb);
            }
        }

        void LateUpdate()
        {
            swatPulse = Mathf.MoveTowards(swatPulse, 0f, Time.deltaTime / 0.15f);
            // Placeholder until art hooks onWindUp / onSwat: swell while winding up, pop on a swat.
            if (visual && IsFighting) visual.localScale = visualScale * (1f + 0.3f * windUp01 + 0.25f * swatPulse);
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

        IEnumerator DriftTo(Vector2 target, float driftTime)
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

        IEnumerator WindUp(float windUpTime)
        {
            onWindUp.Invoke();
            for (float t = 0f; t < windUpTime; t += Time.deltaTime)
            {
                windUp01 = t / windUpTime;
                yield return null;
            }
            windUp01 = 0f;
        }

        /// <summary>With mixed speeds: all at once down one line, fastest to slowest, so they arrive as a rhythm of parries.</summary>
        void FireAimed(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float speedScale = MixedSpeeds && count > 1 ? Mathf.Lerp(fastSpeedScale, slowSpeedScale, i / (count - 1f)) : 1f;
                FireOrb(AimDirection, speedScale);
            }
        }

        /// <summary>With mixed speeds: speeds spread evenly from slow to fast, shuffled across the fan, so it never arrives as one wall.</summary>
        void FireSpread()
        {
            var speedOrder = new int[spreadCount];
            for (int i = 0; i < spreadCount; i++) speedOrder[i] = i;
            for (int i = spreadCount - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (speedOrder[i], speedOrder[j]) = (speedOrder[j], speedOrder[i]);
            }

            for (int i = 0; i < spreadCount; i++)
            {
                float angle = spreadCount > 1 ? Mathf.Lerp(-0.5f, 0.5f, i / (spreadCount - 1f)) * spreadAngle : 0f;
                float speedScale = MixedSpeeds && spreadCount > 1 ? Mathf.Lerp(slowSpeedScale, fastSpeedScale, speedOrder[i] / (spreadCount - 1f)) : 1f;
                FireOrb(Quaternion.Euler(0f, 0f, angle) * AimDirection, speedScale);
            }
        }

        IEnumerator FireArcs()
        {
            for (int i = 0; i < arcCount; i++)
            {
                if (!Player) yield break;
                float offset = (i - (arcCount - 1) * 0.5f) * arcLandingSpacing;
                float flightTime = MixedSpeeds ? Random.Range(arcFlightTimeRange.x, arcFlightTimeRange.y) : arcFlightTime;
                Lob((Vector2)Player.position + Vector2.right * offset, flightTime);
                yield return Wait(arcStagger);
            }
        }

        /// <summary>Lobs an orb that lands on `target` after flightTime.</summary>
        void Lob(Vector2 target, float flightTime)
        {
            var delta = target - (Vector2)transform.position;
            float g = Physics2D.gravity.y * arcGravityScale;
            var velocity = new Vector2(delta.x / flightTime, delta.y / flightTime - 0.5f * g * flightTime);
            var orb = Fire(orbPrefab, velocity);
            orb.Arc(velocity, arcGravityScale);
            orbs.Add(orb);
            if (!MixedSpeeds) return;
            // Tell the speed by flight time: the shortest lob looks fastest.
            float mid = (arcFlightTimeRange.x + arcFlightTimeRange.y) * 0.5f;
            DressBySpeed(orb, mid / flightTime);
        }

        Projectile FireOrb(Vector2 direction, float speedScale)
        {
            var orb = Fire(orbPrefab, direction);
            orb.ScaleSpeed(speedScale);
            DressBySpeed(orb, speedScale);
            orbs.Add(orb);
            return orb;
        }

        /// <summary>Placeholder speed tell until there's orb art: fast = small and white-hot, slow = big and orange.</summary>
        void DressBySpeed(Projectile orb, float speedScale)
        {
            float t = speedScale >= 1f
                ? Mathf.InverseLerp(1f, fastSpeedScale, speedScale)
                : -Mathf.InverseLerp(1f, slowSpeedScale, speedScale);
            orb.transform.localScale *= t >= 0f ? Mathf.Lerp(1f, fastOrbSize, t) : Mathf.Lerp(1f, slowOrbSize, -t);

            var sprite = orb.GetComponentInChildren<SpriteRenderer>();
            if (sprite) sprite.color = t >= 0f ? UnityEngine.Color.Lerp(sprite.color, UnityEngine.Color.white, t) : UnityEngine.Color.Lerp(sprite.color, slowOrbTint, -t);
        }

        /// <summary>Bats a reflected orb straight back at the player, faster with every rally.</summary>
        void Swat(Projectile orb)
        {
            toSwat.Remove(orb);
            judged.Remove(orb);   // the player's next reflect gets a fresh roll
            orb.Return(Team, Player.position - orb.transform.position, returnSpeed + orb.Rallies * returnSpeedStep);
            swatPulse = 1f;
            onSwat.Invoke();
        }
    }
}
