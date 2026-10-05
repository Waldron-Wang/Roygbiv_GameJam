using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// RED — hot-blooded, anger. A furious brute that flies over the arena, ARMORED: hits don't hurt it,
    /// they make it angrier. When its RAGE maxes out it OVERHEATS: it stops dead, shudders, smokes and
    /// flashes white-hot, drops out of the sky and lies collapsed on the floor, exposed, for a few seconds.
    ///
    /// Rage climbs slowly on its own (faster each phase) and jumps when the player hits the armored boss:
    /// a melee hit is worth a lot, a Light Shot only a little (and at most one per shotRageCooldown), so
    /// plinking from afar is slow and getting in close is the real way to make it blow.
    ///
    /// Each cycle: drift to a new spot overhead -> wind up -> one fire pattern -> rest.
    ///   Spread: a fan of fireballs aimed at the player. Dash through or slip between them.
    ///   Arc:    fireballs lobbed onto the player (warning strip where each lands). Phase 1+: they leave fire.
    ///   Ground: (phase 1+) slams a wave of fire pillars along the floor from under it, both ways. Jump it.
    /// Now and then (chargeCooldownPerPhase) it CHARGES instead: lands at the far end, winds up, rushes the
    /// length of the arena (jump over it or dash through), skids, then stands there panting: the opening to
    /// punch it for rage. Phase 2 turns around and rushes back before it rests.
    ///
    /// Overheated, every hit hurts, up to maxHitsPerOverheat; then it shakes it off, armored, rage spent.
    /// 12 HP, 4 hits per overheat, thresholds 0.67 / 0.34 = one phase per overheat.
    ///
    /// Animation is procedural, on the Pose child (pivot at the feet): squash, lean, tremble, a heat glow
    /// that reddens with rage, and puffs of steam / smoke / dust. Art can read CurrentState instead.
    /// Reward: Blaze Strike.
    /// </summary>
    public class RedBoss : BossBase
    {
        public enum Attack { Spread, Arc, Ground }

        public enum State { Hover, WindUp, Swoop, ChargeWindUp, Rush, Skid, Panting, OverheatTell, Falling, Overheated, ShakeOff, Defeated }

        // Each phase opens with its new pattern so the player sees it right away.
        static readonly Attack[][] Rotation =
        {
            new[] { Attack.Spread, Attack.Arc },
            new[] { Attack.Ground, Attack.Spread, Attack.Arc },
            new[] { Attack.Ground, Attack.Arc, Attack.Spread },
        };

        [Header("Rage")]
        [SerializeField] float rageMax = 100f;
        [Tooltip("Rage it builds per second on its own, per phase.")]
        [SerializeField] float[] ragePerSecondPerPhase = { 2.5f, 3f, 3.5f };
        [Tooltip("Rage from a melee hit (anything but a projectile) while it's armored.")]
        [SerializeField] float rageFromMelee = 7f;
        [Tooltip("Rage from a Light Shot while it's armored. Small, so plinking from afar is slow.")]
        [SerializeField] float rageFromShot = 1.5f;
        [Tooltip("Shots closer together than this give no rage, however fast they come.")]
        [SerializeField] float shotRageCooldown = 0.6f;

        [Header("Fire")]
        [SerializeField] Projectile firePrefab; // not reflectable: fire is dodged, not punched
        [SerializeField] Color fireColor = new(1f, 0.35f, 0.05f);
        [SerializeField] int[] spreadCountPerPhase = { 3, 5, 5 };
        [Tooltip("Total angle of the spread fan, in degrees.")]
        [SerializeField] float spreadAngle = 50f;
        [SerializeField] int[] arcCountPerPhase = { 2, 3, 4 };
        [Tooltip("Lobbed fireballs land this far apart, centered on the player.")]
        [SerializeField] float arcLandingSpacing = 2.5f;
        [SerializeField] float arcFlightTime = 1.1f;
        [SerializeField] float arcGravityScale = 1.5f;
        [SerializeField] float arcStagger = 0.2f;
        [Tooltip("Per phase: a lobbed fireball leaves fire burning where it lands for this long. 0 = none.")]
        [SerializeField] float[] arcBurnTimePerPhase = { 0f, 2f, 2.5f };
        [SerializeField] float burnPatchWidth = 1.4f;
        [SerializeField] float burnPatchHeight = 0.6f;

        [Header("Ground fire")]
        [Tooltip("Per phase: fire waves per Ground attack, waveGap apart.")]
        [SerializeField] int[] groundWavesPerPhase = { 1, 1, 2 };
        [SerializeField] float waveGap = 0.7f;
        [SerializeField] float pillarSpacing = 1.1f;
        [Tooltip("Seconds between neighbouring pillars, so wave speed = spacing / this.")]
        [SerializeField] float pillarStepTime = 0.08f;
        [SerializeField] float pillarHeight = 1.3f;
        [Tooltip("Each pillar's warning strip shows this long first, so a fuse races ahead of the flames.")]
        [SerializeField] float pillarWarnTime = 0.45f;
        [SerializeField] float pillarBurnTime = 0.35f;

        [Header("Charge")]
        [Tooltip("Earliest first charge, in seconds after the fight starts.")]
        [SerializeField] float firstChargeDelay = 9f;
        [Tooltip("Seconds from the end of one charge to the next, per phase. Rare on purpose: the panting after it is the player's best opening.")]
        [SerializeField] float[] chargeCooldownPerPhase = { 13f, 11f, 9f };
        [Tooltip("Fire patterns it always does between two charges.")]
        [SerializeField] int minCyclesBetweenCharges = 2;
        [Tooltip("Flying down to its starting end.")]
        [SerializeField] float swoopTime = 0.9f;
        [Tooltip("Crouched, pawing the ground, with the charge path lit up. Per phase.")]
        [SerializeField] float[] chargeWindUpPerPhase = { 1.1f, 0.9f, 0.75f };
        [SerializeField] float[] chargeSpeedPerPhase = { 15f, 17f, 19f };
        [Tooltip("Per phase: 2 = it turns around and rushes back before it rests.")]
        [SerializeField] int[] rushesPerPhase = { 1, 1, 2 };
        [SerializeField] float turnAroundTime = 0.45f;
        [SerializeField] float skidDistance = 1.8f;
        [Tooltip("Panting on the ground after a charge: the opening to punch it for rage.")]
        [SerializeField] float pantTime = 1.8f;
        [SerializeField] int chargeDamage = 1;
        [SerializeField] float chargeKnockback = 9f;
        [SerializeField] Color chargePathColor = new(1f, 0.15f, 0.1f, 0.6f);

        [Header("Overheat")]
        [Tooltip("Stops dead and shudders (smoking, flashing) before it drops.")]
        [SerializeField] float overheatTellTime = 0.9f;
        [SerializeField] float fallGravity = 35f;
        [Tooltip("Seconds it lies on the ground, exposed.")]
        [SerializeField] float overheatDuration = 4.5f;
        [Tooltip("It snaps out of it early after this many hits. With 12 HP and thresholds 0.67 / 0.34, 4 = one phase per overheat.")]
        [SerializeField] int maxHitsPerOverheat = 4;
        [SerializeField] float shakeOffTime = 0.5f;
        [Tooltip("Art/SFX: the moment it starts to overheat.")]
        [SerializeField] UnityEvent onOverheat = new();

        [Header("Movement")]
        [Tooltip("World X range it hovers in and charges between. Defaults fit the generated arena (walls at ±20).")]
        [SerializeField] float arenaMinX = -16f;
        [SerializeField] float arenaMaxX = 16f;
        [Tooltip("The floor between the walls: top surface and inner wall faces. Ground fire stays inside it.")]
        [SerializeField] float groundY = -2.5f;
        [SerializeField] float floorMinX = -19.5f;
        [SerializeField] float floorMaxX = 19.5f;
        [Tooltip("World Y it flies at: out of reach from the floor, just reachable with a jump from a platform.")]
        [SerializeField] float hoverY = 4.5f;
        [Tooltip("Roughly how far beside the player it settles each cycle.")]
        [SerializeField] float sideOffset = 6f;
        [SerializeField] float[] driftTimePerPhase = { 1f, 0.85f, 0.7f };

        [Header("Timing")]
        [Tooltip("Telegraph before each fire pattern, per phase. Art: hook onWindUp and read CurrentAttack.")]
        [SerializeField] float[] windUpTimePerPhase = { 0.7f, 0.6f, 0.5f };
        [SerializeField] float[] restTimePerPhase = { 1.4f, 1.1f, 0.8f };
        [SerializeField] UnityEvent onWindUp = new();

        [Header("Animation")]
        [Tooltip("Squashed, leaned and shaken by the procedural animation; its origin is the boss's feet. Defaults to the child named Pose.")]
        [SerializeField] Transform pose;
        [SerializeField] Color heatColor = new(1f, 0.3f, 0.05f);
        [SerializeField] Color whiteHot = new(1f, 0.95f, 0.75f);
        [SerializeField] Color burntColor = new(0.12f, 0.1f, 0.1f);
        [SerializeField] Color smokeColor = new(0.3f, 0.28f, 0.28f, 0.8f);
        [SerializeField] Color steamColor = new(1f, 1f, 1f, 0.55f);
        [SerializeField] Color dustColor = new(0.6f, 0.55f, 0.5f, 0.6f);
        [Tooltip("Art: the attack sprite shows through wind-ups and charges, and this long after each throw or slam.")]
        [SerializeField] float attackPoseHold = 0.35f;

        State state;
        Collider2D bodyCollider;
        Hitbox chargeHitbox;
        SpriteRenderer heat, heatSource, pathMarker;
        Vector3 poseBasePos, poseBaseScale = Vector3.one;
        float poseFeetY; // the feet in the pose's local space: 0 for a proper Pose child
        Vector2 smoothVelocity, lastPosition;
        float halfWidth = 1f, halfHeight = 1f;
        float lastShotRageAt = float.NegativeInfinity, nextChargeAt;
        int cycle, cyclesSinceCharge, overheatHits;

        // Animation state, written by the attack coroutines and read in LateUpdate.
        float stateTime, stateDuration, animTime, windUp01, punch, landSquash, jolt, flare;
        float breathPhase, lastBreath, emitA, emitB, emitC;
        int facing = 1, rushDir = 1, fallSide = 1;
        bool punchDown;

        public float Rage { get; private set; }
        public float RageFraction => Rage / rageMax; // for UI
        public bool Overheated => state == State.Overheated;
        public Attack CurrentAttack { get; private set; }

        public override BossPose Pose => state switch
        {
            State.WindUp or State.ChargeWindUp or State.Rush => BossPose.Attack,
            State.OverheatTell or State.Falling or State.Overheated or State.Defeated => BossPose.Hurt,
            _ => base.Pose,
        };

        public State CurrentState
        {
            get => state;
            private set
            {
                if (state == value) return;
                state = value;
                stateTime = 0f;
            }
        }

        bool Blowing => Rage >= rageMax;
        bool ChargeReady => Player && Time.time >= nextChargeAt && cyclesSinceCharge >= minCyclesBetweenCharges;
        float GroundedY => groundY + halfHeight;

        // Hits on the armor and the clock only build rage while it's in control of itself.
        bool GainsRage => state is State.Hover or State.WindUp or State.Swoop or State.ChargeWindUp
                                 or State.Rush or State.Skid or State.Panting;

        protected override void Awake()
        {
            base.Awake();
            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider is BoxCollider2D box)
            {
                halfWidth = box.size.x * 0.5f * transform.lossyScale.x;
                halfHeight = box.size.y * 0.5f * transform.lossyScale.y;
            }

            if (!pose) pose = transform.Find("Pose");
            if (!pose)
            {
                Debug.LogWarning($"{name}: no Pose child, so the animation squashes the Visual around its center.");
                pose = transform.Find("Visual");
                if (pose) poseFeetY = -halfHeight;
            }
            if (pose)
            {
                poseBasePos = pose.localPosition;
                poseBaseScale = pose.localScale;
            }
            lastPosition = transform.position;

            Health.DamageFilter = FilterHit;
            chargeHitbox = MakeChargeHitbox();
        }

        // After every Awake, so HitFeedback's flash doesn't pick up the glow overlay.
        void Start()
        {
            heatSource = pose ? pose.GetComponentInChildren<SpriteRenderer>() : null;
            if (!heatSource) return;
            heat = new GameObject("Heat").AddComponent<SpriteRenderer>();
            heat.transform.SetParent(heatSource.transform, false);
            heat.sprite = heatSource.sprite;
            heat.sortingOrder = heatSource.sortingOrder + 1;
            heat.color = UnityEngine.Color.clear;
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

        protected override void OnFightStarted()
        {
            nextChargeAt = Time.time + firstChargeDelay;
            lastPosition = transform.position;

            // It never shoves or carries the player: they pass through it, and only the charge hurts on touch.
            if (Player && bodyCollider)
                foreach (var c in Player.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(bodyCollider, c);
        }

        protected override void OnPhaseChanged(int newPhase) => cycle = 0;

        protected override IEnumerator RunPhase(int phase)
        {
            if (Blowing) { yield return Overheat(); yield break; }
            if (ChargeReady) { yield return Charge(phase); yield break; }

            yield return DriftTo(NextHoverPoint(), PerPhase(driftTimePerPhase, phase));

            var rotation = Rotation[Mathf.Min(phase, Rotation.Length - 1)];
            CurrentAttack = rotation[cycle++ % rotation.Length];
            cyclesSinceCharge++;
            yield return WindUp(PerPhase(windUpTimePerPhase, phase));
            if (Blowing) yield break; // it boils over before it throws

            switch (CurrentAttack)
            {
                case Attack.Spread: FireSpread(PerPhase(spreadCountPerPhase, phase)); break;
                case Attack.Arc: yield return FireArcs(phase); break;
                case Attack.Ground: StartCoroutine(GroundFire(PerPhase(groundWavesPerPhase, phase))); break;
            }
            yield return WaitOrBlow(PerPhase(restTimePerPhase, phase));
        }

        protected override void OnDefeated()
        {
            CurrentState = State.Defeated;
            windUp01 = 0f;
            if (chargeHitbox) chargeHitbox.gameObject.SetActive(false);
            if (pathMarker) Destroy(pathMarker.gameObject);
        }

        void Update()
        {
            if (IsFighting && GainsRage) AddRage(PerPhase(ragePerSecondPerPhase, Phase) * Time.deltaTime);
        }

        void FixedUpdate()
        {
            var position = (Vector2)transform.position;
            smoothVelocity = Vector2.Lerp(smoothVelocity, (position - lastPosition) / Time.fixedDeltaTime, 0.25f);
            lastPosition = position;
        }

        // ---------- Rage & armor ----------

        /// <summary>Health's veto: only an overheated boss takes damage. Anything else feeds the rage.</summary>
        bool FilterHit(DamageInfo info)
        {
            if (state == State.Overheated) return true;

            bool shot = info.source && info.source.GetComponent<Projectile>();
            bool counts = !shot || Time.time - lastShotRageAt >= shotRageCooldown;
            if (shot && counts) lastShotRageAt = Time.time;
            if (counts && IsFighting && GainsRage) AddRage(shot ? rageFromShot : rageFromMelee);

            Clang(info, !shot);
            return false;
        }

        void AddRage(float amount) => Rage = Mathf.Min(rageMax, Rage + amount);

        void OnDamaged(DamageInfo _)
        {
            if (state == State.Overheated) overheatHits++;
        }

        /// <summary>A hit bounced off the armor: sparks, a jolt and a flare of heat, so it reads as "that made it angrier".</summary>
        void Clang(DamageInfo info, bool heavy)
        {
            Vector2 at = info.source ? bodyCollider.ClosestPoint(info.source.transform.position) : (Vector2)transform.position;
            for (int i = 0, n = heavy ? 6 : 3; i < n; i++)
                Puff(at, Random.insideUnitCircle.normalized * Random.Range(3f, 6f), 0.18f, 0.04f, whiteHot, 0.25f, 12);
            jolt = heavy ? 1f : 0.5f;
            flare = Mathf.Min(1f, flare + (heavy ? 0.6f : 0.25f));
        }

        // ---------- Movement ----------

        Vector2 NextHoverPoint()
        {
            float px = Player ? Player.position.x : 0f;
            // Usually crosses to the player's other side, but it's too hot-headed to be a metronome.
            float side = transform.position.x > px ? -1f : 1f;
            if (Random.value < 0.35f) side = -side;
            float offset = sideOffset + Random.Range(-1.5f, 1.5f);
            float x = px + side * offset;
            if (x < arenaMinX || x > arenaMaxX) x = px - side * offset; // no room there
            return new Vector2(Mathf.Clamp(x, arenaMinX, arenaMaxX), hoverY + Random.Range(-0.4f, 0.4f));
        }

        /// <summary>Darts over with an overshoot, then settles.</summary>
        IEnumerator DriftTo(Vector2 target, float time)
        {
            CurrentState = State.Hover;
            Vector2 start = transform.position;
            for (float t = 0f; t < 1f;)
            {
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / time);
                MoveTo(Vector2.LerpUnclamped(start, target, EaseOutBack(t)));
            }
        }

        void MoveTo(Vector2 position)
        {
            if (Body) Body.MovePosition(position);
            else transform.position = position;
        }

        IEnumerator WaitOrBlow(float seconds)
        {
            for (float t = 0f; t < seconds && !Blowing; t += Time.deltaTime) yield return null;
        }

        // ---------- Fire patterns ----------

        IEnumerator WindUp(float time)
        {
            CurrentState = State.WindUp;
            facing = FacingSign;
            onWindUp.Invoke();
            for (float t = 0f; t < time && !Blowing; t += Time.deltaTime)
            {
                windUp01 = t / time;
                yield return null;
            }
            windUp01 = 0f;
            CurrentState = State.Hover;
        }

        void FireSpread(int count)
        {
            if (!firePrefab || !Player) return;
            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? Mathf.Lerp(-0.5f, 0.5f, i / (count - 1f)) * spreadAngle : 0f;
                Fire(firePrefab, Quaternion.Euler(0f, 0f, angle) * AimDirection);
            }
            Kick(false);
        }

        IEnumerator FireArcs(int phase)
        {
            int count = PerPhase(arcCountPerPhase, phase);
            float burnTime = PerPhase(arcBurnTimePerPhase, phase);
            for (int i = 0; i < count; i++)
            {
                if (!firePrefab || !Player) yield break;
                float offset = (i - (count - 1) * 0.5f) * arcLandingSpacing;
                float x = Mathf.Clamp(Player.position.x + offset, floorMinX + 0.5f, floorMaxX - 0.5f);
                Lob(new Vector2(x, groundY), burnTime);
                Kick(false);
                yield return Wait(arcStagger);
            }
        }

        /// <summary>Lobs a fireball that lands on `target` after arcFlightTime.</summary>
        void Lob(Vector2 target, float burnTime)
        {
            var delta = target - (Vector2)transform.position;
            float g = Physics2D.gravity.y * arcGravityScale;
            var velocity = new Vector2(delta.x / arcFlightTime, delta.y / arcFlightTime - 0.5f * g * arcFlightTime);
            Fire(firePrefab, velocity).Arc(velocity, arcGravityScale);
            // The warning strip is the landing marker; with a burn time it catches fire as the ball lands.
            FirePatch.Spawn(target.x, groundY, burnTime > 0f ? burnPatchWidth : 1f, burnPatchHeight, arcFlightTime, burnTime, fireColor);
        }

        IEnumerator GroundFire(int waves)
        {
            for (int w = 0; w < waves; w++)
            {
                if (w > 0) yield return Wait(waveGap);
                Kick(true);
                ShakeCamera(0.12f, 0.2f);
                StartCoroutine(FireWave(transform.position.x));
            }
        }

        /// <summary>Pillars erupt one after another outward from under it, both ways, to the walls.</summary>
        IEnumerator FireWave(float originX)
        {
            for (float d = 0f; originX - d >= floorMinX || originX + d <= floorMaxX; d += pillarSpacing)
            {
                SpawnPillar(originX + d);
                if (d > 0f) SpawnPillar(originX - d);
                yield return Wait(pillarStepTime);
            }
        }

        void SpawnPillar(float x)
        {
            float half = pillarSpacing * 0.45f;
            if (x - half < floorMinX || x + half > floorMaxX) return;
            FirePatch.Spawn(x, groundY, pillarSpacing * 0.9f, pillarHeight, pillarWarnTime, pillarBurnTime, fireColor);
        }

        // ---------- Charge ----------

        IEnumerator Charge(int phase)
        {
            // Start at the end farther from the player, so the rush has to pass them.
            int dir = Player.position.x > (arenaMinX + arenaMaxX) * 0.5f ? 1 : -1;
            yield return Swoop(new Vector2(dir > 0 ? arenaMinX : arenaMaxX, GroundedY));

            for (int r = 0, rushes = PerPhase(rushesPerPhase, phase); r < rushes; r++, dir = -dir)
            {
                rushDir = dir;
                float endX = dir > 0 ? arenaMaxX : arenaMinX;
                yield return ChargeWindUp(r == 0 ? PerPhase(chargeWindUpPerPhase, phase) : turnAroundTime, endX);
                yield return Rush(endX, PerPhase(chargeSpeedPerPhase, phase));
            }

            // Spent: stands there heaving for breath. Punching it now is the fastest way to make it blow.
            breathPhase = Mathf.PI; // open on a big, steaming out-breath
            lastBreath = 1f;
            CurrentState = State.Panting;
            yield return WaitOrBlow(pantTime);

            cyclesSinceCharge = 0;
            nextChargeAt = Time.time + PerPhase(chargeCooldownPerPhase, phase);
            if (Blowing) yield break; // overheats right here, on the ground, next cycle
            yield return DriftTo(NextHoverPoint(), PerPhase(driftTimePerPhase, phase));
        }

        /// <summary>Dives down to the floor and lands with a thud.</summary>
        IEnumerator Swoop(Vector2 target)
        {
            CurrentState = State.Swoop;
            Vector2 start = transform.position;
            for (float t = 0f; t < 1f;)
            {
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / swoopTime);
                // Across first, then down hard at the end.
                float x = Mathf.Lerp(start.x, target.x, Mathf.SmoothStep(0f, 1f, t));
                float y = Mathf.Lerp(start.y, target.y, t * t) + Mathf.Sin(t * Mathf.PI) * 1f;
                MoveTo(new Vector2(x, y));
            }
            Land(0.6f, 0.12f);
        }

        IEnumerator ChargeWindUp(float time, float endX)
        {
            CurrentState = State.ChargeWindUp;
            stateDuration = time;
            float startX = transform.position.x;
            pathMarker = FlatSprite.Create("ChargePath", null, new Vector2((startX + endX) * 0.5f, groundY + 0.05f),
                new Vector2(Mathf.Abs(endX - startX) + halfWidth * 2f, 0.1f), chargePathColor, 3);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                var c = chargePathColor;
                c.a *= 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 30f));
                pathMarker.color = c;
                yield return null;
            }
            Destroy(pathMarker.gameObject);
        }

        IEnumerator Rush(float endX, float speed)
        {
            CurrentState = State.Rush;
            chargeHitbox.Open(float.PositiveInfinity); // closed by hand once it stops
            float x = transform.position.x, v = 0f;
            float skidFrom = endX - rushDir * skidDistance;
            while (rushDir * (skidFrom - x) > 0f)
            {
                yield return new WaitForFixedUpdate();
                v = Mathf.MoveTowards(v, speed, speed / 0.12f * Time.fixedDeltaTime);
                x += rushDir * v * Time.fixedDeltaTime;
                MoveTo(new Vector2(x, GroundedY));
            }

            // Digs in and slides to a stop, still dangerous.
            CurrentState = State.Skid;
            ShakeCamera(0.1f, 0.25f);
            float from = x, skidTime = 2f * skidDistance / Mathf.Max(v, 0.01f);
            for (float t = 0f; t < 1f;)
            {
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / skidTime);
                MoveTo(new Vector2(from + rushDir * skidDistance * (1f - (1f - t) * (1f - t)), GroundedY));
            }
            chargeHitbox.gameObject.SetActive(false);
        }

        Hitbox MakeChargeHitbox()
        {
            var go = new GameObject("ChargeHitbox");
            go.SetActive(false); // Hitbox.Open() turns it on
            go.transform.SetParent(transform, false);
            // A little under the body's size and down at the floor, so a jump that clears it looks like it did.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(halfWidth * 1.8f, halfHeight * 1.6f);
            box.offset = new Vector2(0f, -halfHeight * 0.2f);
            var hitbox = go.AddComponent<Hitbox>();
            hitbox.team = Team.Enemy;
            hitbox.damage = chargeDamage;
            hitbox.knockback = chargeKnockback;
            return hitbox;
        }

        // ---------- Overheat ----------

        IEnumerator Overheat()
        {
            onOverheat.Invoke();
            fallSide = -FacingSign; // keels over away from the player

            // 1. Stops dead: shudders, smokes, flashes white-hot.
            CurrentState = State.OverheatTell;
            yield return Wait(overheatTellTime);

            // 2. Drops out of the sky (unless it blew while already on the floor, after a charge).
            if (transform.position.y > GroundedY + 0.05f)
            {
                CurrentState = State.Falling;
                float x = transform.position.x, y = transform.position.y, vy = 0f;
                while (y > GroundedY)
                {
                    yield return new WaitForFixedUpdate();
                    vy -= fallGravity * Time.fixedDeltaTime;
                    y = Mathf.Max(GroundedY, y + vy * Time.fixedDeltaTime);
                    MoveTo(new Vector2(x, y));
                }
                Land(1f, 0.3f);
                for (int i = 0; i < 8; i++)
                    Puff(transform.position, new Vector2(Random.Range(-3f, 3f), Random.Range(0.5f, 2.5f)), 0.4f, 1.4f, smokeColor, 1.2f);
            }

            // 3. Collapsed and exposed: every hit lands, up to maxHitsPerOverheat.
            overheatHits = 0;
            CurrentState = State.Overheated;
            for (float t = 0f; t < overheatDuration && overheatHits < maxHitsPerOverheat; t += Time.deltaTime)
                yield return null;

            // 4. Shakes it off, rage spent, armored again, and flies back up.
            CurrentState = State.ShakeOff;
            Rage = 0f;
            yield return Wait(shakeOffTime);
            cyclesSinceCharge = 0;
            nextChargeAt = Mathf.Max(nextChargeAt, Time.time + 4f);
            yield return DriftTo(NextHoverPoint(), PerPhase(driftTimePerPhase, Phase));
        }

        // ---------- Animation ----------

        void Kick(bool down)
        {
            punch = 1f;
            punchDown = down;
            HoldAttackPose(attackPoseHold);
            flare = Mathf.Max(flare, 0.5f);
        }

        void Land(float squash, float cameraShake)
        {
            landSquash = squash;
            ShakeCamera(cameraShake, 0.3f);
            for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 3; i++)
                Puff(PosePoint(s * 0.8f, 0f), new Vector2(s * Random.Range(2f, 4f), Random.Range(0.3f, 1f)), 0.3f, 0.8f, dustColor, 0.5f, 9);
        }

        void LateUpdate()
        {
            if (!pose) return;
            float dt = Time.deltaTime;
            animTime += dt;
            stateTime += dt;
            punch = Mathf.MoveTowards(punch, 0f, dt / 0.2f);
            landSquash = Mathf.MoveTowards(landSquash, 0f, dt / 0.35f);
            jolt = Mathf.MoveTowards(jolt, 0f, dt / 0.15f);
            flare = Mathf.MoveTowards(flare, 0f, dt * 1.5f);

            float rage = RageFraction;
            var squash = Vector2.one;  // pivot at the feet
            var offset = Vector2.zero;
            float lean = 0f;           // degrees; + tips the top toward +x
            float tremble = 0f;        // degrees of jittery wobble
            float heatAlpha = Mathf.Pow(rage, 1.5f) * 0.7f + flare * 0.4f; // reddens as it gets angrier
            var heatTint = heatColor;

            switch (state)
            {
                case State.Hover:
                case State.WindUp:
                case State.Swoop:
                {
                    // Seething: a hard throb that quickens with rage, a bob, and leaning into its movement.
                    float throb = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(animTime * Mathf.Lerp(6f, 13f, rage)), 4f);
                    squash = new Vector2(1f + 0.05f * throb, 1f + 0.08f * throb);
                    offset.y = Mathf.Sin(animTime * 3.1f) * 0.15f;
                    float vx = smoothVelocity.x;
                    lean = Mathf.Clamp(vx * 1.2f, -20f, 20f);
                    float stretch = Mathf.Min(Mathf.Abs(vx) * 0.012f, 0.2f);
                    squash.x *= 1f + stretch;
                    squash.y *= 1f - stretch;
                    tremble = 1f + 6f * rage * rage;
                    if (state == State.WindUp)
                    {
                        // Rears back and swells before it throws.
                        squash *= 1f + 0.22f * windUp01;
                        lean -= facing * 12f * windUp01;
                        tremble += 5f * windUp01;
                        heatAlpha += 0.35f * windUp01;
                    }
                    break;
                }
                case State.ChargeWindUp:
                {
                    // Crouches, leans back, paws the ground, steam blasting off the top.
                    float w = Mathf.Clamp01(stateTime / stateDuration);
                    squash = new Vector2(1f + 0.22f * w, 1f - 0.28f * w);
                    lean = -rushDir * 14f * w;
                    offset.x = -rushDir * 0.15f * Mathf.Abs(Mathf.Sin(stateTime * 14f));
                    tremble = 3f + 9f * w;
                    heatAlpha = Mathf.Max(heatAlpha, 0.3f) + 0.3f * w;
                    if (Every(ref emitA, 0.08f))
                        Puff(PosePoint(Random.Range(-0.5f, 0.5f), 1f), new Vector2(Random.Range(-0.5f, 0.5f), 3f), 0.25f, 0.8f, steamColor, 0.5f);
                    if (Every(ref emitB, 0.2f))
                        Puff(PosePoint(-rushDir * 0.8f, 0f), new Vector2(-rushDir * 2.5f, 0.8f), 0.25f, 0.6f, dustColor, 0.4f, 9);
                    break;
                }
                case State.Rush:
                    squash = new Vector2(1.3f, 0.8f);
                    lean = rushDir * 18f;
                    offset.y = Mathf.Abs(Mathf.Sin(stateTime * 28f)) * 0.08f; // galloping
                    tremble = 3f;
                    heatAlpha = Mathf.Max(heatAlpha, 0.5f);
                    if (Every(ref emitA, 0.03f))
                        Puff(PosePoint(-rushDir, 0.5f), new Vector2(-rushDir * 2f, 1f), 0.6f, 0.1f, fireColor, 0.35f, 7);
                    if (Every(ref emitB, 0.06f))
                        Puff(PosePoint(-rushDir * 0.8f, 0f), new Vector2(-rushDir * 1.5f, 1f), 0.3f, 0.7f, dustColor, 0.4f, 9);
                    break;
                case State.Skid:
                    squash = new Vector2(1.15f, 0.85f);
                    lean = -rushDir * 16f; // digging its heels in
                    tremble = 4f;
                    if (Every(ref emitA, 0.03f))
                        Puff(PosePoint(rushDir * 0.8f, 0f), new Vector2(rushDir * 3f, Random.Range(0.8f, 2f)), 0.3f, 0.8f, dustColor, 0.45f, 9);
                    break;
                case State.Panting:
                {
                    // Big breaths that slow as it recovers: the body heaves up and narrows on the in-breath,
                    // slumps, widens and steams on the out-breath. Hunched toward where it ran.
                    float r = Mathf.Clamp01(stateTime / pantTime);
                    breathPhase += dt * Mathf.Lerp(2.6f, 1.4f, r) * Mathf.PI * 2f;
                    float b = Mathf.Sin(breathPhase);
                    float amp = Mathf.Lerp(1f, 0.6f, r);
                    squash = new Vector2(1.06f - 0.07f * b * amp, 0.92f + 0.14f * b * amp);
                    lean = rushDir * (12f - 5f * r) - rushDir * 5f * b * amp; // head comes up as it breathes in
                    offset.y = 0.05f * b * amp;
                    tremble = 1f;
                    heatAlpha *= 0.85f + 0.15f * b;
                    if (lastBreath > 0f && b <= 0f) // start of an out-breath
                        Puff(PosePoint(rushDir * 0.9f, 0.7f), new Vector2(rushDir * 2.5f, 0.8f), 0.35f, 0.9f, steamColor, 0.6f);
                    lastBreath = b;
                    break;
                }
                case State.OverheatTell:
                {
                    // Swells and shudders, strobing white-hot, smoke pouring off it.
                    float w = Mathf.Clamp01(stateTime / overheatTellTime);
                    squash = Vector2.one * (1f + 0.15f * w) + Random.insideUnitCircle * (0.05f * w);
                    offset = Random.insideUnitCircle * (0.08f * w);
                    tremble = 6f + 12f * w;
                    heatTint = Mathf.Repeat(stateTime * 14f, 1f) < 0.5f ? whiteHot : heatColor;
                    heatAlpha = 0.9f;
                    if (Every(ref emitA, 0.05f))
                        Puff(PosePoint(Random.Range(-0.8f, 0.8f), 1f), new Vector2(Random.Range(-0.8f, 0.8f), 2f), 0.3f, 1.2f, smokeColor, 1f);
                    break;
                }
                case State.Falling:
                    squash = new Vector2(0.88f, 1.15f);
                    lean = fallSide * 25f * Mathf.Clamp01(stateTime / 0.5f);
                    heatTint = UnityEngine.Color.Lerp(heatColor, burntColor, stateTime * 2f);
                    heatAlpha = 0.8f;
                    if (Every(ref emitA, 0.04f))
                        Puff(PosePoint(0f, 1f), new Vector2(Random.Range(-0.3f, 0.3f), 1f), 0.3f, 1f, smokeColor, 0.8f);
                    break;
                case State.Overheated:
                {
                    // Collapsed and burnt out: flattened, tipped over, limp, smoking, and blinking white-hot so
                    // it reads as "hit me now". Nothing like the upright, rhythmic panting. Stirs in its last second.
                    float stir = Mathf.Clamp01(1f - (overheatDuration - stateTime));
                    squash = new Vector2(1.25f, 0.68f);
                    lean = fallSide * (22f - 6f * stir) + Mathf.Sin(stateTime * 4.4f) * 3f;
                    tremble = 8f * stir;
                    bool blink = Mathf.Repeat(stateTime, 0.55f) < 0.1f;
                    heatTint = blink ? whiteHot : UnityEngine.Color.Lerp(burntColor, heatColor, stir * 0.6f);
                    heatAlpha = blink ? 0.85f : 0.55f;
                    if (Every(ref emitA, 0.12f))
                        Puff(PosePoint(Random.Range(-0.6f, 0.6f), 1f), new Vector2(Random.Range(-0.2f, 0.2f), 1.2f), 0.25f, 1.1f, smokeColor, 1.4f);
                    break;
                }
                case State.ShakeOff:
                {
                    // Springs back into shape, shaking the soot off.
                    float w = Mathf.Clamp01(stateTime / shakeOffTime);
                    squash = Vector2.LerpUnclamped(new Vector2(1.25f, 0.68f), Vector2.one, EaseOutBack(w));
                    lean = fallSide * 22f * (1f - w);
                    tremble = 10f * (1f - w);
                    if (Every(ref emitA, 0.06f))
                        Puff(PosePoint(Random.Range(-0.8f, 0.8f), 0.8f), Random.insideUnitCircle * 2f, 0.25f, 0.7f, smokeColor, 0.6f);
                    break;
                }
                case State.Defeated:
                    squash = new Vector2(1.25f, 0.68f);
                    lean = fallSide * 22f;
                    heatTint = burntColor;
                    heatAlpha = 0.5f;
                    if (Every(ref emitA, 0.25f))
                        Puff(PosePoint(0f, 1f), new Vector2(0f, 1f), 0.25f, 1f, smokeColor, 1.4f);
                    break;
            }

            // Impulses on top of whatever stance it's in.
            if (punchDown)
            {
                squash.x *= 1f - 0.15f * punch;
                squash.y *= 1f + 0.25f * punch;
                offset.y -= 0.4f * punch;
            }
            else
            {
                squash.x *= 1f + 0.22f * punch;
                squash.y *= 1f - 0.16f * punch;
                lean += facing * 10f * punch;
            }
            squash.x *= 1f + 0.35f * landSquash;
            squash.y *= 1f - 0.35f * landSquash;
            tremble += 10f * jolt;

            // Close to boiling over: steam vents off the top, faster and faster.
            if (IsFighting && GainsRage && rage > 0.7f && Every(ref emitC, Mathf.Lerp(0.4f, 0.12f, (rage - 0.7f) / 0.3f)))
                Puff(PosePoint(Random.Range(-0.6f, 0.6f), 1f), new Vector2(Random.Range(-0.4f, 0.4f), 2.5f), 0.2f, 0.7f, steamColor, 0.5f);

            float wobble = (Mathf.PerlinNoise(animTime * 18f, 0.37f) - 0.5f) * 2f * tremble;
            pose.localPosition = poseBasePos + (Vector3)offset;
            pose.localRotation = Quaternion.Euler(0f, 0f, -(lean + wobble));
            pose.localScale = Vector3.Scale(poseBaseScale, new Vector3(squash.x, squash.y, 1f));
            if (heat)
            {
                heat.sprite = heatSource.sprite; // follows BossSprites' pose swaps
                heat.color = new Color(heatTint.r, heatTint.g, heatTint.b, Mathf.Clamp01(heatAlpha));
            }
        }

        /// <summary>A point on the body in world space: x from -1 (left edge) to 1, y from 0 (feet) to 1 (top).</summary>
        Vector2 PosePoint(float x, float y) =>
            pose.TransformPoint(new Vector3(x * halfWidth / poseBaseScale.x, (poseFeetY + y * halfHeight * 2f) / poseBaseScale.y, 0f));

        bool Every(ref float clock, float interval)
        {
            clock += Time.deltaTime;
            if (clock < interval) return false;
            clock = 0f;
            return true;
        }

        static void Puff(Vector2 at, Vector2 velocity, float size, float endSize, Color color, float life, int order = 10) =>
            HeatPuff.Spawn(at + Random.insideUnitCircle * 0.15f, velocity, size, endSize, color, life, order);

        static void ShakeCamera(float amount, float time)
        {
            if (amount > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(amount, time);
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.4f;
            t -= 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }
    }
}
