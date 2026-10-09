using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// BLUE — loneliness, sadness. A weeping figure the player climbs after and finally reaches.
    ///
    /// CLIMB (the fight starts with the level, but it can't be hurt yet). It hangs in the top-left corner of the screen,
    /// always out of reach, and throws its grief down at the player:
    ///   Tears  they form in a fan on the side facing the player, a dotted arc and a landing glow following the aim;
    ///          the aim locks (a white flash), and each is thrown in a parabola at where the player was, the boss
    ///          leaning into the throw. Ledges shelter you. Tears never hurt: one knocks the player down and away.
    ///          Any hit pops it (BlueTear).
    ///   Sighs  it breathes in (faint streaks drift across the screen), then a gust pushes the player sideways
    ///          for a moment (PlayerMotor.Wind). Hold against it.
    /// It cries harder the higher the player gets (more tears a volley, shorter rests); sighs start a little way up.
    /// Its tears also flood the shaft from below (BlueWater): water that's there from the start and rises once the fight
    /// begins, faster when it's far behind and slower right under the player's feet. Falling in is death. It stops just
    /// under the summit floor.
    ///
    /// SUMMIT. A one-way floor spans the shaft at the top and catches every fall, so nothing up there can be lost.
    /// It turns away and hops between small perches above the floor while tears fall around the player.
    /// Reach it (touch it, or hit it up close; shots don't count) to catch it. Each catch is a phase: it flees to
    /// the far side, cries harder and the perches shrink. After the last catch it stops running and settles beside
    /// the player, and the level is won (LevelController completes on Defeated).
    ///
    /// The summit (floor and perches) is built at runtime around `summit`. Placeholder look: the prefab's square
    /// plus eyes made at runtime; art can read CurrentState. Reward: Down Dash.
    /// </summary>
    public class BlueBoss : BossBase
    {
        public enum State { Drifting, Weeping, Sighing, Hopping, Perched, Settled }

        [Header("Summit")]
        [Tooltip("World point on top of the summit floor, in the middle of the shaft. Set by ROYGBIV > Bake Blue Level Into Scene.")]
        [SerializeField] Vector2 summit = new(0f, 100f);
        [Tooltip("Half the floor's width: out to the walls' inner faces.")]
        [SerializeField] float summitHalfWidth = 12f;
        [Tooltip("Top middle of each perch, from `summit`. Perches (and the floor) are one-way platforms.")]
        [SerializeField] Vector2[] perches = { new(-7.5f, 1.8f), new(-2.5f, 3.2f), new(3.5f, 1.8f), new(8.5f, 3.2f), new(0.5f, 4.4f) };
        [Tooltip("Catches to win. Each catch starts the next phase.")]
        [SerializeField] int catchesToWin = 3;
        [Tooltip("Perch width per phase: they shrink as it cries harder.")]
        [SerializeField] float[] perchWidthPerPhase = { 3f, 2.4f, 1.8f };
        [Tooltip("Seconds it sits on a perch before it moves on by itself.")]
        [SerializeField] float[] sitTimePerPhase = { 4f, 3.4f, 2.8f };
        [Tooltip("Seconds between tears while it sits.")]
        [SerializeField] float[] summitTearGapPerPhase = { 1.6f, 1.25f, 0.95f };
        [Tooltip("It sighs on every n-th perch. 0 = never.")]
        [SerializeField] int[] summitSighEveryPerPhase = { 0, 2, 2 };
        [Tooltip("At the summit tears fall from this high over the floor (off screen: only the glow warns you).")]
        [SerializeField] float summitTearHeight = 11f;
        [SerializeField] float hopTime = 0.7f;
        [SerializeField] float hopArc = 1.6f;

        [Header("Climb")]
        [Tooltip("While the player climbs it hangs in the top-left corner of the screen, this far in from the edges (x from the left, y from the top).")]
        [SerializeField] Vector2 cornerInset = new(0.4f, 0.3f);
        [Tooltip("Seconds it takes to keep up with the corner as the camera moves.")]
        [SerializeField] float driftTime = 0.2f;
        [SerializeField] float maxDriftSpeed = 30f;
        [Tooltip("How far out from its middle a tear forms, on the side facing the player.")]
        [SerializeField] float tearReach = 1.1f;
        [Tooltip("Degrees between the tears of a volley as they form around it.")]
        [SerializeField] float volleySpread = 35f;
        [Tooltip("Tears a volley, by how far up the player is (bottom, middle, top).")]
        [SerializeField] int[] climbVolley = { 1, 2, 3 };
        [Tooltip("Seconds of rest between volleys at the bottom (x) and near the top (y).")]
        [SerializeField] Vector2 climbRest = new(2.4f, 1.3f);
        [Tooltip("Seconds between the tears of a volley.")]
        [SerializeField] float volleyGap = 0.35f;
        [Tooltip("Sighs start once the player is this far up (0 = bottom, 1 = summit)...")]
        [Range(0f, 1f)] [SerializeField] float sighFrom = 0.25f;
        [Tooltip("...then every n-th volley is a sigh instead. 0 = never.")]
        [SerializeField] int sighEvery = 3;

        [Header("Tears")]
        [SerializeField] BlueTear.Settings tear = new();

        [Header("Rising water")]
        [Tooltip("Its tears flood the shaft from below while the player climbs (BlueWater).")]
        [SerializeField] bool risingWater = true;
        [SerializeField] BlueWater.Settings water = new();

        [Header("Sighs")]
        [Tooltip("Seconds it breathes in: the warning.")]
        [SerializeField] float sighWindUp = 1f;
        [SerializeField] float sighTime = 1.3f;
        [Tooltip("Sideways speed the gust adds to the player (they run at 10).")]
        [SerializeField] float sighStrength = 3.5f;
        [SerializeField] Color streakColor = new(0.8f, 0.88f, 1f, 0.55f);

        [Header("Look")]
        [SerializeField] Color platformColor = new(0.2f, 0.4f, 0.95f);
        [SerializeField] Color eyeColor = new(0.08f, 0.1f, 0.25f);
        [SerializeField] float eyeSize = 0.22f;
        [Tooltip("Draw two eyes on the side it faces. Off when the art has its own face: they stay as unseen spots its tears fall from.")]
        [SerializeField] bool drawEyes = true;
        [Tooltip("How high the eyes sit above its center, as a fraction of its height.")]
        [SerializeField] float eyeHeight = 0.18f;

        struct Streak
        {
            public SpriteRenderer sprite;
            public float speed;
        }


        readonly List<BlueTear> tears = new();
        readonly List<Streak> streaks = new();
        readonly List<Transform> perchBodies = new();

        Transform arena, visual;
        BlueWater flood;
        Vector3 visualScale;
        Collider2D bodyCollider;
        Vector2 bodySize = new(1.4f, 1.8f);
        SpriteRenderer[] eyes;

        State state;
        bool atSummit, summitStarted, caught;
        int perchIndex = -1, cycle, sits;
        float climbBaseY, gust, inhale, jolt, lean, trickle, streakDebt, lastX;
        int gustDir = 1, facing = -1;
        Vector2 driftVelocity;

        public State CurrentState => state;

        /// <summary>0 at the bottom of the climb, 1 at the summit floor.</summary>
        float ClimbProgress => Player ? Mathf.InverseLerp(climbBaseY, summit.y, Player.position.y) : 0f;

        /// <summary>Ends the attack in progress: the player reached the summit, or caught it.</summary>
        bool Interrupted => Health.IsDead || caught || (atSummit && !summitStarted);

        // ---------- Lifecycle ----------

        protected override void Awake()
        {
            base.Awake();
            // One catch per phase: the thresholds sit halfway between catches.
            catchesToWin = Mathf.Max(1, catchesToWin);
            Health.Configure(catchesToWin);
            var thresholds = new float[catchesToWin - 1];
            for (int i = 0; i < thresholds.Length; i++) thresholds[i] = (catchesToWin - 1 - i + 0.5f) / catchesToWin;
            SetPhaseThresholds(thresholds);
            // Catching means reaching it: shots from afar don't count.
            Health.DamageFilter = info => !(info.source && info.source.GetComponent<Projectile>());

            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider is BoxCollider2D box) bodySize = Vector2.Scale(box.size, transform.lossyScale);

            visual = transform.Find("Visual");
            if (visual) visualScale = visual.localScale;
            eyes = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                eyes[i] = IndigoShapes.Create("Eye", IndigoShapes.Disc, transform, Vector2.zero, eyeSize, eyeColor, 9);
                eyes[i].enabled = drawEyes;
            }

            BuildSummit();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Health.Damaged += OnCaught;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Health.Damaged -= OnCaught;
            SetWind(0f);
        }

        void OnDestroy()
        {
            SetWind(0f);
            ClearTears(splash: false); // the scene is unloading: spawning splash puffs now would leak them past it
            ClearStreaks();
            if (arena) Destroy(arena.gameObject);
            if (flood) Destroy(flood.gameObject);
        }

        // The water is there from the start, before the fight (and any intro) begins.
        void Start() => MakeWater();

        void MakeWater()
        {
            // It stops just under the summit floor: the summit can't be flooded.
            if (risingWater && !flood && Player)
                flood = BlueWater.Create(water, summit.x, summitHalfWidth, Player.position.y, summit.y - water.stopBelowSummit);
        }

        protected override void OnFightStarted()
        {
            Health.Invulnerable = true; // nothing to catch until the summit
            climbBaseY = Player ? Player.position.y : transform.position.y;
            MakeWater();
            if (flood) flood.Begin();

            // The player passes through it; reaching it is checked in Update.
            if (Player && bodyCollider)
                foreach (var c in Player.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(bodyCollider, c);
        }

        protected override IEnumerator RunPhase(int phase)
        {
            SetWind(0f);
            if (!atSummit) yield return Climb();
            else if (!summitStarted) yield return ReachSummit();
            else if (caught) yield return Flee(phase);
            else yield return Sit(phase);
        }

        protected override void OnDefeated()
        {
            SetWind(0f);
            gust = inhale = 0f;
            ClearTears();
            StartCoroutine(Settle());
        }

        void OnCaught(DamageInfo _)
        {
            caught = true;
            jolt = 1f;
            for (int i = 0; i < 12; i++)
                HeatPuff.Spawn(transform.position, Random.insideUnitCircle.normalized * Random.Range(3f, 6f), 0.3f, 0.05f, tear.color, 0.5f, 12);
            ShakeCamera(0.2f, 0.25f);
        }

        void Update()
        {
            if (!IsFighting || !Player) return;

            if (!atSummit && PlayerController.Instance.IsGrounded && Player.position.y > summit.y - 0.2f) atSummit = true;

            if (summitStarted && !caught && !Health.Invulnerable && bodyCollider &&
                PlayerTouches(bodyCollider.bounds, out var pc))
                Health.TakeDamage(new DamageInfo(1, Team.Player, Vector2.zero, pc.gameObject));
        }

        void FixedUpdate()
        {
            // While the player climbs it hangs in the top-left corner of the screen, throwing its tears down at them.
            if (!IsFighting || summitStarted || !Player) return;
            Vector2 pos = Body ? Body.position : (Vector2)transform.position;
            pos = Vector2.SmoothDamp(pos, CornerSpot(), ref driftVelocity, driftTime, maxDriftSpeed, Time.fixedDeltaTime);
            MoveTo(pos);
        }

        /// <summary>The top-left corner of the camera's view, kept over the shaft (never out past the left wall).</summary>
        Vector2 CornerSpot()
        {
            var cam = Camera.main;
            Vector2 center = cam ? (Vector2)cam.transform.position : (Vector2)Player.position + Vector2.up * 1.5f;
            float halfH = cam ? cam.orthographicSize : 3.5f, halfW = cam ? halfH * cam.aspect : 6.2f;
            float x = center.x - halfW + bodySize.x * 0.5f + cornerInset.x;
            float y = center.y + halfH - bodySize.y * 0.5f - cornerInset.y;
            return new Vector2(Mathf.Max(x, summit.x - summitHalfWidth + bodySize.x * 0.5f), y);
        }

        // ---------- Climb ----------

        IEnumerator Climb()
        {
            float k = ClimbProgress;
            cycle++;
            if (k >= sighFrom && sighEvery > 0 && cycle % sighEvery == 0) yield return Sigh();
            else
            {
                state = State.Weeping;
                int count = climbVolley[Mathf.Clamp((int)(k * climbVolley.Length), 0, climbVolley.Length - 1)];
                // The tears of a volley form in a fan on the side facing the player, then fly one after another.
                for (int i = 0; i < count && !Interrupted; i++)
                {
                    HoldAttackPose(tear.warnTime + tear.lockTime);
                    ThrowTear(i - (count - 1) * 0.5f);
                    yield return Pause(volleyGap);
                }
            }
            state = State.Drifting;
            yield return Pause(Mathf.Lerp(climbRest.x, climbRest.y, k));
        }

        IEnumerator Sigh()
        {
            state = State.Sighing;
            gustDir = Random.value < 0.5f ? -1 : 1;

            // It breathes in; faint streaks start drifting the way the gust will blow.
            HoldAttackPose(sighWindUp + sighTime);
            for (float t = 0f; t < sighWindUp && !Interrupted; t += Time.deltaTime)
            {
                inhale = t / sighWindUp;
                gust = 0.25f;
                yield return null;
            }

            // The gust: eases in and out so it shoves rather than snaps.
            for (float t = 0f; t < sighTime && !Interrupted; t += Time.deltaTime)
            {
                float k = Mathf.Min(1f, Mathf.Min(t, sighTime - t) / 0.2f);
                inhale = 1f - t / sighTime;
                gust = Mathf.Max(0.25f, k);
                SetWind(gustDir * sighStrength * k);
                yield return null;
            }
            SetWind(0f);
            gust = inhale = 0f;
        }

        // ---------- Summit ----------

        IEnumerator ReachSummit()
        {
            summitStarted = true;
            ClearTears();
            gust = inhale = 0f;
            driftVelocity = Vector2.zero;

            // It turns away and runs to the far side. From here on it can be caught.
            state = State.Hopping;
            perchIndex = PickPerch(true);
            yield return Hop(Seat(perchIndex), hopTime * 1.4f, false);
            Health.Invulnerable = false;
            state = State.Perched;
        }

        IEnumerator Sit(int phase)
        {
            state = State.Perched;
            sits++;
            int every = PerPhase(summitSighEveryPerPhase, phase);
            if (every > 0 && sits % every == 0)
            {
                yield return Sigh();
                state = State.Perched;
            }

            float sit = PerPhase(sitTimePerPhase, phase), gap = PerPhase(summitTearGapPerPhase, phase);
            float next = 0.4f;
            for (float t = 0f; t < sit && !Interrupted; t += Time.deltaTime)
            {
                if (t >= next && Player)
                {
                    next += gap;
                    var from = new Vector2(Player.position.x + Random.Range(-1.5f, 1.5f), summit.y + summitTearHeight);
                    from.x = Mathf.Clamp(from.x, -summitHalfWidth + 0.5f, summitHalfWidth - 0.5f);
                    DropTear(from);
                }
                yield return null;
            }
            if (Interrupted) yield break;

            // It moves on by itself; it can still be caught on the way.
            state = State.Hopping;
            perchIndex = PickPerch(false);
            yield return Hop(Seat(perchIndex), hopTime, true);
            if (!Interrupted) state = State.Perched;
        }

        IEnumerator Flee(int phase)
        {
            // Caught: it slips away to the far side and can't be caught again until it lands. The perches shrink.
            caught = false;
            Health.Invulnerable = true;
            state = State.Hopping;
            StartCoroutine(ResizePerches(PerPhase(perchWidthPerPhase, phase), hopTime));
            perchIndex = PickPerch(true);
            yield return Hop(Seat(perchIndex), hopTime, false);
            Health.Invulnerable = false;
            state = State.Perched;
        }

        IEnumerator Settle()
        {
            // It stops running and comes down to sit beside the player.
            state = State.Hopping;
            Health.Invulnerable = true;
            float px = Player ? Player.position.x : transform.position.x;
            float side = transform.position.x >= px ? 1f : -1f;
            float x = Mathf.Clamp(px + side * 1.4f, -summitHalfWidth + bodySize.x, summitHalfWidth - bodySize.x);
            yield return Hop(new Vector2(x, summit.y + bodySize.y * 0.5f), hopTime * 1.6f, false);
            state = State.Settled;
            for (int i = 0; i < 16; i++)
            {
                var dir = (Vector2)(Quaternion.Euler(0f, 0f, i * 22.5f) * Vector2.right);
                HeatPuff.Spawn(transform.position, dir * 5f, 0.35f, 0.05f, UnityEngine.Color.Lerp(tear.color, UnityEngine.Color.white, 0.5f), 0.7f, 12);
            }
        }

        // ---------- Tears ----------

        /// <summary>
        /// A tear forms beside it (`slot` turns it around the side facing the player: 0 = straight at them) and is
        /// thrown at the player in an arc. While the player climbs it flies through the summit's platforms.
        /// </summary>
        void ThrowTear(float slot)
        {
            tears.RemoveAll(t => !t);
            Vector2 Origin()
            {
                Vector2 me = transform.position;
                var toPlayer = Player ? ((Vector2)Player.position - me).normalized : Vector2.right;
                return me + (Vector2)(Quaternion.Euler(0f, 0f, slot * volleySpread) * toPlayer) * tearReach;
            }
            var thrown = BlueTear.Throw(Origin, () => Player ? (Vector2)Player.position : Origin(), tear, flood, summitStarted ? null : arena);
            thrown.Launched = () => { lean = 1f; jolt = Mathf.Max(jolt, 0.3f); };
            tears.Add(thrown);
        }

        /// <summary>A tear dropped straight down from `from` (the summit's tears, falling from the sky).</summary>
        void DropTear(Vector2 from)
        {
            tears.RemoveAll(t => !t);
            tears.Add(BlueTear.Throw(() => from, null, tear, flood, null));
        }

        void ClearTears(bool splash = true)
        {
            foreach (var t in tears)
                if (t)
                {
                    if (splash) t.Splash();
                    else Destroy(t.gameObject);
                }
            tears.Clear();
        }

        // ---------- Movement ----------

        IEnumerator Hop(Vector2 target, float time, bool interruptible)
        {
            Vector2 start = transform.position;
            float arc = hopArc + Mathf.Max(0f, target.y - start.y) * 0.4f;
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

        /// <summary>Where it sits on perch i.</summary>
        Vector2 Seat(int i) => summit + perches[i] + Vector2.up * bodySize.y * 0.5f;

        /// <summary>A perch away from the player, never the one it's on: the farthest, or one of the two farthest.</summary>
        int PickPerch(bool farthest)
        {
            var order = new List<int>();
            for (int i = 0; i < perches.Length; i++)
                if (i != perchIndex) order.Add(i);
            if (order.Count == 0) return Mathf.Max(0, perchIndex);
            Vector2 p = Player ? (Vector2)Player.position : summit;
            order.Sort((a, b) => Vector2.Distance(Seat(b), p).CompareTo(Vector2.Distance(Seat(a), p)));
            return farthest ? order[0] : order[Random.Range(0, Mathf.Min(2, order.Count))];
        }

        // ---------- Summit build ----------

        void BuildSummit()
        {
            arena = new GameObject("BlueSummit").transform;
            Platform("SummitFloor", summit + Vector2.down * 0.25f, new Vector2(summitHalfWidth * 2f, 0.5f));
            float width = PerPhase(perchWidthPerPhase, 0);
            foreach (var p in perches)
                perchBodies.Add(Platform("Perch", summit + p + Vector2.down * 0.175f, new Vector2(width, 0.35f)));
        }

        /// <summary>A one-way platform that looks like the level's ledges (gray until Blue is restored).</summary>
        Transform Platform(string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name);
            go.SetActive(false); // Recolorable reads its color when it wakes up
            go.transform.SetParent(arena, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FlatSprite.Square;
            sr.color = platformColor;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            go.AddComponent<Recolorable>().ColorId = Color;
            go.SetActive(true);
            return go.transform;
        }

        IEnumerator ResizePerches(float width, float time)
        {
            float from = perchBodies.Count > 0 ? perchBodies[0].localScale.x : width;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, time));
                float w = Mathf.Lerp(from, width, Mathf.SmoothStep(0f, 1f, t));
                foreach (var p in perchBodies)
                    if (p) p.localScale = new Vector3(w, p.localScale.y, 1f);
                yield return null;
            }
        }

        // ---------- Wind ----------

        void SetWind(float speed)
        {
            if (PlayerController.Instance && PlayerController.Instance.TryGetComponent<PlayerMotor>(out var motor)) motor.Wind = speed;
        }

        void UpdateStreaks()
        {
            var cam = Camera.main;
            if (!cam) return;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;

            streakDebt += Time.deltaTime * gust * 45f;
            for (; streakDebt >= 1f; streakDebt -= 1f)
            {
                var at = new Vector2(c.x - gustDir * (halfW + 1f), c.y + Random.Range(-halfH, halfH));
                var color = streakColor;
                color.a *= 0.4f + 0.6f * gust;
                var sr = FlatSprite.Create("Gust", null, at, new Vector2(Random.Range(0.8f, 2f), 0.05f), color, 15);
                streaks.Add(new Streak { sprite = sr, speed = gustDir * Mathf.Lerp(5f, 22f, gust) * Random.Range(0.8f, 1.2f) });
            }

            for (int i = streaks.Count - 1; i >= 0; i--)
            {
                var s = streaks[i];
                if (s.sprite)
                {
                    s.sprite.transform.position += Vector3.right * s.speed * Time.deltaTime;
                    if (Mathf.Abs(s.sprite.transform.position.x - c.x) <= halfW + 2f) continue;
                    Destroy(s.sprite.gameObject);
                }
                streaks.RemoveAt(i);
            }
        }

        void ClearStreaks()
        {
            foreach (var s in streaks)
                if (s.sprite) Destroy(s.sprite.gameObject);
            streaks.Clear();
        }

        // ---------- Look ----------

        void LateUpdate()
        {
            UpdateStreaks();
            float dx = transform.position.x - lastX;
            lastX = transform.position.x;
            if (Player)
            {
                int toPlayer = Player.position.x >= transform.position.x ? 1 : -1;
                // Looks down at the player while they climb, the way it's going when it hops,
                // and turns its back on them while it sits on a perch.
                if (state == State.Perched) facing = -toPlayer;
                else if (state == State.Hopping) { if (Mathf.Abs(dx) > 0.01f) facing = dx > 0f ? 1 : -1; }
                else facing = toPlayer;
            }

            // Sobbing shoulders, a deep breath before a sigh, a start when it's caught.
            float t = Time.time;
            bool crying = state != State.Settled;
            float sob = crying ? Mathf.Sin(t * 9f) * 0.04f : Mathf.Sin(t * 2f) * 0.02f;
            float swell = 1f + inhale * 0.18f;
            jolt = Mathf.MoveTowards(jolt, 0f, Time.deltaTime * 3f);
            lean = Mathf.MoveTowards(lean, 0f, Time.deltaTime * 4f);
            if (visual)
            {
                visual.localScale = new Vector3(visualScale.x * swell * (1f + sob), visualScale.y * swell * (1f - sob), 1f);
                visual.localPosition = jolt > 0f ? (Vector3)(Random.insideUnitCircle * jolt * 0.15f) : Vector3.zero;
                // It leans into each throw, toward the player, and straightens up again.
                visual.localRotation = Quaternion.Euler(0f, 0f, -facing * 18f * Mathf.Sin(lean * Mathf.PI * 0.5f));
            }

            // Eyes on the side it faces, cast down; shut (calm) once it has settled.
            for (int i = 0; i < eyes.Length; i++)
            {
                var eye = eyes[i];
                float x = drawEyes ? facing * bodySize.x * (0.12f + i * 0.2f) * swell : 0f;
                eye.transform.localPosition = new Vector3(x, bodySize.y * eyeHeight * swell, 0f) + (visual ? visual.localPosition : Vector3.zero);
                eye.transform.localScale = state == State.Settled ? new Vector3(eyeSize, eyeSize * 0.25f, 1f) : Vector3.one * eyeSize;
            }

            // Tears run from its eyes while it cries.
            trickle -= Time.deltaTime;
            if (crying && IsFighting && trickle <= 0f)
            {
                trickle = Random.Range(0.25f, 0.6f);
                var eye = eyes[Random.Range(0, eyes.Length)].transform.position;
                HeatPuff.Spawn(eye + Vector3.down * eyeSize, new Vector2(facing * 0.4f, -2.5f), 0.12f, 0.05f, tear.color, 0.6f, 10);
            }
        }

        // ---------- Helpers ----------

        /// <summary>True if `area` overlaps the player's body (their solid colliders).</summary>
        public static bool PlayerTouches(Bounds area, out PlayerController player)
        {
            player = PlayerController.Instance;
            if (!player || player.Health.IsDead) return false;
            foreach (var c in player.GetComponentsInChildren<Collider2D>())
            {
                if (c.isTrigger) continue;
                var b = c.bounds;
                b.center = new Vector3(b.center.x, b.center.y, area.center.z);
                if (b.Intersects(area)) return true;
            }
            return false;
        }

        IEnumerator Pause(float seconds)
        {
            for (float t = 0f; t < seconds && !Interrupted; t += Time.deltaTime) yield return null;
        }

        static void ShakeCamera(float amount, float time)
        {
            if (amount > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(amount, time);
        }
    }
}
