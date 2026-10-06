using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// VIOLET — royalty, wisdom, creativity. The final exam: a KING with a crown, a long cape and a one-handed
    /// greatsword, who can only be beaten by using every ability at the right moment.
    ///
    /// PHASE 1, THE APPROACH (not a BossBase phase: the fight hasn't started). He stands on a dais at the far end of
    /// a long course and bombards the player from afar; he can't be hurt. VioletApproach runs that part and asks him
    /// for the matching poses (FarGesture). Entering the arena calls StartFight: he leaps down and the duel begins.
    /// PHASE 2, THE DUEL (BossBase phase 0). Hurt by everything, except while casting (a glowing aura: hits clang off).
    ///   Crescent Slash  wind-up, then a slash wave at ground height (jump it) or head height (Down Dash under / dash).
    ///   Earthsplitter   leaps onto the player, slams the sword into the floor: eruptions run both ways (double jump);
    ///                   the sword stays stuck: the main opening.
    ///   Royal Lance     the left hand charges, a beam tracks the player, locks and fires (step off the line).
    ///   Arcane Rings    rings of orbs with gaps; the gold ones can be punched back at him.
    ///   Blade Rain      spectral swords fall on marked spots around the player.
    ///   Lunge           a fast low thrust across the arena (jump over or dash through), then a long recovery.
    /// PHASE 3, TWIN BLADES (BossBase phase 1, at twin-blades HP). Untouchable transition: everything dispelled, he
    /// roars, tears off his cape and a second greatsword forms in his left hand. Then everything is faster:
    ///   Twin Crescent   two waves, one low and one high, one after the other in either order (double jump / Down Dash).
    ///   Whirlwind       spins across the arena with both blades (dash through or double jump over).
    ///   Double Earthsplitter  eruptions plus a shockwave ring that sweeps every height (dash through it).
    ///   Laser Grid      4-6 beams: a sweeping fan, sliding bars, or a turning pinwheel.
    ///   Royal Decree    below ~25% HP: a screen-filling storm of piercing needles and orb rings, short and dense.
    ///                   Serenity is effectively required, so it's announced and never comes back before Serenity
    ///                   could have recharged (duration + recharge + a margin, in real time).
    /// Defeat: he kneels, both swords shatter, the crown falls.
    ///
    /// Animation is procedural (VioletFigure on a Pose child, pivot at the feet). Art can read CurrentState instead.
    /// </summary>
    public class VioletBoss : BossBase
    {
        public enum Attack { CrescentSlash, Earthsplitter, RoyalLance, ArcaneRings, BladeRain, Lunge, TwinCrescent, Whirlwind, DoubleEarthsplitter, LaserGrid }

        public enum State { Throne, FarGesture, Descend, Idle, Walk, WindUp, Swing, Leap, Stuck, Cast, Lunge, Whirl, Recover, Transition, Decree, Defeated }

        /// <summary>Long-range gestures for the approach (he's far away; VioletApproach spawns the attacks).</summary>
        public enum Far { Slash, Slam, Cast, RaiseSword, Throw }

        // Each phase opens with its new pattern so the player sees it right away.
        static readonly Attack[][] Rotation =
        {
            new[] { Attack.CrescentSlash, Attack.Earthsplitter, Attack.RoyalLance, Attack.ArcaneRings, Attack.Lunge, Attack.BladeRain, Attack.CrescentSlash },
            new[] { Attack.TwinCrescent, Attack.Whirlwind, Attack.DoubleEarthsplitter, Attack.LaserGrid, Attack.Lunge, Attack.BladeRain, Attack.RoyalLance, Attack.ArcaneRings },
        };

        [Header("Arena (set by VioletApproach from the course)")]
        [Tooltip("Top of the arena floor.")]
        [SerializeField] float floorY;
        [Tooltip("Inner face of the arena's left wall.")]
        [SerializeField] float arenaMinX = -12f;
        [Tooltip("Inner face of the arena's right wall.")]
        [SerializeField] float arenaMaxX = 12f;

        [Header("Body (applied at runtime, so the boss is right even if the prefab wasn't updated)")]
        [Tooltip("Max HP (overrides Health.maxHealth).")]
        [SerializeField] int maxHealth = 24;
        [Tooltip("Twin Blades starts at this health fraction (the boss's only phase threshold).")]
        [SerializeField, Range(0.1f, 0.9f)] float twinBladesAt = 0.5f;
        [Tooltip("Body collider size (the procedural king is ~3.3 tall).")]
        [SerializeField] Vector2 bodySize = new(1.6f, 3.2f);

        [Header("Look")]
        [Tooltip("Lowest sorting order the procedural king uses.")]
        [SerializeField] int sortingBase = 6;
        [Tooltip("Color of his slashes, beams and eruptions.")]
        [SerializeField] Color attackColor = new(0.76f, 0.4f, 1f);
        [Tooltip("Tip color of his eruptions.")]
        [SerializeField] Color eruptionTip = new(0.95f, 0.85f, 1f);
        [Tooltip("Art/SFX: the second greatsword appears.")]
        [SerializeField] UnityEvent onTwinBlades = new();
        [Tooltip("Art/SFX: Royal Decree is announced.")]
        [SerializeField] UnityEvent onDecree = new();

        [Header("Movement")]
        [Tooltip("Walking speed, per phase.")]
        [SerializeField] float[] walkSpeedPerPhase = { 5f, 7f };
        [Tooltip("Seconds of rest after each attack, per phase.")]
        [SerializeField] float[] restTimePerPhase = { 0.9f, 0.6f };
        [Tooltip("Seconds of recovery after a big attack (he's open), per phase.")]
        [SerializeField] float[] recoverTimePerPhase = { 0.7f, 0.5f };

        [Header("Crescent Slash")]
        [Tooltip("Wind-up seconds (sword raised, blade glowing), per phase.")]
        [SerializeField] float[] slashWindUpPerPhase = { 0.75f, 0.55f };
        [Tooltip("How far from the player he gets before slashing.")]
        [SerializeField] float slashDistance = 7f;
        [Tooltip("Wave speed, per phase.")]
        [SerializeField] float[] waveSpeedPerPhase = { 12f, 15f };
        [Tooltip("Wave thickness (its hit band's width).")]
        [SerializeField] float waveThickness = 0.9f;
        [Tooltip("Low wave: from the floor up to this height. Jump it.")]
        [SerializeField] float lowWaveTop = 1.3f;
        [Tooltip("High wave: from this height (above a Down Dash surf: the low body is ~0.55 tall)...")]
        [SerializeField] float highWaveBottom = 0.75f;
        [Tooltip("...up to this height. Down Dash under it or dash through.")]
        [SerializeField] float highWaveTop = 2.6f;
        [Tooltip("Seconds between the two waves of Twin Crescent.")]
        [SerializeField] float twinWaveGap = 0.5f;

        [Header("Earthsplitter")]
        [Tooltip("Crouch before the leap.")]
        [SerializeField] float[] leapWindUpPerPhase = { 0.55f, 0.4f };
        [Tooltip("Leap time, per phase.")]
        [SerializeField] float[] leapTimePerPhase = { 0.65f, 0.55f };
        [Tooltip("Leap arc height.")]
        [SerializeField] float leapHeight = 4.5f;
        [Tooltip("Seconds the sword stays stuck in the floor after the slam: the main opening.")]
        [SerializeField] float[] stuckTimePerPhase = { 1.2f, 1f };
        [Tooltip("Eruptions on each side of the impact, per phase.")]
        [SerializeField] int[] eruptionsPerPhase = { 6, 8 };
        [Tooltip("Distance between eruptions.")]
        [SerializeField] float eruptionSpacing = 1.6f;
        [Tooltip("Warning before the first eruption.")]
        [SerializeField] float eruptionWarn = 0.35f;
        [Tooltip("Delay from one eruption to the next along the chain.")]
        [SerializeField] float eruptionStagger = 0.08f;
        [Tooltip("Eruption height: over a single jump (2.9), under a double jump (5.3).")]
        [SerializeField] float eruptionHeight = 3.6f;
        [Tooltip("Eruption width.")]
        [SerializeField] float eruptionWidth = 1f;
        [Tooltip("Seconds an eruption burns.")]
        [SerializeField] float eruptionBurn = 0.3f;
        [Tooltip("Twin Blades: shockwave rings from the impact (dash through them).")]
        [SerializeField] int ringsOnSlam = 2;
        [Tooltip("Shockwave ring speed.")]
        [SerializeField] float ringSpeed = 9f;
        [Tooltip("Shockwave ring reach.")]
        [SerializeField] float ringRadius = 13f;
        [Tooltip("Shockwave ring thickness.")]
        [SerializeField] float ringThickness = 0.5f;
        [Tooltip("Seconds between the shockwave rings.")]
        [SerializeField] float ringGap = 0.6f;

        [Header("Royal Lance")]
        [Tooltip("Beams per Lance, per phase.")]
        [SerializeField] int[] lanceBeamsPerPhase = { 1, 2 };
        [Tooltip("Seconds the aim line follows the player.")]
        [SerializeField] float[] lanceTrackPerPhase = { 1.1f, 0.85f };
        [Tooltip("How fast the aim line can turn, degrees per second.")]
        [SerializeField] float lanceTurnSpeed = 110f;
        [Tooltip("Seconds between the lock and the shot: step off the line.")]
        [SerializeField] float lanceLockTime = 0.35f;
        [Tooltip("Seconds the beam burns.")]
        [SerializeField] float lanceFireTime = 0.45f;
        [Tooltip("Beam width.")]
        [SerializeField] float lanceWidth = 0.9f;
        [Tooltip("Twin Blades also sweeps a low horizontal beam across the arena (Down Dash under): its bottom...")]
        [SerializeField] float sweepBottom = 0.7f;
        [Tooltip("...and top above the floor.")]
        [SerializeField] float sweepTop = 1.7f;
        [Tooltip("Warning and burn time of the low sweep.")]
        [SerializeField] Vector2 sweepWarnAndFire = new(0.85f, 0.9f);

        [Header("Arcane Rings")]
        [Tooltip("Orbs per ring, per phase.")]
        [SerializeField] int[] ringOrbsPerPhase = { 14, 16 };
        [Tooltip("Rings per cast, per phase.")]
        [SerializeField] int[] ringsPerPhase = { 2, 3 };
        [Tooltip("Consecutive orbs left out of each ring: the gap.")]
        [SerializeField] int ringGapOrbs = 3;
        [Tooltip("Every Nth orb is gold and can be punched back at him (0 = none).")]
        [SerializeField] int reflectableEvery = 4;
        [Tooltip("Orb speed.")]
        [SerializeField] float orbSpeed = 5.5f;
        [Tooltip("Seconds between rings.")]
        [SerializeField] float ringInterval = 0.55f;
        [Tooltip("Seconds of spiral after the rings, per phase (0 = none).")]
        [SerializeField] float[] spiralTimePerPhase = { 0f, 1.1f };
        [Tooltip("Cast wind-up (untouchable).")]
        [SerializeField] float castWindUp = 0.6f;

        [Header("Blade Rain")]
        [Tooltip("Swords per Blade Rain, per phase.")]
        [SerializeField] int[] rainSwordsPerPhase = { 6, 9 };
        [Tooltip("Seconds a spot is marked before its sword lands, per phase.")]
        [SerializeField] float[] rainWarnPerPhase = { 0.75f, 0.6f };
        [Tooltip("How far around the player the swords spread.")]
        [SerializeField] float rainSpread = 6f;
        [Tooltip("Seconds between swords.")]
        [SerializeField] float rainStagger = 0.12f;
        [Tooltip("Falling speed.")]
        [SerializeField] float swordSpeed = 26f;
        [Tooltip("Height above the floor they fall from.")]
        [SerializeField] float swordHeight = 11f;

        [Header("Lunge")]
        [Tooltip("Wind-up seconds (crouched, sword pointed), per phase.")]
        [SerializeField] float[] lungeWindUpPerPhase = { 0.7f, 0.5f };
        [Tooltip("Lunge speed.")]
        [SerializeField] float[] lungeSpeedPerPhase = { 26f, 32f };
        [Tooltip("Height of the thrust's hit band: under a single jump.")]
        [SerializeField] float lungeHeight = 1.7f;
        [Tooltip("Half width of the thrust's hit band around his center.")]
        [SerializeField] float lungeHalfWidth = 1.4f;

        [Header("Whirlwind")]
        [Tooltip("Wind-up seconds.")]
        [SerializeField] float whirlWindUp = 0.55f;
        [Tooltip("Travel speed while whirling.")]
        [SerializeField] float whirlSpeed = 11f;
        [Tooltip("Spins per second.")]
        [SerializeField] float whirlSpin = 3f;
        [Tooltip("Height of the whirl's hit band: under a double jump (5.3).")]
        [SerializeField] float whirlHeight = 3.4f;
        [Tooltip("Half width of the whirl's hit band.")]
        [SerializeField] float whirlHalfWidth = 2f;
        [Tooltip("Dizzy seconds after it (open).")]
        [SerializeField] float whirlRecover = 0.8f;

        [Header("Laser Grid")]
        [Tooltip("Beams in the fan.")]
        [SerializeField] int gridFanBeams = 5;
        [Tooltip("Total fan spread, degrees.")]
        [SerializeField] float gridFanSpread = 70f;
        [Tooltip("Degrees per second the fan sweeps while firing.")]
        [SerializeField] float gridFanSweep = 22f;
        [Tooltip("Vertical bars across the arena.")]
        [SerializeField] int gridBars = 4;
        [Tooltip("Bars slide sideways at this speed.")]
        [SerializeField] float gridBarSlide = 3f;
        [Tooltip("Pinwheel arms (turning around the arena center).")]
        [SerializeField] int gridPinwheelArms = 4;
        [Tooltip("Pinwheel turn speed, degrees per second.")]
        [SerializeField] float gridPinwheelTurn = 32f;
        [Tooltip("Warning time of the grid.")]
        [SerializeField] float gridWarn = 1f;
        [Tooltip("Burn time of the grid.")]
        [SerializeField] float gridFire = 1.4f;
        [Tooltip("Grid beam width.")]
        [SerializeField] float gridWidth = 0.7f;

        [Header("Royal Decree")]
        [Tooltip("It can come once his health is at or below this fraction.")]
        [SerializeField, Range(0f, 1f)] float decreeBelow = 0.25f;
        [Tooltip("Never closer together than this many real seconds; also at least Serenity's duration + recharge + decreeMargin.")]
        [SerializeField] float decreeMinSpacing = 18f;
        [Tooltip("Extra real seconds on top of Serenity's duration + recharge between two decrees.")]
        [SerializeField] float decreeMargin = 4f;
        [Tooltip("Seconds of announcement and charge before the storm (time to get ready to use Serenity).")]
        [SerializeField] float decreeCharge = 2.2f;
        [Tooltip("Seconds (game time) the storm lasts. In Serenity at 0.35x, 1.3 s is ~3.7 real seconds: inside its 4.")]
        [SerializeField] float decreeStorm = 1.3f;
        [Tooltip("Height above the floor he hovers at during the decree.")]
        [SerializeField] float decreeHeight = 6.5f;
        [Tooltip("Seconds between needle walls (they come from alternating sides).")]
        [SerializeField] float decreeWallInterval = 0.3f;
        [Tooltip("Vertical spacing of the needles in a wall.")]
        [SerializeField] float decreeNeedleSpacing = 0.45f;
        [Tooltip("Height of the gap in each wall.")]
        [SerializeField] float decreeGap = 1.9f;
        [Tooltip("Needle speed.")]
        [SerializeField] float decreeNeedleSpeed = 13f;
        [Tooltip("Needle damage (they pierce Dash's i-frames).")]
        [SerializeField] int decreeDamage = 1;
        [Tooltip("Highest the bottom of a wall's gap can be above the floor (3.4 = reachable from the platforms).")]
        [SerializeField] float decreeGapMaxBottom = 3.4f;
        [Tooltip("Seconds between orb rings during the storm.")]
        [SerializeField] float decreeRingInterval = 0.65f;
        [Tooltip("Orbs per ring during the storm.")]
        [SerializeField] int decreeRingOrbs = 12;
        [Tooltip("Subtitle under ROYAL DECREE.")]
        [SerializeField] string decreeSubtitle = "Breathe.";
        [Tooltip("The screen during the decree.")]
        [SerializeField] WarpLook decreeLook = new()
        {
            tint = new Color(0.55f, 0.2f, 0.9f, 0.18f), vignette = new Color(0.15f, 0f, 0.25f, 0.7f), chromatic = 0.006f, zoomPulse = 0.02f,
        };

        [Header("Twin Blades transition")]
        [Tooltip("Seconds of the roar before the cape comes off.")]
        [SerializeField] float roarTime = 0.9f;
        [Tooltip("Seconds the second sword takes to form.")]
        [SerializeField] float summonTime = 1.1f;

        [Header("Defeat")]
        [Tooltip("Seconds from the last hit to the swords shattering.")]
        [SerializeField] float shatterDelay = 0.9f;
        [Tooltip("Seconds from the last hit to the crown falling.")]
        [SerializeField] float crownDelay = 1.6f;

        readonly List<GameObject> spawned = new(); // everything in the air: dispelled on a transition / defeat
        VioletFigure figure;
        Collider2D bodyCollider;
        float halfHeight = 1.6f, halfWidth = 0.8f;
        State state = State.Throne;
        int cycle, gridCycle, lanceCycle;
        bool transitionPending, descendPending, untouchable, twinBlades, restoringCheckpoint, startOnFloor;
        float realClock, lastDecreeAt = float.NegativeInfinity;
        bool arenaKnown;

        public State CurrentState => state;
        public bool TwinBlades => twinBlades;
        public VioletFigure Figure => figure;
        public float GroundedY => floorY + halfHeight;

        public override BossPose Pose => state switch
        {
            State.WindUp or State.Swing or State.Cast or State.Lunge or State.Whirl or State.Decree or State.FarGesture => BossPose.Attack,
            State.Walk or State.Leap or State.Descend => BossPose.Move,
            State.Defeated => BossPose.Hurt,
            _ => base.Pose,
        };

        public override int FacingSign => figure ? figure.facing : base.FacingSign;

        bool Interrupted => transitionPending || Health.IsDead;
        float Center => (arenaMinX + arenaMaxX) * 0.5f;

        // ---------- Lifecycle ----------

        protected override void Awake()
        {
            base.Awake();
            Health.Configure(maxHealth);
            SetPhaseThresholds(twinBladesAt);
            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider is BoxCollider2D box)
            {
                box.size = bodySize;
                box.offset = Vector2.zero;

                halfHeight = box.size.y * 0.5f * transform.lossyScale.y;
                halfWidth = box.size.x * 0.5f * transform.lossyScale.x;
            }

            // The skeleton's placeholder square makes way for the procedural king.
            var visual = transform.Find("Visual");
            if (visual && visual.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite && sr.sprite.name == "Square") sr.enabled = false;

            figure = gameObject.AddComponent<VioletFigure>();
            figure.Build(transform, new Vector2(0f, -halfHeight / Mathf.Max(0.01f, transform.lossyScale.y)), sortingBase);
            ThronePose();
            Health.DamageFilter = FilterHit;
            VioletSetup.EnsureLevel(this); // the scene has no Violet setup (menu not run): build it now
        }

        // If nothing told him where the arena is, find the real ground under him: his feet are never in mid-air.
        void Start()
        {
            if (!arenaKnown) SnapToGround();
        }

        void SnapToGround()
        {
            Vector2 from = transform.position;
            float best = float.PositiveInfinity, groundY = from.y - halfHeight;
            foreach (var hit in Physics2D.RaycastAll(from + Vector2.up * 2f, Vector2.down, 80f))
            {
                var c = hit.collider;
                if (!c || c.isTrigger || c.transform.IsChildOf(transform) || c.GetComponentInParent<PlayerController>()) continue;
                if (hit.distance < best) { best = hit.distance; groundY = hit.point.y; }
            }
            floorY = groundY;
            arenaMinX = from.x - 12f;
            arenaMaxX = from.x + 12f;
            Teleport(new Vector2(from.x, GroundedY));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Health.Damaged += OnDamaged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Health.Damaged -= OnDamaged;
        }

        void OnDestroy()
        {
            ClearSpawned();
            if (Camera.main && Camera.main.TryGetComponent<ScreenWarp>(out var warp)) warp.Clear(0f);
        }

        void Update()
        {
            // Real seconds that aren't paused: the clock Royal Decree is spaced on (same clock as Serenity's timers).
            if (Game.Time == null || !Game.Time.IsPaused) realClock += Time.unscaledDeltaTime;
            if (figure && state != State.Whirl && state != State.Defeated && IsFighting && Player && state is State.Idle or State.Walk or State.Recover)
                figure.facing = Player.position.x < transform.position.x ? -1 : 1;
        }

        /// <summary>Called by VioletApproach before the fight: where the arena is.</summary>
        public void SetArena(float floor, float minX, float maxX)
        {
            floorY = floor;
            arenaMinX = minX;
            arenaMaxX = maxX;
            arenaKnown = true;
        }

        /// <summary>Stand with the feet at `feet` (the dais).</summary>
        public void PlaceAt(Vector2 feet)
        {
            var p = new Vector2(feet.x, feet.y + halfHeight);
            if (Body) Body.position = p;
            transform.position = p;
            figure.facing = -1;
        }

        protected override void OnFightStarted()
        {
            StopAllCoroutines(); // any long-range gesture from the approach (the brain starts right after this)
            descendPending = !startOnFloor;
            if (Player && bodyCollider)
                foreach (var c in Player.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(bodyCollider, c);
        }

        /// <summary>
        /// Checkpoint respawn: the duel starts with him already on the arena floor at `x` (no leap off the dais), and at
        /// Twin Blades if `twin` (health at its threshold, the cape already gone, no transition). Call right BEFORE
        /// StartFight: StartFight runs the first step of the fight immediately.
        /// </summary>
        public void PrepareFloorStart(float x, bool twin)
        {
            startOnFloor = true;
            Teleport(new Vector2(Mathf.Clamp(x, arenaMinX + 2f, arenaMaxX - 2f), GroundedY));
            IdlePose();
            figure.SnapArms();
            if (twin) RestoreTwinBlades();
        }

        void RestoreTwinBlades()
        {
            restoringCheckpoint = true;
            float threshold = PhaseThresholds.Count > 0 ? PhaseThresholds[0] : 0.5f;
            Health.SetCurrent(Mathf.Max(1, Mathf.FloorToInt(Health.Max * threshold)));
            restoringCheckpoint = false;
            transitionPending = false;
            twinBlades = true;
            figure.RemoveCape();
            figure.secondSword = 1f;
        }

        protected override void OnPhaseChanged(int newPhase)
        {
            if (newPhase < 1 || twinBlades) return;
            if (restoringCheckpoint) return; // RestoreTwinBlades sets it up without the show
            transitionPending = true;
            Untouchable(true);
            Dispel();
        }

        protected override IEnumerator RunPhase(int phase)
        {
            if (transitionPending) { yield return Transition(); yield break; }
            if (descendPending) { yield return Descend(); yield break; }
            if (twinBlades && DecreeDue) { yield return RoyalDecree(phase); yield break; }

            var rotation = Rotation[Mathf.Min(phase, Rotation.Length - 1)];
            switch (rotation[cycle++ % rotation.Length])
            {
                case Attack.CrescentSlash: yield return CrescentSlash(phase); break;
                case Attack.Earthsplitter: yield return Earthsplitter(phase, false); break;
                case Attack.RoyalLance: yield return RoyalLance(phase); break;
                case Attack.ArcaneRings: yield return ArcaneRings(phase); break;
                case Attack.BladeRain: yield return BladeRain(phase); break;
                case Attack.Lunge: yield return Lunge(phase); break;
                case Attack.TwinCrescent: yield return TwinCrescent(phase); break;
                case Attack.Whirlwind: yield return Whirlwind(phase); break;
                case Attack.DoubleEarthsplitter: yield return Earthsplitter(phase, true); break;
                case Attack.LaserGrid: yield return LaserGrid(phase); break;
            }
            if (Interrupted) yield break;
            state = State.Idle;
            IdlePose();
            yield return WaitOrInterrupt(PerPhase(restTimePerPhase, phase));
        }

        protected override void OnDefeated()
        {
            state = State.Defeated;
            transitionPending = false;
            untouchable = false;
            Dispel();
            if (Camera.main && Camera.main.TryGetComponent<ScreenWarp>(out var warp)) { warp.Clear(1.2f); warp.Flash(new Color(1f, 0.9f, 1f, 0.7f), 0.8f); }
            StartCoroutine(DeathSequence());
        }

        // ---------- Hits ----------

        bool FilterHit(DamageInfo info)
        {
            if (IsFighting && !untouchable && state != State.Defeated) return true;
            // Can't be hurt (far away, casting, transforming): it clangs off the aura.
            var at = info.source ? (Vector2)info.source.transform.position : (Vector2)transform.position;
            VioletHits.Burst(Vector2.Lerp(transform.position, at, 0.6f), VioletShapes.Glow, 5, 4f, 0.25f);
            if (figure) figure.Jolt(0.3f);
            return false;
        }

        void OnDamaged(DamageInfo _)
        {
            figure.Flash();
            VioletHits.ShakeCamera(0.12f, 0.15f);
        }

        void Untouchable(bool on)
        {
            untouchable = on;
            figure.aura = on ? 1f : 0f;
        }

        bool DecreeDue => Health.Fraction <= decreeBelow && realClock - lastDecreeAt >= DecreeSpacing;

        float DecreeSpacing
        {
            get
            {
                float spacing = decreeMinSpacing;
                var pc = PlayerController.Instance;
                if (pc && pc.Loadout.Get(AbilityId.Serenity) is SerenityAbility serenity)
                    spacing = Mathf.Max(spacing, serenity.Duration + serenity.RechargeTime + decreeMargin);
                return spacing;
            }
        }

        // ---------- The approach (phase 1): poses only, VioletApproach spawns the attacks ----------

        /// <summary>The intro: attack stance on the dais, sword raised, cape billowing.</summary>
        public void IntroStance(bool on)
        {
            if (on)
            {
                state = State.FarGesture;
                figure.swordArm = 172f;
                figure.swordTwist = -8f;
                figure.offArm = 35f;
                figure.lean = -6f;
                figure.crouch = 0.12f;
                figure.capeWind = 1.2f;
                figure.bladeGlow = 0.6f;
                figure.SnapArms();
            }
            else ThronePose();
        }

        public void Glint() => figure.Glint();

        /// <summary>A long-range gesture while the player is still on the course: wind up for `windUp`, then the motion.</summary>
        public void FarGesture(Far move, float windUp)
        {
            if (IsFighting || Health.IsDead) return;
            StopAllCoroutines();
            StartCoroutine(FarRoutine(move, windUp));
        }

        IEnumerator FarRoutine(Far move, float windUp)
        {
            state = State.FarGesture;
            figure.armResponse = 9f;
            switch (move)
            {
                case Far.Slash:
                    figure.swordArm = 205f; figure.swordTwist = 0f; figure.bladeGlow = 1f; figure.lean = -8f; figure.crouch = 0.25f;
                    yield return new WaitForSeconds(windUp);
                    figure.armResponse = 45f; figure.swordArm = -25f; figure.lean = 22f;
                    break;
                case Far.Slam:
                    figure.swordArm = 190f; figure.swordTwist = 0f; figure.crouch = 0.6f; figure.bladeGlow = 1f;
                    yield return new WaitForSeconds(windUp);
                    figure.armResponse = 50f; figure.swordArm = 55f; figure.swordTwist = 50f; figure.crouch = 0.75f; figure.lean = 25f;
                    break;
                case Far.Cast:
                    figure.offArm = 105f; figure.handGlow = 1f; figure.capeWind = 0.6f; figure.lean = -4f;
                    yield return new WaitForSeconds(windUp);
                    figure.armResponse = 30f; figure.offArm = 125f;
                    break;
                case Far.RaiseSword:
                    figure.swordArm = 178f; figure.swordTwist = 0f; figure.bladeGlow = 1f; figure.offArm = 150f; figure.handGlow = 1f; figure.capeWind = 1f;
                    yield return new WaitForSeconds(windUp);
                    figure.armResponse = 30f; figure.swordArm = 120f;
                    break;
                case Far.Throw:
                    figure.offArm = 170f; figure.handGlow = 1f;
                    yield return new WaitForSeconds(windUp);
                    figure.armResponse = 35f; figure.offArm = 70f;
                    break;
            }
            yield return new WaitForSeconds(0.45f);
            ThronePose();
        }

        void ThronePose()
        {
            state = IsFighting ? State.Idle : State.Throne;
            figure.armResponse = 6f;
            figure.swordArm = 28f; figure.swordTwist = -38f;
            figure.offArm = 14f; figure.offTwist = -20f;
            figure.lean = -3f; figure.crouch = 0f; figure.capeWind = 0.25f;
            figure.bladeGlow = 0f; figure.handGlow = 0f;
        }

        void IdlePose()
        {
            figure.armResponse = 10f;
            figure.swordArm = 32f; figure.swordTwist = -40f;
            figure.offArm = twinBlades ? 32f : 14f; figure.offTwist = twinBlades ? -40f : -20f;
            figure.lean = 4f; figure.crouch = 0.15f; figure.capeWind = 0f;
            figure.bladeGlow = 0f; figure.handGlow = 0f; figure.spinSpeed = 0f; figure.headTilt = 0f;
        }

        // ---------- Duel: openings ----------

        IEnumerator Descend()
        {
            descendPending = false;
            state = State.Descend;
            float x = Mathf.Clamp(Center + 4f, arenaMinX + 2f, arenaMaxX - 2f);
            figure.facing = Player && Player.position.x < x ? -1 : 1;
            figure.crouch = 0.6f;
            yield return Wait(0.35f);
            figure.swordArm = 160f;
            yield return LeapTo(x, 3.5f, 0.75f, false);
            Land(0.35f);
            // A short roar to open the duel.
            figure.lean = -14f; figure.headTilt = -12f; figure.swordArm = 120f; figure.offArm = 120f; figure.capeWind = 1f;
            VioletHits.ShakeCamera(0.3f, 0.6f);
            yield return Wait(0.8f);
            IdlePose();
            state = State.Idle;
        }

        // ---------- Duel: attacks ----------

        IEnumerator CrescentSlash(int phase)
        {
            yield return Approach(slashDistance, phase);
            if (Interrupted || !Player) yield break;
            bool high = (cycle / Rotation[0].Length) % 2 == 1 ? Random.value < 0.6f : Random.value < 0.4f;
            int waves = phase == 0 && cycle % 3 == 0 ? 2 : 1;
            for (int w = 0; w < waves && !Interrupted; w++)
            {
                yield return SlashWindUp(PerPhase(slashWindUpPerPhase, phase), high ? new[] { true } : new[] { false }, false);
                if (Interrupted) yield break;
                Swing(false);
                SpawnWave(high, phase);
                high = !high;
                yield return WaitOrInterrupt(0.35f);
            }
            yield return Recover(phase);
        }

        IEnumerator TwinCrescent(int phase)
        {
            yield return Approach(slashDistance + 1f, phase);
            if (Interrupted || !Player) yield break;
            bool highFirst = Random.value < 0.5f;
            yield return SlashWindUp(PerPhase(slashWindUpPerPhase, phase) + 0.15f, new[] { highFirst, !highFirst }, true);
            if (Interrupted) yield break;
            Swing(true);
            CrossFlash();
            SpawnWave(highFirst, phase);
            yield return WaitOrInterrupt(twinWaveGap);
            if (Interrupted) yield break;
            SpawnWave(!highFirst, phase);
            yield return Recover(phase);
        }

        IEnumerator SlashWindUp(float time, bool[] heights, bool both)
        {
            state = State.WindUp;
            FacePlayer();
            figure.armResponse = 9f;
            figure.swordArm = 205f; figure.swordTwist = 0f; figure.bladeGlow = 1f; figure.lean = -8f; figure.crouch = 0.25f;
            if (both) { figure.offArm = 195f; figure.offTwist = 0f; }
            int dir = figure.facing;
            float from = transform.position.x + dir * 1.4f, to = dir > 0 ? arenaMaxX : arenaMinX;
            foreach (bool h in heights)
                Track(VioletTelegraph.Lane(from, to, floorY + (h ? highWaveBottom : 0f), floorY + (h ? highWaveTop : lowWaveTop), time, attackColor).gameObject);
            yield return WaitOrInterrupt(time);
        }

        void Swing(bool both)
        {
            state = State.Swing;
            figure.armResponse = 45f;
            figure.swordArm = -25f; figure.lean = 22f; figure.crouch = 0.35f;
            if (both) figure.offArm = -25f;
            VioletHits.ShakeCamera(0.12f, 0.15f);
        }

        void SpawnWave(bool high, int phase)
        {
            int dir = figure.facing;
            float x = transform.position.x + dir * (halfWidth + 0.8f);
            float bottom = floorY + (high ? highWaveBottom : 0f), top = floorY + (high ? highWaveTop : lowWaveTop);
            Track(VioletWave.Spawn(x, bottom, top, dir, PerPhase(waveSpeedPerPhase, phase), dir > 0 ? arenaMaxX : arenaMinX, waveThickness, 1, attackColor).gameObject);
        }

        void CrossFlash()
        {
            // The two blades cross: an X of light where they meet.
            var at = figure.Chest + new Vector2(figure.facing * 1.2f, 0f);
            for (int i = 0; i < 2; i++)
            {
                var sr = VioletShapes.Create("CrossSlash", VioletShapes.Crescent, null, at, VioletShapes.Glow, 30);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, i == 0 ? 35f : -35f);
                sr.transform.localScale = new Vector3(1.2f, 3.2f, 1f);
                sr.gameObject.AddComponent<VioletDebris>().Launch(Vector2.zero, 0.35f, 0.01f).NoGravity();
            }
        }

        IEnumerator Earthsplitter(int phase, bool twin)
        {
            if (!Player) yield break;
            state = State.WindUp;
            FacePlayer();
            figure.armResponse = 10f;
            figure.crouch = 0.65f; figure.swordArm = 190f; figure.swordTwist = 0f; figure.bladeGlow = 1f;
            if (twin) { figure.offArm = 190f; figure.offTwist = 0f; }
            yield return WaitOrInterrupt(PerPhase(leapWindUpPerPhase, phase));
            if (Interrupted || !Player) yield break;

            float target = Mathf.Clamp(Player.position.x, arenaMinX + halfWidth + 0.5f, arenaMaxX - halfWidth - 0.5f);
            float leapTime = PerPhase(leapTimePerPhase, phase);
            Track(VioletTelegraph.Spot(new Vector2(target, floorY + 0.06f), new Vector2(2.6f, 0.14f), leapTime, attackColor).gameObject);
            state = State.Leap;
            figure.crouch = 0f;
            yield return LeapTo(target, leapHeight, leapTime, true);
            if (Interrupted) yield break;

            // SLAM: the blade (both blades) bite into the floor.
            state = State.Stuck;
            figure.armResponse = 55f;
            figure.swordArm = 58f; figure.swordTwist = 48f; figure.crouch = 0.75f; figure.lean = 26f;
            if (twin) { figure.offArm = 58f; figure.offTwist = 48f; }
            Land(0.5f);
            if (ScreenWarp.Main is { } warp) { warp.Shockwave(new Vector2(target, floorY), 0.8f, 0.6f); warp.Pulse(0.6f, 0.3f); }
            if (VioletHits.RectTouchesPlayer(new Rect(target - halfWidth, floorY, halfWidth * 2f, halfHeight * 2f)))
                VioletHits.Hurt(1, new Vector2(Mathf.Sign(Player.position.x - target) * 9f, 6f), gameObject);

            int count = PerPhase(eruptionsPerPhase, phase);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < count; i++)
                {
                    float x = target + side * (1.4f + i * eruptionSpacing);
                    if (x < arenaMinX + eruptionWidth * 0.5f || x > arenaMaxX - eruptionWidth * 0.5f) break;
                    Track(FirePatch.Spawn(x, floorY, eruptionWidth, eruptionHeight, eruptionWarn + i * eruptionStagger, eruptionBurn, attackColor, eruptionTip).gameObject);
                }
            if (twin) StartCoroutine(Rings(new Vector2(target, floorY)));

            yield return WaitOrInterrupt(PerPhase(stuckTimePerPhase, phase)); // the opening
            if (Interrupted) yield break;
            figure.armResponse = 14f;
            figure.crouch = 0.2f; figure.lean = 4f; figure.swordArm = 110f; figure.swordTwist = -20f;
            if (twin) figure.offArm = 110f;
            VioletHits.Burst(new Vector2(target + figure.facing * 1.2f, floorY), new Color(0.6f, 0.5f, 0.7f, 0.7f), 6, 3f, 0.4f);
            yield return WaitOrInterrupt(0.35f);
        }

        IEnumerator Rings(Vector2 at)
        {
            for (int i = 0; i < ringsOnSlam && !Interrupted; i++)
            {
                if (i > 0) yield return WaitOrInterrupt(ringGap);
                if (Interrupted) yield break;
                Track(VioletRing.Spawn(at, ringRadius, ringSpeed, ringThickness, 1, attackColor).gameObject);
            }
        }

        IEnumerator RoyalLance(int phase)
        {
            yield return Approach(9f, phase);
            if (Interrupted || !Player) yield break;
            state = State.Cast;
            FacePlayer();
            Untouchable(true);
            figure.armResponse = 10f;
            figure.offArm = 102f; figure.offTwist = 0f; figure.handGlow = 1f; figure.capeWind = 0.5f; figure.lean = -4f;
            yield return WaitOrInterrupt(0.35f);

            bool sweep = twinBlades && lanceCycle++ % 2 == 1;
            if (sweep)
            {
                // A low beam straight across the arena: only a Down Dash surf fits under it.
                float y0 = floorY + sweepBottom, y1 = floorY + sweepTop;
                var area = Rect.MinMaxRect(arenaMinX, y0, arenaMaxX, y1);
                var beam = Track(VioletBeam.Band(area, sweepWarnAndFire.x, sweepWarnAndFire.y, 1, attackColor));
                while (beam && !Interrupted) yield return null;
            }
            else
            {
                int beams = PerPhase(lanceBeamsPerPhase, phase);
                for (int b = 0; b < beams && !Interrupted && Player; b++)
                {
                    var hand = figure.OffHand;
                    var to = (Vector2)Player.position - hand;
                    var beam = Track(VioletBeam.Spawn(hand, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, 45f, lanceWidth,
                        PerPhase(lanceTrackPerPhase, phase) + lanceLockTime, lanceFireTime, 1, attackColor).Tracking(lanceTurnSpeed, lanceLockTime));
                    while (beam && !beam.Firing && !Interrupted)
                    {
                        figure.offArm = ArmAngleFor(beam.Angle); // the hand follows its own aim line
                        yield return null;
                    }
                    VioletHits.ShakeCamera(0.15f, 0.25f);
                    while (beam && !Interrupted) yield return null;
                }
            }
            Untouchable(false);
            figure.handGlow = 0f;
            yield return Recover(phase);
        }

        IEnumerator ArcaneRings(int phase)
        {
            state = State.Cast;
            FacePlayer();
            Untouchable(true);
            figure.offArm = 160f; figure.handGlow = 1f; figure.capeWind = 0.8f;
            yield return WaitOrInterrupt(castWindUp);
            Untouchable(false); // the orbs are out: he's open while they fly
            int rings = PerPhase(ringsPerPhase, phase), count = Mathf.Max(4, PerPhase(ringOrbsPerPhase, phase));
            float step = 360f / count, offset = Random.Range(0f, step);
            for (int r = 0; r < rings && !Interrupted; r++)
            {
                if (r > 0) yield return WaitOrInterrupt(ringInterval);
                if (Interrupted) yield break;
                int gapAt = Random.Range(0, count);
                for (int i = 0; i < count; i++)
                {
                    int fromGap = (i - gapAt + count) % count;
                    if (fromGap < ringGapOrbs) continue;
                    bool gold = reflectableEvery > 0 && i % reflectableEvery == 0;
                    FireOrb(FromAngle(offset + r * step * 0.5f + i * step), figure.Chest, gold);
                }
                figure.Jolt();
            }
            float spiral = PerPhase(spiralTimePerPhase, phase), angle = Random.Range(0f, 360f);
            for (float t = 0f; t < spiral && !Interrupted; t += 0.12f)
            {
                for (int a = 0; a < 3; a++) FireOrb(FromAngle(angle + a * 120f), figure.Chest, false);
                angle += 17f;
                yield return WaitOrInterrupt(0.12f);
            }
            figure.handGlow = 0f;
            yield return Recover(phase);
        }

        IEnumerator BladeRain(int phase)
        {
            if (!Player) yield break;
            state = State.Cast;
            FacePlayer();
            Untouchable(true);
            figure.swordArm = 178f; figure.swordTwist = 0f; figure.bladeGlow = 1f; figure.offArm = 150f; figure.handGlow = 1f; figure.capeWind = 1f;
            yield return WaitOrInterrupt(0.5f);
            int count = PerPhase(rainSwordsPerPhase, phase);
            float warn = PerPhase(rainWarnPerPhase, phase);
            for (int i = 0; i < count && !Interrupted && Player; i++)
            {
                float x = i == 0 ? Player.position.x : Player.position.x + Random.Range(-rainSpread, rainSpread);
                x = Mathf.Clamp(x, arenaMinX + 0.4f, arenaMaxX - 0.4f);
                StartCoroutine(DropSword(x, warn));
                yield return WaitOrInterrupt(rainStagger);
            }
            Untouchable(false);
            yield return WaitOrInterrupt(warn);
            figure.bladeGlow = 0f; figure.handGlow = 0f;
            yield return Recover(phase);
        }

        IEnumerator DropSword(float x, float warn)
        {
            float fall = (swordHeight - 1f) / swordSpeed;
            Track(VioletTelegraph.Spot(new Vector2(x, floorY + 0.06f), new Vector2(0.55f, 0.12f), warn, attackColor).gameObject);
            yield return WaitOrInterrupt(Mathf.Max(0f, warn - fall));
            if (Interrupted) yield break;
            Track(VioletShots.Sword(new Vector2(x, floorY + swordHeight), swordSpeed, new Color(0.85f, 0.75f, 1f, 0.85f)).gameObject);
        }

        IEnumerator Lunge(int phase)
        {
            if (!Player) yield break;
            // From the end of the arena away from the player, across all of it.
            bool fromRight = Player.position.x < Center;
            float startX = fromRight ? arenaMaxX - 2f : arenaMinX + 2f, endX = fromRight ? arenaMinX + 2f : arenaMaxX - 2f;
            if (Mathf.Abs(transform.position.x - startX) > 1f) yield return LeapTo(startX, 2.5f, 0.55f, true);
            if (Interrupted || !Player) yield break;

            state = State.WindUp;
            figure.facing = fromRight ? -1 : 1;
            figure.armResponse = 12f;
            figure.crouch = 0.75f; figure.lean = 32f; figure.swordArm = 92f; figure.swordTwist = 0f; figure.bladeGlow = 1f;
            float windUp = PerPhase(lungeWindUpPerPhase, phase);
            Track(VioletTelegraph.Lane(startX, endX, floorY, floorY + lungeHeight, windUp, attackColor).gameObject);
            yield return WaitOrInterrupt(windUp);
            if (Interrupted) yield break;

            state = State.Lunge;
            float speed = PerPhase(lungeSpeedPerPhase, phase), dir = Mathf.Sign(endX - startX);
            bool hit = false;
            while ((endX - Body.position.x) * dir > 0f && !Interrupted)
            {
                yield return new WaitForFixedUpdate();
                float x = Body.position.x + dir * speed * Time.fixedDeltaTime;
                if ((endX - x) * dir < 0f) x = endX;
                Body.MovePosition(new Vector2(x, GroundedY));
                if (Random.value < 0.7f) VioletHits.Puff(new Vector2(x - dir, floorY + 0.1f), new Vector2(-dir * 3f, 1f), 0.3f, 0.6f, new Color(0.6f, 0.5f, 0.7f, 0.5f), 0.4f, 8);
                if (!hit && VioletHits.RectTouchesPlayer(new Rect(x - lungeHalfWidth, floorY, lungeHalfWidth * 2f, lungeHeight)))
                    hit = VioletHits.Hurt(1, new Vector2(dir * 10f, 6f), gameObject);
            }
            figure.crouch = 0.4f; figure.lean = 10f; figure.swordArm = 60f; figure.bladeGlow = 0f;
            yield return Recover(phase, 0.3f);
        }

        IEnumerator Whirlwind(int phase)
        {
            if (!Player) yield break;
            state = State.WindUp;
            FacePlayer();
            int dir = figure.facing;
            float endX = dir > 0 ? arenaMaxX - halfWidth - 0.5f : arenaMinX + halfWidth + 0.5f;
            figure.armResponse = 12f;
            figure.swordArm = 92f; figure.swordTwist = 0f; figure.offArm = 92f; figure.offTwist = 0f; figure.bladeGlow = 1f; figure.crouch = 0.3f;
            Track(VioletTelegraph.Lane(transform.position.x, endX, floorY, floorY + whirlHeight, whirlWindUp, attackColor).gameObject);
            for (float t = 0f; t < whirlWindUp && !Interrupted; t += Time.deltaTime)
            {
                figure.spinSpeed = Mathf.Lerp(0.3f, whirlSpin * 0.5f, t / whirlWindUp);
                yield return null;
            }
            if (Interrupted) { figure.spinSpeed = 0f; yield break; }

            state = State.Whirl;
            figure.spinSpeed = whirlSpin;
            bool hit = false;
            while ((endX - Body.position.x) * dir > 0f && !Interrupted)
            {
                yield return new WaitForFixedUpdate();
                float x = Body.position.x + dir * whirlSpeed * Time.fixedDeltaTime;
                if ((endX - x) * dir < 0f) x = endX;
                Body.MovePosition(new Vector2(x, GroundedY));
                if (Random.value < 0.5f) VioletHits.Puff(new Vector2(x, floorY + Random.Range(0.5f, whirlHeight)), Random.insideUnitCircle * 4f, 0.25f, 0.05f, attackColor, 0.3f, 25);
                if (!hit && VioletHits.RectTouchesPlayer(new Rect(x - whirlHalfWidth, floorY, whirlHalfWidth * 2f, whirlHeight)))
                    hit = VioletHits.Hurt(1, new Vector2(dir * 9f, 7f), gameObject);
            }
            figure.spinSpeed = 0f;
            figure.facing = dir;
            state = State.Recover;
            figure.crouch = 0.45f; figure.headTilt = 10f; figure.bladeGlow = 0f; figure.swordArm = 20f; figure.offArm = 20f;
            yield return WaitOrInterrupt(whirlRecover);
            figure.headTilt = 0f;
        }

        IEnumerator LaserGrid(int phase)
        {
            state = State.Cast;
            Untouchable(true);
            figure.swordArm = 175f; figure.offArm = 175f; figure.bladeGlow = 1f; figure.handGlow = 1f; figure.capeWind = 1f;
            yield return WalkTo(Center, PerPhase(walkSpeedPerPhase, phase) * 1.5f);
            if (Interrupted || !Player) yield break;
            FacePlayer();

            var beams = new List<VioletBeam>();
            float top = floorY + 13f;
            switch (gridCycle++ % 3)
            {
                case 0: // Fan from his hand, aimed at the player, sweeping.
                {
                    var hand = figure.OffHand;
                    var to = (Vector2)Player.position - hand;
                    float aim = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                    float sweepDir = Random.value < 0.5f ? 1f : -1f;
                    for (int i = 0; i < gridFanBeams; i++)
                    {
                        float a = aim + Mathf.Lerp(-gridFanSpread, gridFanSpread, gridFanBeams == 1 ? 0.5f : i / (gridFanBeams - 1f)) * 0.5f;
                        beams.Add(VioletBeam.Spawn(hand, a, 40f, gridWidth, gridWarn, gridFire, 1, attackColor).Sweeping(gridFanSweep * sweepDir));
                    }
                    break;
                }
                case 1: // Bars falling from the ceiling, sliding sideways together: walk with a gap.
                {
                    float width = arenaMaxX - arenaMinX, gap = width / gridBars, slide = (Random.value < 0.5f ? 1f : -1f) * gridBarSlide;
                    float shift = Random.Range(0f, gap);
                    for (int i = 0; i < gridBars + 1; i++)
                    {
                        float x = arenaMinX + shift + i * gap - gap * 0.5f;
                        beams.Add(VioletBeam.Spawn(new Vector2(x, top), -90f, top - floorY + 0.5f, gridWidth, gridWarn, gridFire, 1, attackColor).Sliding(new Vector2(slide, 0f)));
                    }
                    break;
                }
                default: // Pinwheel: arms turning around a point over the arena's center.
                {
                    var center = new Vector2(Center, floorY + 4f);
                    float offset = Random.Range(0f, 360f / gridPinwheelArms), turn = (Random.value < 0.5f ? 1f : -1f) * gridPinwheelTurn;
                    for (int i = 0; i < gridPinwheelArms; i++)
                        beams.Add(VioletBeam.Spawn(center, offset + i * 360f / gridPinwheelArms, 22f, gridWidth, gridWarn, gridFire, 1, attackColor).Sweeping(turn));
                    break;
                }
            }
            foreach (var b in beams) Track(b);
            float until = gridWarn + gridFire + 0.2f;
            for (float t = 0f; t < until && !Interrupted; t += Time.deltaTime) yield return null;
            Untouchable(false);
            figure.handGlow = 0f; figure.bladeGlow = 0f;
            yield return Recover(phase);
        }

        IEnumerator RoyalDecree(int phase)
        {
            lastDecreeAt = realClock;
            state = State.Decree;
            figure.spinSpeed = 0f;
            Untouchable(true);
            Dispel();
            onDecree.Invoke();
            GameEvents.RaiseTitleCardShown("ROYAL DECREE", decreeSubtitle);
            figure.swordArm = 175f; figure.offArm = 175f; figure.bladeGlow = 1f; figure.handGlow = 1f; figure.capeWind = 1f; figure.lean = -10f;
            var warp = ScreenWarp.Main;
            if (warp) warp.BlendTo(decreeLook, 0.6f);

            var hover = new Vector2(Center, floorY + decreeHeight);
            yield return Glide(hover, 0.8f);
            for (float t = 0f; t < decreeCharge - 0.8f && !Health.IsDead; t += Time.deltaTime)
            {
                if (Random.value < 0.5f)
                {
                    var from = (Vector2)transform.position + Random.insideUnitCircle.normalized * Random.Range(3f, 6f);
                    VioletHits.Puff(from, ((Vector2)transform.position - from) * 2f, 0.3f, 0.05f, VioletShapes.Glow, 0.45f, 25);
                }
                yield return null;
            }
            if (warp) warp.Pulse(1.2f, 0.4f);
            VioletHits.ShakeCamera(0.35f, 0.4f);

            // The storm: walls of piercing needles from alternating sides, each with one gap, and rings of orbs.
            var bounds = Rect.MinMaxRect(arenaMinX - 0.5f, floorY - 0.5f, arenaMaxX + 0.5f, floorY + 14f);
            float nextWall = 0f, nextRing = 0.15f;
            int side = Random.value < 0.5f ? 1 : -1;
            for (float t = 0f; t < decreeStorm && !Health.IsDead; t += Time.deltaTime)
            {
                if (t >= nextWall)
                {
                    nextWall += decreeWallInterval;
                    NeedleWall(side, bounds);
                    side = -side;
                }
                if (t >= nextRing)
                {
                    nextRing += decreeRingInterval;
                    int count = Mathf.Max(3, decreeRingOrbs);
                    float offset = Random.Range(0f, 360f);
                    for (int i = 0; i < count; i++) FireOrb(FromAngle(offset + i * 360f / count), transform.position, false, 4.5f);
                }
                yield return null;
            }
            yield return WaitOrInterrupt(0.6f);
            if (warp) warp.Clear(0.8f);
            figure.bladeGlow = 0f; figure.handGlow = 0f; figure.capeWind = 0f;
            yield return Glide(new Vector2(Center, GroundedY), 0.6f);
            Land(0.3f);
            Untouchable(false);
            yield return Recover(phase, 0.6f);
        }

        void NeedleWall(int fromSide, Rect bounds)
        {
            float x = fromSide > 0 ? arenaMinX + 0.2f : arenaMaxX - 0.2f;
            float gapBottom = floorY + Random.Range(0f, decreeGapMaxBottom);
            for (float y = floorY + 0.25f; y < floorY + 8f; y += decreeNeedleSpacing)
            {
                if (y > gapBottom && y < gapBottom + decreeGap) continue;
                Track(VioletNeedle.Spawn(new Vector2(x, y), new Vector2(fromSide * decreeNeedleSpeed, 0f), 0.75f, decreeDamage, bounds, true, 3f).gameObject);
            }
        }

        // ---------- Twin Blades ----------

        IEnumerator Transition()
        {
            transitionPending = false;
            state = State.Transition;
            Untouchable(true);
            Dispel();
            figure.spinSpeed = 0f; // a Whirlwind it interrupted
            if (Body) Body.position = new Vector2(Body.position.x, GroundedY);
            GameEvents.RaiseTitleCardShown("TWIN BLADES", "The king draws his second sword.");

            // The roar.
            figure.armResponse = 8f;
            figure.lean = -16f; figure.headTilt = -14f; figure.swordArm = 135f; figure.offArm = 135f; figure.capeWind = 1.5f; figure.crouch = 0.1f;
            VioletHits.ShakeCamera(0.45f, roarTime);
            if (ScreenWarp.Main is { } warp) warp.Pulse(1f, roarTime);
            yield return Wait(roarTime);

            // Tears the cape off.
            figure.armResponse = 30f;
            figure.offArm = 210f;
            yield return Wait(0.15f);
            figure.DetachCape(floorY);
            figure.offArm = 60f;
            VioletHits.Burst(figure.Chest, VioletShapes.Deep, 10, 5f, 0.5f);
            yield return Wait(0.5f);

            // Left hand up: light gathers into a second greatsword.
            figure.armResponse = 8f;
            figure.lean = -4f; figure.headTilt = 0f; figure.offArm = 172f; figure.offTwist = 0f; figure.handGlow = 1f;
            for (float t = 0f; t < summonTime; t += Time.deltaTime)
            {
                var hand = figure.OffHand;
                if (Random.value < 0.8f)
                {
                    var from = hand + Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f);
                    VioletHits.Puff(from, (hand - from) * 2.4f, 0.28f, 0.05f, VioletShapes.Glow, 0.42f, 25);
                }
                figure.secondSword = Mathf.Clamp01((t - summonTime * 0.4f) / (summonTime * 0.6f));
                yield return null;
            }
            figure.secondSword = 1f;
            twinBlades = true;
            onTwinBlades.Invoke();
            if (ScreenWarp.Main is { } flash) flash.Flash(new Color(0.95f, 0.8f, 1f, 0.7f), 0.5f);
            VioletHits.ShakeCamera(0.5f, 0.5f);
            VioletHits.Burst(figure.OffHand, UnityEngine.Color.white, 16, 8f, 0.5f);
            figure.handGlow = 0f;
            figure.offArm = 40f;
            yield return Wait(0.5f);
            Untouchable(false);
            cycle = 0;
            IdlePose();
            state = State.Idle;
        }

        // ---------- Defeat ----------

        IEnumerator DeathSequence()
        {
            figure.armResponse = 6f;
            figure.spinSpeed = 0f;
            figure.aura = 0f;
            figure.bladeGlow = 0f; figure.handGlow = 0f;
            figure.lean = -12f; figure.headTilt = -10f;
            if (Body) Body.position = new Vector2(Body.position.x, GroundedY);
            VioletHits.ShakeCamera(0.5f, 0.6f);
            yield return new WaitForSeconds(0.35f);
            figure.kneel = 1f; figure.crouch = 0.3f; figure.lean = 18f; figure.headTilt = 25f;
            figure.swordArm = 70f; figure.swordTwist = 40f; figure.offArm = 20f;
            yield return new WaitForSeconds(Mathf.Max(0f, shatterDelay - 0.35f));
            figure.ShatterSwords();
            VioletHits.ShakeCamera(0.3f, 0.3f);
            yield return new WaitForSeconds(Mathf.Max(0f, crownDelay - shatterDelay));
            figure.DropCrown(floorY);
            figure.swordArm = 5f; figure.offArm = 5f;
        }

        // ---------- Movement ----------

        IEnumerator Approach(float distance, int phase)
        {
            if (!Player) yield break;
            float side = Mathf.Sign(transform.position.x - Player.position.x);
            if (side == 0f) side = 1f;
            float x = Player.position.x + side * distance;
            if (x < arenaMinX + 1.5f || x > arenaMaxX - 1.5f) x = Player.position.x - side * distance; // no room: the other side
            x = Mathf.Clamp(x, arenaMinX + halfWidth + 0.5f, arenaMaxX - halfWidth - 0.5f);
            if (Mathf.Abs(x - transform.position.x) < 1.5f) yield break;
            if (Mathf.Abs(x - transform.position.x) > 9f) yield return LeapTo(x, 3f, 0.6f, true); // too far to walk: a bound
            else yield return WalkTo(x, PerPhase(walkSpeedPerPhase, phase));
        }

        IEnumerator WalkTo(float x, float speed)
        {
            state = State.Walk;
            x = Mathf.Clamp(x, arenaMinX + halfWidth + 0.3f, arenaMaxX - halfWidth - 0.3f);
            figure.facing = x < transform.position.x ? -1 : 1;
            float step = 0f;
            for (float t = 0f; t < 2f && Mathf.Abs(Body.position.x - x) > 0.05f && !Interrupted; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                float nx = Mathf.MoveTowards(Body.position.x, x, speed * Time.fixedDeltaTime);
                Body.MovePosition(new Vector2(nx, GroundedY));
                step += Time.fixedDeltaTime * speed * 0.9f;
                figure.crouch = 0.12f + 0.08f * Mathf.Abs(Mathf.Sin(step * 2f));
                figure.lean = 8f;
            }
        }

        IEnumerator LeapTo(float x, float height, float time, bool interruptible)
        {
            Vector2 start = Body.position;
            var end = new Vector2(x, GroundedY);
            if (Mathf.Abs(x - start.x) > 0.05f) figure.facing = x < start.x ? -1 : 1;
            for (float t = 0f; t < 1f;)
            {
                if (interruptible && Interrupted) { Body.position = new Vector2(Body.position.x, GroundedY); yield break; }
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / Mathf.Max(0.05f, time));
                var p = Vector2.Lerp(start, end, t);
                p.y += Mathf.Sin(t * Mathf.PI) * height;
                Body.MovePosition(p);
            }
        }

        IEnumerator Glide(Vector2 target, float time)
        {
            Vector2 start = Body.position;
            for (float t = 0f; t < 1f && !Health.IsDead;)
            {
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / Mathf.Max(0.05f, time));
                Body.MovePosition(Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
            }
        }

        void Teleport(Vector2 p)
        {
            if (Body) Body.position = p;
            transform.position = p;
        }

        void Land(float shake)
        {
            VioletHits.ShakeCamera(shake, 0.35f);
            for (int i = 0; i < 10; i++)
                VioletHits.Puff(new Vector2(transform.position.x + Random.Range(-1.2f, 1.2f), floorY + 0.1f), new Vector2(Random.Range(-5f, 5f), Random.Range(0.5f, 2.5f)),
                    0.4f, 1f, new Color(0.55f, 0.45f, 0.65f, 0.6f), 0.6f, 8);
        }

        IEnumerator Recover(int phase, float extra = 0f)
        {
            state = State.Recover;
            figure.armResponse = 8f;
            figure.bladeGlow = 0f;
            yield return WaitOrInterrupt(PerPhase(recoverTimePerPhase, phase) + extra);
        }

        void FacePlayer()
        {
            if (Player) figure.facing = Player.position.x < transform.position.x ? -1 : 1;
        }

        // ---------- Helpers ----------

        void FireOrb(Vector2 dir, Vector2 from, bool reflectable, float speed = -1f)
        {
            var orb = VioletShots.Orb(from, dir, speed > 0f ? speed : orbSpeed, reflectable, transform, true);
            Track(orb.gameObject);
        }

        T Track<T>(T component) where T : Component
        {
            if (component) Track(component.gameObject);
            return component;
        }

        void Track(GameObject go)
        {
            spawned.RemoveAll(g => !g);
            if (go) spawned.Add(go);
        }

        /// <summary>Clears the arena: every wave, beam, ring, eruption, sword, needle and orb in the air.</summary>
        void Dispel()
        {
            foreach (var go in spawned)
            {
                if (!go) continue;
                VioletHits.Burst(go.transform.position, VioletShapes.Glow, 3, 3f, 0.25f);
                Destroy(go);
            }
            spawned.Clear();
        }

        void ClearSpawned()
        {
            foreach (var go in spawned)
                if (go) Destroy(go);
            spawned.Clear();
        }

        IEnumerator WaitOrInterrupt(float seconds)
        {
            for (float t = 0f; t < seconds && !Interrupted; t += Time.deltaTime) yield return null;
        }

        /// <summary>The arm angle (0 = down, 90 = forward, 180 = up) that points the hand along a world angle, for the current facing.</summary>
        float ArmAngleFor(float worldAngle)
        {
            float local = figure.facing > 0 ? worldAngle : 180f - worldAngle;
            return Mathf.Repeat(local + 90f + 90f, 360f) - 90f;
        }

        static Vector2 FromAngle(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    }
}
