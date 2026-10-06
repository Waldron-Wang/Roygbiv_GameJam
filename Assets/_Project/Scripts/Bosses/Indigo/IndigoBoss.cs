using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// INDIGO — calm, perception, spirituality. A floating seer with a third eye that bends how the player
    /// sees and moves. Every phase opens with a CURSE that scrambles the controls (IInputModifiers on Game.Input)
    /// and warps the screen to match (ScreenWarp on the camera), so the look always tells you what's wrong:
    ///   0 MIRROR     left/right swapped        mirrored ghost of the world, split colors, rocking camera
    ///   1 SWAP       jump/attack, shoot/dash   every hue turned to its opposite, glitching slices
    ///   2 ECHO       the body acts 0.2s late   heavy motion trails tunneling inward, washed-out wobble
    ///   3 INVERSION  world upside down + mirror + swap (left/right feel right again; up doesn't)
    /// Curses are data (one per phase in the Inspector): any mix of controls, any WarpLook.
    ///
    /// Between phases it CASTS: the old curse lifts at once (clean screen, normal controls, the boss can't be hurt
    /// and every orb is dispelled), it rises high over the player and draws a sigil with the next curse's name and
    /// what it does written under it. Then the curse lands with a flash and a shockwave. The fight opens with a cast too.
    ///
    /// Attacks (Rotation per phase), all built so the scrambled controls are the hard part, not the reading:
    ///   Gaze      its eye tracks the player with a thin line, locks, then fires a beam (move off the line).
    ///   Mandala   rings of slow orbs with gaps; later phases spiral. Orbs can be punched back at it.
    ///   Blink     vanishes, a mark hunts the player from above, it drops there and sends ripples along the floor
    ///             (jump them), then meditates on the ground with its eye shut: the opening for melee.
    ///   Illusions splits into copies and shuffles them with its eye shut. Follow the real one, or read the tells:
    ///             only the real one casts light on the floor below it, and only it watches you.
    ///             Hitting a copy bursts it into orbs; hitting the real one dispels all.
    ///   Starfall  marks the floor around the player, then pillars of light come down.
    /// It can be hurt by anything, any time it isn't casting. 12 HP, thresholds 0.75 / 0.5 / 0.25 = 3 hits a phase.
    /// Everything is undone on defeat or when the scene unloads.
    ///
    /// Animation is procedural (a diamond body, a halo and an eye made at runtime); art can read CurrentState.
    /// Reward: TBD.
    /// </summary>
    public class IndigoBoss : BossBase
    {
        public enum Attack { Gaze, Mandala, Blink, Illusions, Starfall }

        public enum State { Hover, Casting, Gaze, Mandala, Blink, Meditate, Illusions, Starfall, Defeated }

        /// <summary>What one phase does to the player: the controls it scrambles and how the screen looks while it does.</summary>
        [Serializable]
        public class Curse
        {
            [Tooltip("Written big under the sigil while it's cast.")]
            public string title = "CURSE";
            [Tooltip("Written under the title. Empty = spelled out from the controls below.")]
            public string hint = "";
            [Tooltip("The sigil, halo, orbs and beams of this phase.")]
            public Color accent = new(0.55f, 0.45f, 1f);

            [Header("Controls")]
            public bool mirrorMove;
            public bool swapJumpAndAttack;
            public bool swapShootAndDash;
            [Tooltip("Seconds the player's body acts after they press. 0 = off.")]
            [Range(0f, 0.4f)] public float inputDelay;

            [Header("Screen")]
            public WarpLook look;
        }

        // Each phase opens with its new attack so the player sees it right away.
        static readonly Attack[][] Rotation =
        {
            new[] { Attack.Gaze, Attack.Mandala, Attack.Blink },
            new[] { Attack.Illusions, Attack.Gaze, Attack.Blink, Attack.Mandala },
            new[] { Attack.Starfall, Attack.Mandala, Attack.Illusions, Attack.Gaze, Attack.Blink },
            new[] { Attack.Starfall, Attack.Blink, Attack.Illusions, Attack.Gaze, Attack.Mandala },
        };

        static readonly List<RaycastHit2D> RayHits = new();

        [Header("Curses: one per phase, cast as the phase starts")]
        [SerializeField] Curse[] curses = DefaultCurses();

        [Header("Casting (between phases)")]
        [Tooltip("Height above the floor it rises to while casting.")]
        [SerializeField] float castHeight = 7.5f;
        [SerializeField] float riseTime = 0.9f;
        [Tooltip("Seconds the sigil charges. The screen is clean and the controls are normal the whole time.")]
        [SerializeField] float castTime = 2.6f;
        [Tooltip("Seconds the old curse takes to clear off the screen when a phase ends.")]
        [SerializeField] float liftTime = 0.35f;
        [Tooltip("Seconds the new curse takes to wash over the screen.")]
        [SerializeField] float landTime = 0.6f;
        [Tooltip("Seconds the screen takes to clear after it's defeated.")]
        [SerializeField] float defeatClearTime = 1.5f;
        [Tooltip("How far under the sigil the curse's name is written.")]
        [SerializeField] float labelOffset = 3.6f;
        [Tooltip("Art/SFX: it starts casting.")]
        [SerializeField] UnityEvent onCast = new();
        [Tooltip("Art/SFX: the curse lands.")]
        [SerializeField] UnityEvent onCurse = new();

        [Header("Movement")]
        [Tooltip("Top of the floor. Defaults fit the generated arena (floor at -2.5, walls at ±20).")]
        [SerializeField] float groundY = -2.5f;
        [Tooltip("Inner faces of the walls: ripples and pillars stop here.")]
        [SerializeField] float floorMinX = -19.5f;
        [SerializeField] float floorMaxX = 19.5f;
        [Tooltip("World X range it floats in.")]
        [SerializeField] float arenaMinX = -16f;
        [SerializeField] float arenaMaxX = 16f;
        [Tooltip("Height above the floor it floats at: a jumping swing or a shot reaches it.")]
        [SerializeField] float hoverHeight = 3.6f;
        [Tooltip("How far beside the player it settles before an attack.")]
        [SerializeField] float sideOffset = 6f;
        [SerializeField] float[] glideTimePerPhase = { 1f, 0.9f, 0.85f, 0.7f };

        [Header("Orbs")]
        [FormerlySerializedAs("projectilePrefab")]
        [Tooltip("Reflectable orbs: the player can punch them back at it.")]
        [SerializeField] Projectile orbPrefab;

        [Header("Gaze (eye beam)")]
        [SerializeField] int[] gazeBeamsPerPhase = { 1, 1, 2, 2 };
        [Tooltip("Seconds the thin line follows the player before it locks.")]
        [SerializeField] float[] gazeTrackTimePerPhase = { 1.1f, 1f, 1f, 0.85f };
        [Tooltip("How fast the line can follow, degrees per second. Outrun it to throw it off.")]
        [SerializeField] float[] gazeTurnSpeedPerPhase = { 90f, 110f, 120f, 150f };
        [Tooltip("Seconds between the lock and the beam: the moment to step off the line.")]
        [SerializeField] float gazeLockTime = 0.35f;
        [SerializeField] float gazeBeamTime = 0.45f;
        [SerializeField] float gazeWidth = 0.9f;
        [SerializeField] float gazeRange = 45f;
        [SerializeField] int gazeDamage = 1;

        [Header("Mandala (orb rings)")]
        [SerializeField] int[] mandalaOrbsPerPhase = { 10, 12, 12, 14 };
        [SerializeField] int[] mandalaRingsPerPhase = { 1, 2, 1, 2 };
        [Tooltip("Seconds of spiraling orbs after the rings. 0 = no spiral.")]
        [SerializeField] float[] spiralTimePerPhase = { 0f, 0f, 1.4f, 1.2f };
        [SerializeField] float mandalaWindUp = 0.6f;
        [SerializeField] float mandalaRingGap = 0.5f;
        [SerializeField] int spiralArms = 3;
        [SerializeField] float spiralInterval = 0.11f;
        [Tooltip("Degrees the spiral turns between shots.")]
        [SerializeField] float spiralTurn = 19f;

        [Header("Blink (vanish, drop, meditate)")]
        [SerializeField] float blinkFadeTime = 0.25f;
        [Tooltip("Seconds the mark hunts the player before it drops.")]
        [SerializeField] float[] blinkMarkTimePerPhase = { 0.9f, 0.8f, 0.75f, 0.6f };
        [Tooltip("The mark stops following this long before the drop.")]
        [SerializeField] float blinkLockTime = 0.25f;
        [Tooltip("Height above the floor it reappears at.")]
        [SerializeField] float blinkHeight = 6f;
        [SerializeField] float blinkDropTime = 0.16f;
        [SerializeField] int crushDamage = 1;
        [SerializeField] float rippleSpeed = 12f;
        [SerializeField] float rippleHeight = 0.7f;
        [SerializeField] int rippleDamage = 1;
        [Tooltip("Seconds it sits on the floor with its eye shut after a drop: the opening for melee.")]
        [SerializeField] float[] meditateTimePerPhase = { 1.8f, 1.5f, 1.3f, 1.1f };

        [Header("Illusions (decoys)")]
        [SerializeField] int[] decoysPerPhase = { 2, 2, 2, 3 };
        [SerializeField] int[] shufflesPerPhase = { 2, 2, 3, 4 };
        [SerializeField] float[] swapTimePerPhase = { 0.5f, 0.5f, 0.42f, 0.36f };
        [SerializeField] float illusionSpacing = 4.5f;
        [SerializeField] float splitTime = 0.6f;
        [SerializeField] int illusionVolleys = 2;
        [SerializeField] float illusionVolleyGap = 0.9f;
        [Tooltip("Seconds the copies hang around after their volleys, waiting to be told apart.")]
        [SerializeField] float illusionLinger = 2f;
        [Tooltip("Orbs a copy bursts into when it's hit.")]
        [SerializeField] int decoyBurstOrbs = 6;
        [Tooltip("Seconds between a copy breaking and its orbs flying.")]
        [SerializeField] float decoyBurstFuse = 0.3f;

        [Header("Starfall (pillars of light)")]
        [SerializeField] int[] starsPerPhase = { 3, 3, 4, 5 };
        [SerializeField] float starSpacing = 3.2f;
        [SerializeField] float starWidth = 1.3f;
        [SerializeField] float starHeight = 18f;
        [SerializeField] float starWarnTime = 0.9f;
        [SerializeField] float starBurnTime = 0.35f;
        [SerializeField] float starStagger = 0.15f;

        [Header("Timing")]
        [SerializeField] float[] restTimePerPhase = { 1.3f, 1.1f, 1f, 0.8f };
        [SerializeField] float attackPoseHold = 0.4f;

        [Header("Look")]
        [Tooltip("Turns the placeholder square on its corner. Turn off once there's art.")]
        [SerializeField] bool diamondPlaceholder = true;
        [SerializeField] float haloSize = 3.4f;
        [SerializeField] float eyeSize = 0.9f;
        [Tooltip("The pool of light it casts on the surface below. Copies cast none: the steady tell under any distortion.")]
        [SerializeField] Vector2 lightPoolSize = new(2.6f, 0.45f);
        [SerializeField] Color eyeWhite = new(0.95f, 0.93f, 1f);
        [SerializeField] Color pupilColor = new(0.08f, 0.03f, 0.2f);

        readonly List<IInputModifier> modifiers = new();
        readonly List<Projectile> orbs = new();
        readonly List<GameObject> transients = new(); // beams, marks, ripples, pillars: cleaned up if it's interrupted
        readonly List<Figure> decoys = new();

        Figure self;
        IndigoSigil sigil;
        Collider2D bodyCollider;
        Vector2 bodySize = new(2f, 2f);
        bool castPending, eyesShut;
        int cycle;

        // Animation state, written by the coroutines and read in LateUpdate.
        State state;
        Color accent = new(0.55f, 0.45f, 1f);
        Vector2 gazeDir = Vector2.left, lastPosition;
        float animTime, eyeOpen, glow, castCharge, jolt, squash, hurtFlash, tilt, spin, sink;
        SpriteRenderer lightPool;

        public State CurrentState => state;
        /// <summary>The curse on the player right now, or null while it's casting (or before the fight).</summary>
        public Curse ActiveCurse { get; private set; }

        float HoverY => groundY + hoverHeight;
        bool Interrupted => castPending || Health.IsDead;

        // ---------- Lifecycle ----------

        protected override void Awake()
        {
            base.Awake();
            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider is BoxCollider2D box) bodySize = Vector2.Scale(box.size, transform.lossyScale);

            var visual = transform.Find("Visual");
            if (visual && diamondPlaceholder && visual.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite && sr.sprite.name == "Square")
            {
                visual.localRotation = Quaternion.Euler(0f, 0f, 45f);
                visual.localScale *= 0.75f;
            }
            self = BuildFigure(transform, visual);
            lightPool = IndigoShapes.Create("LightPool", IndigoShapes.Disc, null, transform.position, 1f, UnityEngine.Color.clear, 5);
            lastPosition = transform.position;
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
            castPending = true; // the fight opens with the first curse
            lastPosition = transform.position;

            // The player passes through it; only its attacks hurt.
            if (Player && bodyCollider) IgnorePlayer(bodyCollider);
        }

        protected override void OnPhaseChanged(int newPhase)
        {
            // Lift the old curse right away, so the clean screen and normal controls announce the change.
            castPending = true;
            cycle = 0;
            Health.Invulnerable = true; // no chewing through two phases before the cast
            LiftCurse(liftTime);
        }

        protected override IEnumerator RunPhase(int phase)
        {
            if (castPending) { yield return CastCurse(phase); yield break; }

            var rotation = Rotation[Mathf.Min(phase, Rotation.Length - 1)];
            switch (rotation[cycle++ % rotation.Length])
            {
                case Attack.Gaze: yield return Gaze(phase); break;
                case Attack.Mandala: yield return Mandala(phase); break;
                case Attack.Blink: yield return Blink(phase); break;
                case Attack.Illusions: yield return Illusions(phase); break;
                case Attack.Starfall: yield return Starfall(phase); break;
            }
            if (Interrupted) yield break;
            state = State.Hover;
            yield return WaitOrInterrupt(PerPhase(restTimePerPhase, phase));
        }

        protected override void OnDefeated()
        {
            state = State.Defeated;
            castPending = false;
            Health.Invulnerable = false;
            if (sigil) sigil.Release();
            Dispel();
            LiftCurse(defeatClearTime); // the world slowly comes back into focus
            var warp = Warp;
            if (warp) warp.Flash(new Color(1f, 1f, 1f, 0.6f), 0.8f);
        }

        void OnDestroy()
        {
            if (lightPool) Destroy(lightPool.gameObject);
            RemoveModifiers(); // also when the scene unloads mid-fight (player died)
            if (Camera.main && Camera.main.TryGetComponent<ScreenWarp>(out var warp)) warp.Clear(0f);
        }

        void OnDamaged(DamageInfo _)
        {
            hurtFlash = 1f;
            jolt = 1f;
            if (decoys.Count > 0) ShatterDecoys(); // seen through: every copy fades at once
        }

        // ---------- Curses ----------

        Curse CurseFor(int phase) => curses is { Length: > 0 } ? PerPhase(curses, phase) : null;

        static ScreenWarp Warp => ScreenWarp.Main;

        IEnumerator CastCurse(int phase)
        {
            state = State.Casting;
            Health.Invulnerable = true;
            LiftCurse(liftTime);
            Dispel();

            var curse = CurseFor(phase);
            if (curse != null) accent = curse.accent; // the halo already shows what's coming

            yield return Glide(CastSpot(), riseTime, 0.5f, false);

            onCast.Invoke();
            HoldAttackPose(castTime);
            if (curse != null) sigil = IndigoSigil.Spawn(transform, curse.accent, curse.title, HintFor(curse), labelOffset);
            for (float t = 0f; t < castTime && !Health.IsDead; t += Time.deltaTime)
            {
                castCharge = t / castTime;
                if (sigil) sigil.Charge = castCharge;
                yield return null;
            }
            castCharge = 0f;
            if (Health.IsDead) yield break;

            if (sigil) sigil.Release();
            sigil = null;
            ApplyCurse(curse);
            jolt = 1f;
            var warp = Warp;
            if (warp)
            {
                warp.Shockwave(transform.position, 1.3f, 0.9f);
                warp.Flash(new Color(accent.r, accent.g, accent.b, 0.75f), 0.45f);
                warp.Pulse(1.5f, 0.6f);
            }
            ShakeCamera(0.35f, 0.4f);
            for (int i = 0; i < 16; i++)
            {
                var dir = Quaternion.Euler(0f, 0f, i * 22.5f) * Vector2.right;
                Puff(transform.position, dir * 9f, 0.35f, 0.1f, UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.4f), 0.5f);
            }
            onCurse.Invoke();

            castPending = false;
            Health.Invulnerable = false;
            state = State.Hover;
            yield return WaitOrInterrupt(0.5f);
        }

        void ApplyCurse(Curse curse)
        {
            RemoveModifiers();
            ActiveCurse = curse;
            if (curse == null) return;

            if (curse.mirrorMove) AddModifier(new InvertHorizontalModifier());
            if (curse.swapJumpAndAttack) AddModifier(new SwapJumpAndAttackModifier());
            if (curse.swapShootAndDash) AddModifier(new SwapShootAndDashModifier());
            if (curse.inputDelay > 0f) AddModifier(new DelayedInputModifier(curse.inputDelay));

            var warp = Warp;
            if (warp) warp.BlendTo(curse.look, landTime);
        }

        void LiftCurse(float seconds)
        {
            RemoveModifiers();
            ActiveCurse = null;
            var warp = Warp;
            if (warp) warp.Clear(seconds);
        }

        void AddModifier(IInputModifier m)
        {
            if (Game.Input == null) return;
            modifiers.Add(m);
            Game.Input.AddModifier(m);
        }

        void RemoveModifiers()
        {
            if (Game.Input != null)
                foreach (var m in modifiers) Game.Input.RemoveModifier(m);
            modifiers.Clear();
        }

        /// <summary>What the curse does, in words, for under the sigil. Upside down flips left and right on screen too.</summary>
        static string HintFor(Curse c)
        {
            if (!string.IsNullOrEmpty(c.hint)) return c.hint;
            var parts = new List<string>();
            bool upsideDown = Mathf.Abs(Mathf.DeltaAngle(c.look.roll, 180f)) < 45f;
            if (upsideDown) parts.Add("UPSIDE DOWN");
            if (c.mirrorMove != upsideDown) parts.Add("LEFT <-> RIGHT");
            if (c.swapJumpAndAttack) parts.Add("JUMP <-> ATTACK");
            if (c.swapShootAndDash) parts.Add("SHOOT <-> DASH");
            if (c.inputDelay > 0f) parts.Add("YOUR BODY LAGS BEHIND");
            return string.Join("    ", parts);
        }

        Vector2 CastSpot()
        {
            float px = Player ? Player.position.x : transform.position.x;
            float side = transform.position.x >= px ? 1f : -1f;
            return new Vector2(Mathf.Clamp(px + side * 2f, arenaMinX, arenaMaxX), groundY + castHeight);
        }

        /// <summary>Clears the arena for a cast: the attack in progress, its copies and every orb in the air.</summary>
        void Dispel()
        {
            ClearTransients();
            ClearDecoys();
            foreach (var orb in orbs)
            {
                if (!orb) continue;
                Puff(orb.transform.position, Vector2.zero, 0.5f, 0.1f, UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.5f), 0.3f);
                Destroy(orb.gameObject);
            }
            orbs.Clear();
            eyesShut = false;
            glow = 0f;
            self.fade = 1f;
            if (bodyCollider) bodyCollider.enabled = true;
        }

        // ---------- Attacks ----------

        IEnumerator Gaze(int phase)
        {
            yield return Reposition(phase);
            int beams = PerPhase(gazeBeamsPerPhase, phase);
            for (int b = 0; b < beams && Player && !Interrupted; b++)
            {
                if (b > 0) yield return WaitOrInterrupt(0.35f);
                state = State.Gaze;
                gazeDir = AimDirection;

                // Tell: a thin line follows the player, a little behind.
                var tell = Transient(FlatSprite.Create("GazeTell", null, transform.position, new Vector2(1f, 0.06f), accent, 7));
                float track = PerPhase(gazeTrackTimePerPhase, phase), turn = PerPhase(gazeTurnSpeedPerPhase, phase);
                for (float t = 0f; t < track && !Interrupted; t += Time.deltaTime)
                {
                    if (Player)
                    {
                        var want = ((Vector2)Player.position - (Vector2)transform.position).normalized;
                        float angle = Mathf.MoveTowardsAngle(Angle(gazeDir), Angle(want), turn * Time.deltaTime);
                        gazeDir = FromAngle(angle);
                    }
                    glow = t / track;
                    var c = accent;
                    c.a = 0.35f + 0.4f * (0.5f + 0.5f * Mathf.Sin(t * 30f));
                    tell.color = c;
                    Stretch(tell, transform.position, BeamEnd(gazeDir), 0.06f);
                    HoldAttackPose(attackPoseHold);
                    yield return null;
                }

                // Lock: the line stops and flickers white. Step off it now.
                for (float t = 0f; t < gazeLockTime && !Interrupted; t += Time.deltaTime)
                {
                    tell.color = UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.5f + 0.5f * Mathf.Sin(t * 60f));
                    Stretch(tell, transform.position, BeamEnd(gazeDir), 0.12f);
                    yield return null;
                }
                DestroyTransient(tell.gameObject);
                if (Interrupted) break;

                // The beam.
                var beam = Transient(FlatSprite.Create("Gaze", null, transform.position, Vector2.one, accent, 7));
                var core = Transient(FlatSprite.Create("GazeCore", null, transform.position, Vector2.one, UnityEngine.Color.white, 8));
                jolt = 1f;
                ShakeCamera(0.2f, 0.3f);
                if (Warp is { } warp) warp.Pulse(0.8f, 0.3f);
                bool hit = false;
                for (float t = 0f; t < gazeBeamTime && !Interrupted; t += Time.deltaTime)
                {
                    float k = 1f - t / gazeBeamTime;
                    float width = gazeWidth * Mathf.Sqrt(k) * (0.9f + 0.1f * Mathf.Sin(t * 70f));
                    var end = BeamEnd(gazeDir);
                    Stretch(beam, transform.position, end, width);
                    Stretch(core, transform.position, end, width * 0.4f);
                    if (!hit) hit = BeamHits(transform.position, gazeDir, Vector2.Distance(transform.position, end), width);
                    if (Random.value < 0.4f) Puff(end, Random.insideUnitCircle * 3f, 0.4f, 0.1f, accent, 0.3f);
                    yield return null;
                }
                DestroyTransient(beam.gameObject);
                DestroyTransient(core.gameObject);
                glow = 0f;
            }
        }

        IEnumerator Mandala(int phase)
        {
            yield return Reposition(phase);
            if (Interrupted || !orbPrefab) yield break;
            state = State.Mandala;

            HoldAttackPose(mandalaWindUp + attackPoseHold);
            for (float t = 0f; t < mandalaWindUp && !Interrupted; t += Time.deltaTime)
            {
                glow = t / mandalaWindUp;
                yield return null;
            }
            glow = 0f;

            int count = Mathf.Max(1, PerPhase(mandalaOrbsPerPhase, phase));
            float step = 360f / count, offset = Random.Range(0f, step);
            int rings = PerPhase(mandalaRingsPerPhase, phase);
            for (int r = 0; r < rings && !Interrupted; r++)
            {
                if (r > 0) yield return WaitOrInterrupt(mandalaRingGap);
                if (Interrupted) yield break;
                float start = offset + r * step * 0.5f; // the second ring fills the first one's gaps
                for (int i = 0; i < count; i++) FireOrb(FromAngle(start + i * step), transform.position);
                jolt = 0.6f;
                if (Warp is { } warp) warp.Pulse(0.4f, 0.25f);
            }

            float spiral = PerPhase(spiralTimePerPhase, phase);
            float angle = Random.Range(0f, 360f);
            for (float t = 0f; t < spiral && !Interrupted; t += spiralInterval)
            {
                for (int a = 0; a < spiralArms; a++) FireOrb(FromAngle(angle + a * 360f / spiralArms), transform.position);
                angle += spiralTurn;
                glow = 0.6f;
                HoldAttackPose(attackPoseHold);
                yield return WaitOrInterrupt(spiralInterval);
            }
            glow = 0f;
        }

        IEnumerator Blink(int phase)
        {
            if (!Player) yield break;
            state = State.Blink;

            // Vanish.
            for (float t = 0f; t < blinkFadeTime && !Interrupted; t += Time.deltaTime)
            {
                self.fade = 1f - t / blinkFadeTime;
                yield return null;
            }
            if (Interrupted) yield break;
            self.fade = 0f;
            if (bodyCollider) bodyCollider.enabled = false;

            // A mark hunts the player from above, then stops.
            var mark = Transient(IndigoShapes.Create("BlinkMark", IndigoShapes.Ring, null, transform.position, bodySize.y * 1.2f, accent, 7));
            var markCore = Transient(IndigoShapes.Create("BlinkMarkCore", IndigoShapes.Disc, mark.transform, Vector2.zero, 0.25f, UnityEngine.Color.white, 8));
            var drop = new Vector2(Player.position.x, groundY + blinkHeight);
            float markTime = PerPhase(blinkMarkTimePerPhase, phase);
            for (float t = 0f; t < markTime && !Interrupted; t += Time.deltaTime)
            {
                if (Player && t < markTime - blinkLockTime)
                    drop.x = Mathf.Clamp(Mathf.Lerp(drop.x, Player.position.x, 12f * Time.deltaTime), floorMinX + bodySize.x * 0.5f, floorMaxX - bodySize.x * 0.5f);
                bool locked = t >= markTime - blinkLockTime;
                mark.transform.position = drop;
                mark.transform.localScale = Vector3.one * bodySize.y * (1.2f + 0.15f * Mathf.Sin(t * (locked ? 50f : 14f)));
                mark.color = locked ? UnityEngine.Color.white : accent;
                markCore.color = new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Sin(t * 20f));
                yield return null;
            }
            DestroyTransient(markCore.gameObject);
            DestroyTransient(mark.gameObject);
            if (Interrupted) yield break;

            // Appear there and drop.
            Teleport(drop);
            self.fade = 1f;
            if (bodyCollider) bodyCollider.enabled = true;
            jolt = 1f;
            if (Warp is { } warp) warp.Pulse(0.6f, 0.2f);
            yield return WaitOrInterrupt(0.12f);
            if (Interrupted) yield break;

            var landing = new Vector2(drop.x, groundY + bodySize.y * 0.5f);
            yield return Glide(landing, blinkDropTime, 0f, false);
            squash = 0.35f;
            ShakeCamera(0.35f, 0.35f);
            if (Warp is { } warpLand) { warpLand.Shockwave(landing, 0.8f, 0.6f); warpLand.Pulse(0.7f, 0.3f); }
            TouchesPlayer(new Bounds(landing, bodySize), crushDamage, new Vector2(Player && Player.position.x < landing.x ? -8f : 8f, 6f));
            for (int i = 0; i < 8; i++)
                Puff(new Vector2(landing.x, groundY + 0.1f), new Vector2(Random.Range(-6f, 6f), Random.Range(0.5f, 2.5f)), 0.5f, 1f, new Color(accent.r, accent.g, accent.b, 0.6f), 0.5f);
            StartCoroutine(Ripple(landing.x, 1));
            StartCoroutine(Ripple(landing.x, -1));

            // Meditate: eye shut, sitting on the floor. Hit it now.
            state = State.Meditate;
            yield return WaitOrInterrupt(PerPhase(meditateTimePerPhase, phase));
            if (Interrupted) yield break;
            state = State.Hover;
            yield return Glide(new Vector2(landing.x, HoverY), 0.6f, 0f, true);
        }

        IEnumerator Ripple(float fromX, int dir)
        {
            var sr = Transient(FlatSprite.Create("Ripple", null, new Vector2(fromX, groundY), new Vector2(0.45f, rippleHeight), UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.3f), 6));
            float x = fromX + dir * bodySize.x * 0.5f, end = dir > 0 ? floorMaxX : floorMinX;
            bool hit = false;
            while (sr && (end - x) * dir > 0f)
            {
                x += dir * rippleSpeed * Time.deltaTime;
                float h = rippleHeight * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f));
                var center = new Vector2(x, groundY + h * 0.5f);
                sr.transform.position = center;
                sr.transform.localScale = new Vector3(0.45f, h, 1f);
                if (!hit) hit = TouchesPlayer(new Bounds(center, new Vector3(0.45f, h, 1f)), rippleDamage, new Vector2(dir * 6f, 7f));
                yield return null;
            }
            if (sr) DestroyTransient(sr.gameObject);
        }

        IEnumerator Illusions(int phase)
        {
            if (!Player || !orbPrefab) yield break;
            state = State.Illusions;

            // It shudders, its eye shuts, and it comes apart.
            eyesShut = true;
            for (float t = 0f; t < 0.5f && !Interrupted; t += Time.deltaTime)
            {
                glow = t / 0.5f;
                jolt = Mathf.Max(jolt, 0.3f);
                yield return null;
            }
            glow = 0f;
            if (Interrupted) yield break;

            int copies = Mathf.Max(1, PerPhase(decoysPerPhase, phase));
            for (int i = 0; i < copies; i++) decoys.Add(MakeDecoy(transform.position));
            var figures = new List<Figure> { self };
            figures.AddRange(decoys);
            if (Warp is { } warp) warp.Pulse(0.8f, 0.3f);

            // Fan out to a row of spots over the player, the real one at a random spot.
            var slots = IllusionSlots(figures.Count);
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }
            yield return MoveFigures(figures, slots, null, splitTime);

            // The shuffle: two at a time trade places, one over and one under.
            int shuffles = PerPhase(shufflesPerPhase, phase);
            float swapTime = PerPhase(swapTimePerPhase, phase);
            for (int s = 0; s < shuffles && !Interrupted; s++)
            {
                int a = Random.Range(0, figures.Count), b = (a + Random.Range(1, figures.Count)) % figures.Count;
                var arcs = new float[figures.Count];
                arcs[a] = 1.6f;
                arcs[b] = -1.6f;
                (slots[a], slots[b]) = (slots[b], slots[a]);
                yield return MoveFigures(figures, slots, arcs, swapTime);
            }
            if (Interrupted) yield break;

            // Eyes open: only the real one watches the player.
            eyesShut = false;
            yield return WaitOrInterrupt(0.4f);

            for (int v = 0; v < illusionVolleys && !Interrupted && Player; v++)
            {
                if (v > 0) yield return WaitOrInterrupt(illusionVolleyGap);
                foreach (var f in figures)
                {
                    if (Interrupted || !Player || !f.root) continue;
                    if (f != self && !decoys.Contains(f)) continue;
                    var from = (Vector2)f.root.position;
                    FireOrb((Vector2)Player.position - from, from, 1.4f);
                }
                HoldAttackPose(attackPoseHold);
            }

            yield return WaitOrInterrupt(illusionLinger);
            if (Interrupted) yield break;
            yield return FadeDecoys(0.4f);
        }

        IEnumerator Starfall(int phase)
        {
            yield return Reposition(phase);
            if (Interrupted || !Player) yield break;
            state = State.Starfall;

            HoldAttackPose(0.5f + attackPoseHold);
            for (float t = 0f; t < 0.5f && !Interrupted; t += Time.deltaTime)
            {
                glow = t / 0.5f;
                yield return null;
            }
            if (Interrupted) yield break;

            // One right on the player, the rest fanning out to both sides.
            int count = PerPhase(starsPerPhase, phase);
            float px = Player.position.x;
            for (int i = 0; i < count && !Interrupted; i++)
            {
                int ring = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1;
                float x = Mathf.Clamp(px + side * ring * starSpacing, floorMinX + starWidth * 0.5f, floorMaxX - starWidth * 0.5f);
                var pillar = FirePatch.Spawn(x, groundY, starWidth, starHeight, starWarnTime, starBurnTime, accent, UnityEngine.Color.white);
                transients.Add(pillar.gameObject);
                yield return WaitOrInterrupt(starStagger);
            }
            yield return WaitOrInterrupt(starWarnTime);
            if (Interrupted) yield break;
            ShakeCamera(0.25f, 0.3f);
            if (Warp is { } warp) warp.Pulse(0.6f, 0.3f);
            glow = 0f;
            yield return WaitOrInterrupt(starBurnTime);
        }

        // ---------- Orbs ----------

        void FireOrb(Vector2 direction, Vector2 from, float speedScale = 1f)
        {
            if (!orbPrefab) return;
            var orb = Fire(orbPrefab, direction, from);
            if (!Mathf.Approximately(speedScale, 1f)) orb.ScaleSpeed(speedScale);
            var sr = orb.GetComponentInChildren<SpriteRenderer>();
            if (sr) sr.color = UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.35f);
            orbs.Add(orb);
            orbs.RemoveAll(o => !o);
            HoldAttackPose(attackPoseHold);
        }

        // ---------- Illusions ----------

        Figure MakeDecoy(Vector2 at)
        {
            var go = new GameObject("IndigoDecoy");
            go.transform.position = at;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = bodySize;
            IgnorePlayer(box);
            go.AddComponent<IndigoDecoy>().Hit += OnDecoyHit;

            var body = self.body ? Instantiate(self.body.gameObject, go.transform).transform : null;
            var f = BuildFigure(go.transform, body);
            // The clone was copied mid-animation: give it the real one's rest pose.
            f.bodyPos = self.bodyPos;
            f.bodyScale = self.bodyScale;
            f.bodyRotation = self.bodyRotation;
            f.bodyBase = self.bodyBase;
            f.bobOffset = Random.Range(0f, 10f);
            f.fade = 0f;
            return f;
        }

        void OnDecoyHit(IndigoDecoy decoy)
        {
            var f = decoys.Find(d => d.root == decoy.transform);
            if (f == null) return;
            decoys.Remove(f);

            // Wrong one: it shatters, and a moment later its pieces fly out as a ring of orbs.
            Vector2 at = f.root.position;
            Burst(at, UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.3f));
            if (Warp is { } warp) warp.Pulse(0.7f, 0.3f);
            Destroy(f.root.gameObject);
            StartCoroutine(DecoyBurst(at));
        }

        /// <summary>The fuse lets the swing that broke the copy end first, so it can't knock the burst straight back.</summary>
        IEnumerator DecoyBurst(Vector2 at)
        {
            var spark = Transient(IndigoShapes.Create("DecoySpark", IndigoShapes.Disc, null, at, 0.6f, UnityEngine.Color.white, 9));
            for (float t = 0f; t < decoyBurstFuse && !Interrupted; t += Time.deltaTime)
            {
                spark.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, t / decoyBurstFuse);
                yield return null;
            }
            if (spark) DestroyTransient(spark.gameObject);
            if (Interrupted) yield break;
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < decoyBurstOrbs; i++) FireOrb(FromAngle(offset + i * 360f / decoyBurstOrbs), at);
        }

        void ShatterDecoys()
        {
            foreach (var f in decoys)
                if (f.root) { Burst(f.root.position, new Color(1f, 1f, 1f, 0.7f)); Destroy(f.root.gameObject); }
            decoys.Clear();
            if (Warp is { } warp) warp.Flash(new Color(1f, 1f, 1f, 0.3f), 0.25f);
        }

        void ClearDecoys()
        {
            foreach (var f in decoys)
                if (f.root) Destroy(f.root.gameObject);
            decoys.Clear();
        }

        IEnumerator FadeDecoys(float seconds)
        {
            var fading = new List<Figure>(decoys);
            decoys.Clear();
            foreach (var f in fading)
                if (f.root && f.root.TryGetComponent<Collider2D>(out var c)) c.enabled = false;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                foreach (var f in fading) f.fade = 1f - t / seconds;
                AnimateDecoys(fading);
                yield return null;
            }
            foreach (var f in fading)
                if (f.root) Destroy(f.root.gameObject);
        }

        List<Vector2> IllusionSlots(int count)
        {
            float span = (count - 1) * illusionSpacing;
            float px = Player ? Player.position.x : transform.position.x;
            float center = Mathf.Clamp(px, arenaMinX + span * 0.5f, arenaMaxX - span * 0.5f);
            var slots = new List<Vector2>();
            for (int i = 0; i < count; i++)
                slots.Add(new Vector2(center + (i - (count - 1) * 0.5f) * illusionSpacing, HoverY + 0.6f));
            return slots;
        }

        /// <summary>Glides every figure (the boss and its copies) to its slot together; arcs lift (+) or dip (-) them on the way.</summary>
        IEnumerator MoveFigures(List<Figure> figures, List<Vector2> targets, float[] arcs, float time)
        {
            var starts = new Vector2[figures.Count];
            for (int i = 0; i < figures.Count; i++) starts[i] = figures[i].root ? figures[i].root.position : Vector3.zero;

            for (float t = 0f; t < 1f && !Interrupted;)
            {
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / Mathf.Max(0.01f, time));
                float e = Mathf.SmoothStep(0f, 1f, t);
                for (int i = 0; i < figures.Count; i++)
                {
                    var f = figures[i];
                    if (!f.root) continue;
                    var pos = Vector2.Lerp(starts[i], targets[i], e);
                    if (arcs != null) pos.y += Mathf.Sin(t * Mathf.PI) * arcs[i];
                    if (f == self) MoveTo(pos);
                    else
                    {
                        f.root.position = pos;
                        f.fade = Mathf.Min(1f, f.fade + Time.fixedDeltaTime / 0.3f); // copies fade in as they split off
                    }
                }
            }
        }

        // ---------- Movement ----------

        IEnumerator Reposition(int phase)
        {
            if (!Player) yield break;
            float px = Player.position.x;
            float away = Mathf.Sign(transform.position.x - px);
            if (away == 0f) away = 1f;
            float side = Random.value < 0.65f ? away : -away; // mostly keeps its side, sometimes crosses over
            float x = px + side * sideOffset;
            if (x < arenaMinX || x > arenaMaxX) x = px - side * sideOffset;
            var target = new Vector2(Mathf.Clamp(x, arenaMinX, arenaMaxX), HoverY + Random.Range(-0.3f, 0.9f));
            yield return Glide(target, PerPhase(glideTimePerPhase, phase), 1.2f, true);
        }

        IEnumerator Glide(Vector2 target, float time, float arc, bool interruptible)
        {
            Vector2 start = transform.position;
            for (float t = 0f; t < 1f;)
            {
                if (interruptible && Interrupted) yield break;
                yield return new WaitForFixedUpdate();
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / Mathf.Max(0.01f, time));
                var pos = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                pos.y += Mathf.Sin(t * Mathf.PI) * arc;
                MoveTo(pos);
            }
        }

        void MoveTo(Vector2 position)
        {
            if (Body) Body.MovePosition(position);
            else transform.position = position;
        }

        void Teleport(Vector2 position)
        {
            if (Body) Body.position = position;
            transform.position = position;
            lastPosition = position;
        }

        // ---------- Hits ----------

        /// <summary>Where the eye beam stops: the first solid wall or floor along it.</summary>
        Vector2 BeamEnd(Vector2 dir)
        {
            Vector2 origin = transform.position;
            float best = gazeRange;
            Physics2D.Raycast(origin, dir, ContactFilter2D.noFilter, RayHits, gazeRange);
            foreach (var h in RayHits)
            {
                var c = h.collider;
                if (!c || c.isTrigger || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<PlayerController>() || c.GetComponentInParent<IDamageable>() != null) continue;
                best = Mathf.Min(best, h.distance);
            }
            return origin + dir * best;
        }

        bool BeamHits(Vector2 origin, Vector2 dir, float length, float width)
        {
            var pc = PlayerController.Instance;
            if (!pc) return false;
            var p = (Vector2)pc.transform.position - origin;
            float along = Vector2.Dot(p, dir);
            float across = Mathf.Abs(dir.x * p.y - dir.y * p.x);
            if (along < 0f || along > length || across > width * 0.5f + 0.45f) return false;
            pc.Health.TakeDamage(new DamageInfo(gazeDamage, Team.Enemy, dir * 8f + Vector2.up * 3f, gameObject));
            return true;
        }

        /// <summary>Hurts the player if `area` overlaps their body.</summary>
        bool TouchesPlayer(Bounds area, int damage, Vector2 knockback)
        {
            var pc = PlayerController.Instance;
            if (!pc) return false;
            bool touching = false;
            foreach (var c in pc.GetComponentsInChildren<Collider2D>())
            {
                if (c.isTrigger) continue;
                var b = c.bounds;
                b.center = new Vector3(b.center.x, b.center.y, area.center.z);
                if (b.Intersects(area)) { touching = true; break; }
            }
            if (!touching) return false;
            pc.Health.TakeDamage(new DamageInfo(damage, Team.Enemy, knockback, gameObject));
            return true;
        }

        void IgnorePlayer(Collider2D collider)
        {
            if (!Player) return;
            foreach (var c in Player.GetComponentsInChildren<Collider2D>())
                if (!c.isTrigger) Physics2D.IgnoreCollision(collider, c);
        }

        // ---------- Helpers ----------

        IEnumerator WaitOrInterrupt(float seconds)
        {
            for (float t = 0f; t < seconds && !Interrupted; t += Time.deltaTime) yield return null;
        }

        SpriteRenderer Transient(SpriteRenderer sr)
        {
            transients.Add(sr.gameObject);
            return sr;
        }

        void DestroyTransient(GameObject go)
        {
            transients.Remove(go);
            Destroy(go);
        }

        void ClearTransients()
        {
            foreach (var go in transients)
                if (go) Destroy(go);
            transients.Clear();
        }

        static void Stretch(SpriteRenderer sr, Vector2 a, Vector2 b, float width)
        {
            var d = b - a;
            sr.transform.position = (a + b) * 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude), width, 1f);
        }

        static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        static Vector2 FromAngle(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        static void Puff(Vector2 at, Vector2 velocity, float size, float endSize, Color color, float life) =>
            HeatPuff.Spawn(at + Random.insideUnitCircle * 0.15f, velocity, size, endSize, color, life, 12);

        static void Burst(Vector2 at, Color color)
        {
            for (int i = 0; i < 10; i++) Puff(at, Random.insideUnitCircle.normalized * Random.Range(3f, 7f), 0.4f, 0.1f, color, 0.45f);
        }

        static void ShakeCamera(float amount, float time)
        {
            if (amount > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(amount, time);
        }

        // ---------- Animation ----------

        /// <summary>One body on screen: the boss itself, or one of its copies. Same parts, animated the same way.</summary>
        sealed class Figure
        {
            public Transform root, body;
            public SpriteRenderer bodySprite, halo, white, pupil;
            public Vector3 bodyPos, bodyScale = Vector3.one;
            public Quaternion bodyRotation = Quaternion.identity;
            public Color bodyBase = UnityEngine.Color.white;
            public bool tinted;
            public float bobOffset, fade = 1f;
        }

        Figure BuildFigure(Transform root, Transform body)
        {
            var f = new Figure { root = root, body = body };
            int order = 8;
            if (body)
            {
                f.bodyPos = body.localPosition;
                f.bodyScale = body.localScale;
                f.bodyRotation = body.localRotation;
                if (body.TryGetComponent(out f.bodySprite))
                {
                    f.bodyBase = f.bodySprite.color;
                    order = f.bodySprite.sortingOrder;
                }
            }
            f.halo = IndigoShapes.Create("Halo", IndigoShapes.ThinRing, root, Vector2.zero, haloSize, UnityEngine.Color.clear, order - 1);
            f.white = IndigoShapes.Create("Eye", IndigoShapes.Disc, root, Vector2.zero, eyeSize, eyeWhite, order + 1);
            f.pupil = IndigoShapes.Create("Pupil", IndigoShapes.Disc, root, Vector2.zero, eyeSize * 0.42f, pupilColor, order + 2);
            return f;
        }

        float EyeTarget => state switch
        {
            State.Casting or State.Blink or State.Meditate or State.Defeated => 0f,
            _ => eyesShut ? 0f : 1f,
        };

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            animTime += dt;

            eyeOpen = Mathf.MoveTowards(eyeOpen, EyeTarget, dt * 6f);
            jolt = Mathf.MoveTowards(jolt, 0f, dt * 4f);
            squash = Mathf.MoveTowards(squash, 0f, dt * 1.5f);
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt / 0.12f);
            sink = Mathf.MoveTowards(sink, state == State.Defeated ? 0.6f : 0f, dt * 0.4f);

            Vector2 pos = transform.position;
            var velocity = (pos - lastPosition) / dt;
            lastPosition = pos;
            tilt = Mathf.Lerp(tilt, Mathf.Clamp(-velocity.x * 1.5f, -20f, 20f), 1f - Mathf.Exp(-8f * dt));

            // Casting spins it up; otherwise it settles back onto a corner (the diamond looks the same every 90°).
            if (castCharge > 0f) spin += dt * 720f * castCharge * castCharge;
            else spin = Mathf.MoveTowardsAngle(spin, Mathf.Round(spin / 90f) * 90f, dt * 360f);

            Animate(self, LookDirection(self), self.fade);
            AnimateLightPool();
            AnimateDecoys(decoys);
        }

        void AnimateDecoys(List<Figure> figures)
        {
            foreach (var f in figures)
                if (f.root) Animate(f, LookDirection(f), f.fade);
        }

        /// <summary>The tell: the real one looks at the player (or down its beam); copies look away.</summary>
        Vector2 LookDirection(Figure f)
        {
            if (f == self && state == State.Gaze) return gazeDir;
            if (!Player || !f.root) return Vector2.down;
            var toPlayer = ((Vector2)Player.position - (Vector2)f.root.position).normalized;
            return f == self ? toPlayer : -toPlayer;
        }

        void Animate(Figure f, Vector2 look, float alpha)
        {
            float bobAmount = state == State.Casting ? 0.35f : state == State.Meditate ? 0.04f : 0.18f;
            var offset = new Vector3(0f, Mathf.Sin((animTime + f.bobOffset) * 2.1f) * bobAmount - sink, 0f);
            float pump = 1f + 0.12f * Mathf.Max(glow, castCharge) + 0.25f * jolt;

            if (f.body)
            {
                f.body.localPosition = f.bodyPos + offset;
                f.body.localRotation = Quaternion.Euler(0f, 0f, tilt + spin) * f.bodyRotation;
                f.body.localScale = new Vector3(f.bodyScale.x * pump * (1f + squash), f.bodyScale.y * pump * (1f - squash), f.bodyScale.z);
                Tint(f, alpha, f == self ? hurtFlash : 0f);
            }

            float charge = Mathf.Max(glow, castCharge);
            f.halo.transform.localPosition = offset;
            f.halo.transform.localScale = Vector3.one * haloSize * (1f + 0.25f * charge + 0.15f * jolt);
            f.halo.transform.localRotation = Quaternion.Euler(0f, 0f, animTime * (30f + 300f * charge));
            f.halo.color = new Color(accent.r, accent.g, accent.b, (0.3f + 0.6f * charge) * alpha);

            float open = Mathf.Max(0.06f, eyeOpen);
            var eyeCenter = offset + new Vector3(0f, 0.05f, 0f);
            f.white.transform.localPosition = eyeCenter;
            f.white.transform.localScale = new Vector3(eyeSize, eyeSize * open, 1f);
            f.white.color = new Color(eyeWhite.r, eyeWhite.g, eyeWhite.b, alpha);
            f.pupil.transform.localPosition = eyeCenter + (Vector3)(look * eyeSize * 0.24f * eyeOpen);
            f.pupil.transform.localScale = new Vector3(eyeSize * 0.42f, eyeSize * 0.42f * open, 1f);
            var pupil = UnityEngine.Color.Lerp(pupilColor, accent, charge * 0.8f);
            f.pupil.color = new Color(pupil.r, pupil.g, pupil.b, eyeOpen > 0.2f ? alpha : 0f);
        }

        /// <summary>Light from its eye pooling on whatever is below it: brighter the closer it floats, gone while it's vanished.</summary>
        void AnimateLightPool()
        {
            if (!lightPool) return;
            Vector2 from = transform.position;
            float floor = groundY;
            Physics2D.Raycast(from, Vector2.down, ContactFilter2D.noFilter, RayHits, 30f);
            float best = float.PositiveInfinity;
            foreach (var h in RayHits)
            {
                var c = h.collider;
                if (!c || c.isTrigger || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<PlayerController>() || c.GetComponentInParent<IDamageable>() != null) continue;
                if (h.distance < best) { best = h.distance; floor = h.point.y; }
            }
            float height = Mathf.Max(0f, from.y - floor);
            float near = Mathf.Clamp01(1f - height / 12f);
            float pulse = 1f + 0.08f * Mathf.Sin(animTime * 3f) + 0.3f * Mathf.Max(glow, castCharge);
            lightPool.transform.position = new Vector3(from.x, floor + 0.02f, 0f);
            lightPool.transform.localScale = new Vector3(lightPoolSize.x * pulse * (0.7f + 0.5f * near), lightPoolSize.y * pulse, 1f);
            var tint = UnityEngine.Color.Lerp(accent, UnityEngine.Color.white, 0.3f);
            lightPool.color = new Color(tint.r, tint.g, tint.b, (0.25f + 0.45f * near) * self.fade * (state == State.Defeated ? 0.3f : 1f));
        }

        /// <summary>Fades / flashes the body without fighting Recolorable: only touches the color while it has to.</summary>
        static void Tint(Figure f, float alpha, float flash)
        {
            if (!f.bodySprite) return;
            if (alpha >= 0.999f && flash <= 0f)
            {
                if (f.tinted) { f.bodySprite.color = f.bodyBase; f.tinted = false; }
                else f.bodyBase = f.bodySprite.color;
                return;
            }
            var c = UnityEngine.Color.Lerp(f.bodyBase, UnityEngine.Color.white, flash);
            c.a = f.bodyBase.a * alpha;
            f.bodySprite.color = c;
            f.tinted = true;
        }

        // ---------- Defaults ----------

        static Curse[] DefaultCurses() => new[]
        {
            new Curse
            {
                title = "MIRROR", accent = new Color(0.55f, 0.45f, 1f),
                mirrorMove = true,
                look = new WarpLook
                {
                    tint = new Color(0.45f, 0.35f, 1f, 0.25f), vignette = new Color(0.12f, 0.05f, 0.3f, 0.55f),
                    mirrorGhost = 0.3f, chromatic = 0.012f, wave = 0.004f, waveFrequency = 9f,
                    sway = 4f, swaySpeed = 0.22f, zoomPulse = 0.03f,
                },
            },
            new Curse
            {
                title = "SWAP", accent = new Color(1f, 0.35f, 0.75f),
                swapJumpAndAttack = true, swapShootAndDash = true,
                look = new WarpLook
                {
                    hueShift = 0.5f, glitch = 0.55f, jitter = 0.003f, chromatic = 0.012f,
                    vignette = new Color(0.3f, 0f, 0.25f, 0.5f), sway = 2f, swaySpeed = 0.5f,
                },
            },
            new Curse
            {
                title = "ECHO", accent = new Color(0.3f, 0.9f, 1f),
                inputDelay = 0.2f,
                look = new WarpLook
                {
                    smear = 0.8f, smearZoom = 0.012f, desaturate = 0.35f, tint = new Color(0.2f, 0.7f, 1f, 0.2f),
                    wave = 0.008f, waveFrequency = 6f, chromatic = 0.01f, vignette = new Color(0f, 0.08f, 0.2f, 0.6f),
                    sway = 3f, swaySpeed = 0.18f, zoomPulse = 0.05f,
                },
            },
            new Curse
            {
                title = "INVERSION", accent = new Color(1f, 0.85f, 0.4f),
                mirrorMove = true, swapJumpAndAttack = true,
                look = new WarpLook
                {
                    roll = 180f, hueCycle = 0.15f, chromatic = 0.018f, wave = 0.006f, waveFrequency = 11f,
                    smear = 0.45f, smearZoom = 0.006f, glitch = 0.15f, vignette = new Color(0.1f, 0f, 0.2f, 0.65f),
                    sway = 6f, swaySpeed = 0.3f, zoomPulse = 0.06f,
                },
            },
        };
    }
}
