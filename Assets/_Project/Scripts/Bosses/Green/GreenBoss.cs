using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// GREEN — envy, disgust. A bramble rooted in the arena floor that wants what the player has.
    /// Its bark shrugs off every hit: it can only be hurt after something it stole has been torn back out of it.
    ///
    /// The steal loop:
    ///   COVET: whenever it holds nothing, it hooks a thread onto the player and yanks (telegraphed, can't be
    ///          dodged). It takes the ability the player used MOST RECENTLY. The thread glows in that ability's
    ///          color and changes live, so the player picks what to give up by using something else in time.
    ///   LASH:  its vine whips along the floor toward the player (jump it). A lash that connects also steals,
    ///          while there's room for another pod.
    ///   POD:   each stolen ability grows into a pod somewhere in the arena, its core in that ability's color.
    ///          Break it: the ability comes back and the boss WILTS, heart exposed, for a few hits.
    ///          Leave it: it ripens and bursts in a cloud of spores, then reseeds somewhere else.
    ///          Pods shrug off shots, so they have to be broken up close, and the boss guards them: a hit on
    ///          a pod makes thorns burst up under the player (at most every guardCooldown). Hit, hop, hit.
    /// While it holds an ability it uses its own copy against the player:
    ///   Light Shot   -> volleys from its heart.
    ///   Dash         -> uproots, dashes at the player a few times, and puts down roots where it stops.
    ///   Blaze Strike -> a charged swipe when the player is close (right after a Dash, too).
    ///
    /// Phase 0: lash, one pod at a time. Phase 1: + thorns under the player, two pods.
    /// Phase 2: a low lash then a high one (stay down), three pods, everything faster.
    /// On each phase change it burrows and comes up on the side away from the player, then covets right away.
    /// 12 HP, 4 hits per wilt, thresholds 0.67 / 0.34 = one phase per wilt.
    /// Everything stolen is returned on defeat, and if the scene unloads mid-fight (player died).
    ///
    /// Animation is procedural, on the Pose child (pivot at the feet; made at runtime if the prefab has none).
    /// Art can read CurrentState instead.
    /// Reward: TBD.
    /// </summary>
    [RequireComponent(typeof(AbilityLoadout))]
    public class GreenBoss : BossBase
    {
        public enum Attack { Lash, Thorns }

        public enum State { Rooted, Covet, LashWindUp, Lash, Volley, Uprooted, StrikeWindUp, Wilted, Recover, Burrow, Emerge, Defeated }

        // Each phase opens with its new pattern so the player sees it right away.
        static readonly Attack[][] Rotation =
        {
            new[] { Attack.Lash },
            new[] { Attack.Thorns, Attack.Lash },
            new[] { Attack.Lash, Attack.Thorns },
        };

        // Per phase, the lashes in one Lash attack: false = low (jump it), true = high (stay down).
        static readonly bool[][] LashHeights =
        {
            new[] { false },
            new[] { false, false },
            new[] { false, true },
        };

        [Header("Steal")]
        [Tooltip("What it takes when the player hasn't used anything yet. After that it always takes the most recent.")]
        [SerializeField] AbilityId[] stealOrder = { AbilityId.LightShot, AbilityId.Dash, AbilityId.BlazeStrike };
        [Tooltip("Most pods growing at once, per phase. Lashes only steal while there's room.")]
        [SerializeField] int[] maxPodsPerPhase = { 1, 2, 3 };
        [Tooltip("Seconds after a wilt before it covets again (if it holds nothing), per phase.")]
        [SerializeField] float[] covetDelayPerPhase = { 4f, 3.5f, 3f };
        [Tooltip("The thread's telegraph before the yank, per phase. Long enough to switch what it takes.")]
        [SerializeField] float[] covetTimePerPhase = { 1.8f, 1.5f, 1.3f };
        [Tooltip("A pod with nothing in it: the player had nothing it could take (testing out of order).")]
        [SerializeField] Color bitterColor = new(0.45f, 0.4f, 0.2f);
        [SerializeField] UnityEvent onSteal = new();

        [Header("Pods")]
        [Tooltip("Feet of the stalks: floor and platform tops. Defaults fit the generated arena (floor at -2.5, platforms at ±6).")]
        [SerializeField] Vector2[] podSpots =
        {
            new(-6f, 0.75f), new(6f, 0.75f),
            new(-17.5f, -2.5f), new(17.5f, -2.5f), new(-12f, -2.5f), new(12f, -2.5f), new(0f, -2.5f),
        };
        [SerializeField] float podStalkHeight = 0.5f;
        [Tooltip("Hits to break a pod, per phase.")]
        [SerializeField] int[] podHitsPerPhase = { 3, 4, 5 };
        [Tooltip("Off: shots bounce off pods, so the player has to go and break them up close.")]
        [SerializeField] bool podsTakeShots;
        [Tooltip("Seconds from sprouting to bursting, per phase.")]
        [SerializeField] float[] podRipenTimePerPhase = { 12f, 10f, 9f };
        [SerializeField] float sporeRadius = 2.2f;
        [SerializeField] int sporeDamage = 1;
        [SerializeField] Color sporeColor = new(0.7f, 0.75f, 0.25f, 0.7f);

        [Header("Guarding pods")]
        [Tooltip("A hit on a pod makes thorns burst up under the player after this tell. 0 = it doesn't guard them.")]
        [SerializeField] float guardWarnTime = 0.45f;
        [Tooltip("Seconds before a pod hit can set off thorns again.")]
        [SerializeField] float guardCooldown = 1.2f;
        [SerializeField] float guardWidth = 2.4f;

        [Header("Wilt")]
        [Tooltip("Seconds it droops with its heart exposed after a pod breaks. Long enough to run over from the pod.")]
        [SerializeField] float wiltTime = 5f;
        [Tooltip("It snaps out of it early after this many hits. With 12 HP and thresholds 0.67 / 0.34, 4 = one phase per wilt.")]
        [SerializeField] int maxHitsPerWilt = 4;
        [SerializeField] float recoverTime = 0.6f;
        [Tooltip("Art/SFX: a pod broke and it starts to wilt.")]
        [SerializeField] UnityEvent onWilt = new();

        [Header("Lash")]
        [SerializeField] float[] lashWarnTimePerPhase = { 0.8f, 0.65f, 0.55f };
        [Tooltip("How fast the vine's tip races out along the floor.")]
        [SerializeField] float lashSpeed = 34f;
        [SerializeField] float lashHold = 0.15f;
        [SerializeField] float lashGap = 0.3f;
        [SerializeField] float lashThickness = 0.5f;
        [Tooltip("Center of the low lash above the floor: jump it.")]
        [SerializeField] float lowLashHeight = 0.3f;
        [Tooltip("Center of the high lash above the floor: just over a standing player's head, so stay down.")]
        [SerializeField] float highLashHeight = 1.7f;
        [SerializeField] int lashDamage = 1;
        [SerializeField] float lashKnockback = 7f;
        [SerializeField] Color vineColor = new(0.12f, 0.4f, 0.12f);
        [SerializeField] Color lashWarnColor = new(0.45f, 1f, 0.3f, 0.6f);

        [Header("Thorns")]
        [SerializeField] int[] thornCountPerPhase = { 3, 3, 4 };
        [SerializeField] float thornSpacing = 2.2f;
        [SerializeField] float thornWidth = 1f;
        [SerializeField] float thornHeight = 1.6f;
        [SerializeField] float thornWarnTime = 0.7f;
        [SerializeField] float thornTime = 0.5f;
        [SerializeField] float thornStagger = 0.12f;
        [SerializeField] Color thornColor = new(0.2f, 0.55f, 0.15f);
        [SerializeField] Color thornTip = new(0.6f, 0.9f, 0.3f);

        [Header("Stolen abilities")]
        [SerializeField] int[] shotVolleyPerPhase = { 2, 3, 4 };
        [Tooltip("Seconds between its shots: at least the Light Shot's own cooldown.")]
        [SerializeField] float shotInterval = 0.55f;
        [SerializeField] float volleyWindUp = 0.45f;
        [SerializeField] int[] dashesPerPhase = { 2, 3, 3 };
        [Tooltip("Seconds between its dashes: at least the Dash's own cooldown.")]
        [SerializeField] float dashGap = 0.55f;
        [SerializeField] float uprootTime = 0.35f;
        [SerializeField] int dashDamage = 1;
        [SerializeField] float dashKnockback = 8f;
        [Tooltip("It swings Blaze Strike when the player is within this many units of its center.")]
        [SerializeField] float strikeRange = 3.2f;
        [SerializeField] float strikeWindUp = 0.6f;

        [Header("Movement")]
        [Tooltip("The floor between the walls: top surface and inner wall faces.")]
        [SerializeField] float groundY = -2.5f;
        [SerializeField] float floorMinX = -19.5f;
        [SerializeField] float floorMaxX = 19.5f;
        [Tooltip("Where it can take root. Each phase it burrows to the one farthest from the player.")]
        [SerializeField] float[] rootSpots = { 12f, -12f, 0f };
        [SerializeField] float burrowTime = 0.6f;

        [Header("Timing")]
        [SerializeField] float openingDelay = 0.8f;
        [SerializeField] float[] restTimePerPhase = { 1.4f, 1.1f, 0.8f };

        [Header("Animation")]
        [Tooltip("Squashed, leaned and shaken by the procedural animation; its origin is the boss's feet. Made from the Visual child if missing.")]
        [SerializeField] Transform pose;
        [SerializeField] Color wiltColor = new(0.45f, 0.32f, 0.12f);
        [SerializeField] Color heartClosed = new(0.1f, 0.25f, 0.08f);
        [SerializeField] Color heartOpen = new(1f, 0.45f, 0.65f);
        [SerializeField] Color leafColor = new(0.35f, 0.8f, 0.3f, 0.8f);
        [SerializeField] Color dirtColor = new(0.4f, 0.3f, 0.2f, 0.7f);
        [SerializeField] float attackPoseHold = 0.35f;

        AbilityLoadout loadout;
        readonly List<AbilityId> stolen = new();
        readonly List<GreenPod> pods = new();
        readonly List<GameObject> transients = new(); // threads, vines, warnings: cleaned up if it dies mid-attack

        State state;
        Collider2D bodyCollider;
        Hitbox dashHitbox;
        SpriteRenderer glow, glowSource, heart;
        Vector3 poseBasePos, poseBaseScale = Vector3.one;
        Vector2 smoothVelocity, lastPosition;
        float halfWidth = 1f, halfHeight = 1f;
        float nextCovetAt, nextGuardAt;
        int cycle, nativeCycle, wiltHits;
        bool wiltRequested, rerootRequested;
        AbilityId lastStolenUse;

        // Animation state, written by the coroutines and read in LateUpdate.
        float stateTime, animTime, windUp01, punch, jolt, flash, landSquash, sink;
        float emitA, emitB;
        int facing = 1;
        Color glowTint;
        float glowAmount;

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

        public bool Wilted => state == State.Wilted;
        public IReadOnlyList<AbilityId> Stolen => stolen; // for UI

        public override BossPose Pose => state switch
        {
            State.Covet or State.LashWindUp or State.Lash or State.Volley or State.StrikeWindUp => BossPose.Attack,
            State.Uprooted => BossPose.Move,
            State.Wilted or State.Defeated => BossPose.Hurt,
            _ => base.Pose,
        };

        bool Interrupted => wiltRequested || rerootRequested;
        bool CovetReady => pods.Count == 0 && Time.time >= nextCovetAt;
        bool PodRoom => pods.Count < PerPhase(maxPodsPerPhase, Phase);
        float GroundedY => groundY + halfHeight;
        Vector2 HeartPoint => PosePoint(0f, 0.55f);

        protected override void Awake()
        {
            base.Awake();
            loadout = GetComponent<AbilityLoadout>();
            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider is BoxCollider2D box)
            {
                halfWidth = box.size.x * 0.5f * transform.lossyScale.x;
                halfHeight = box.size.y * 0.5f * transform.lossyScale.y;
            }

            if (!pose) pose = transform.Find("Pose");
            if (!pose && transform.Find("Visual") is { } visual)
            {
                // A pivot at the feet, so it sways and droops from its roots.
                pose = new GameObject("Pose").transform;
                pose.SetParent(transform, false);
                pose.localPosition = new Vector3(0f, -halfHeight / transform.lossyScale.y, 0f);
                visual.SetParent(pose, true);
            }
            if (pose)
            {
                poseBasePos = pose.localPosition;
                poseBaseScale = pose.localScale;
            }
            lastPosition = transform.position;

            Health.DamageFilter = FilterHit;
            dashHitbox = MakeDashHitbox();
        }

        // After every Awake, so nothing else picks up the overlays as part of the art.
        void Start()
        {
            MoveTo(new Vector2(transform.position.x, GroundedY)); // rooted in the floor from the start

            glowSource = pose ? pose.GetComponentInChildren<SpriteRenderer>() : null;
            if (!glowSource) return;
            glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(glowSource.transform, false);
            glow.sprite = glowSource.sprite;
            glow.sortingOrder = glowSource.sortingOrder + 1;
            glow.color = UnityEngine.Color.clear;

            heart = FlatSprite.Create("Heart", pose, HeartPoint, Vector2.one * 0.55f, heartClosed, glowSource.sortingOrder + 2);
            heart.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
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
            lastPosition = transform.position;
            nextCovetAt = Time.time + openingDelay;

            // The player walks through it; only its attacks hurt.
            if (Player && bodyCollider)
                foreach (var c in Player.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(bodyCollider, c);
        }

        protected override void OnPhaseChanged(int newPhase) => rerootRequested = true;

        protected override IEnumerator RunPhase(int phase)
        {
            if (rerootRequested) { yield return Reroot(); yield break; }
            if (wiltRequested) { yield return Wilt(); yield break; }
            if (CovetReady) { yield return Covet(phase); yield break; }

            // Its own attacks take turns with the player's stolen ones, so a pod left growing keeps costing.
            if (cycle++ % 2 == 1 && PickStolen(out var id)) yield return UseStolen(id, phase);
            else yield return NativeAttack(phase);
            yield return WaitOrInterrupt(PerPhase(restTimePerPhase, phase));
        }

        protected override void OnDefeated()
        {
            CurrentState = State.Defeated;
            windUp01 = 0f;
            if (dashHitbox) dashHitbox.gameObject.SetActive(false);
            if (Body) Body.linearVelocity = Vector2.zero;
            ClearTransients();
            ReturnAll();
        }

        void OnDestroy() => ReturnAll(); // also when the scene unloads mid-fight (player died)

        void FixedUpdate()
        {
            // Dashing with the player's Dash: kinematic, so nothing stops it at the walls but this.
            if (Body && Body.linearVelocity != Vector2.zero)
            {
                float x = Mathf.Clamp(Body.position.x, floorMinX + halfWidth, floorMaxX - halfWidth);
                if (!Mathf.Approximately(x, Body.position.x))
                {
                    Body.position = new Vector2(x, Body.position.y);
                    Body.linearVelocity = Vector2.zero;
                }
            }

            var position = (Vector2)transform.position;
            smoothVelocity = Vector2.Lerp(smoothVelocity, (position - lastPosition) / Time.fixedDeltaTime, 0.25f);
            lastPosition = position;
        }

        // ---------- Bark & heart ----------

        /// <summary>Health's veto: only a wilted boss takes damage. Anything else thuds off the bark.</summary>
        bool FilterHit(DamageInfo info)
        {
            if (state == State.Wilted) return true;
            Vector2 at = info.source ? bodyCollider.ClosestPoint(info.source.transform.position) : (Vector2)transform.position;
            for (int i = 0; i < 3; i++)
                Puff(at, Random.insideUnitCircle.normalized * Random.Range(2f, 4f), 0.18f, 0.05f, leafColor, 0.4f, 12);
            jolt = 0.5f;
            return false;
        }

        void OnDamaged(DamageInfo _)
        {
            if (state == State.Wilted) wiltHits++;
            flash = 1f;
            jolt = 1f;
            for (int i = 0; i < 4; i++)
                Puff(HeartPoint, Random.insideUnitCircle.normalized * Random.Range(2f, 5f), 0.2f, 0.05f, heartOpen, 0.4f, 12);
        }

        // ---------- Stealing ----------

        /// <summary>The ability the player used most recently that it can still take. None if there isn't one.</summary>
        AbilityId NextStealTarget()
        {
            var pc = PlayerController.Instance;
            if (!pc) return AbilityId.None;

            AbilityBase best = null;
            foreach (var a in pc.Loadout.All)
                if (a.enabled && loadout.Get(a.Id) && (best == null || a.LastUsedAt > best.LastUsedAt))
                    best = a;
            if (best == null) return AbilityId.None;
            if (!float.IsNegativeInfinity(best.LastUsedAt)) return best.Id;

            // Nothing used yet: go down the list.
            foreach (var id in stealOrder)
                if (pc.Loadout.Has(id) && loadout.Get(id)) return id;
            return best.Id;
        }

        /// <summary>Takes `id` from the player (None: nothing to take) and grows it into a pod.</summary>
        void Steal(AbilityId id, Vector2 from)
        {
            if (id != AbilityId.None)
            {
                stolen.Add(id);
                GameEvents.RaiseAbilityStolen(id);
                loadout.Grant(id);
            }
            onSteal.Invoke();
            SpawnPod(id, from, PodSpot());
        }

        void SpawnPod(AbilityId id, Vector2 from, Vector2 spot)
        {
            pods.Add(GreenPod.Spawn(id, Tint(id), from, spot, podStalkHeight, PerPhase(podHitsPerPhase, Phase), podsTakeShots,
                PerPhase(podRipenTimePerPhase, Phase), OnPodHurt, OnPodBroken, OnPodRipened));
        }

        /// <summary>Away from the player, not too far from itself, never inside it or on another pod.</summary>
        Vector2 PodSpot()
        {
            Vector2 player = Player ? (Vector2)Player.position : Vector2.zero;
            float bossX = transform.position.x;
            Vector2 best = podSpots.Length > 0 ? podSpots[0] : new Vector2(0f, groundY);
            float bestScore = float.NegativeInfinity;
            foreach (var spot in podSpots)
            {
                bool taken = false;
                foreach (var p in pods)
                    if (p && Vector2.Distance(p.Spot, spot) < 1f) taken = true;
                if (taken || Mathf.Abs(spot.x - bossX) < halfWidth + 1.5f) continue;

                // Make the player go and get it, but near enough to the boss that getting it is a risk.
                float score = Vector2.Distance(spot, player) - 0.5f * Mathf.Abs(spot.x - bossX) + Random.Range(0f, 4f);
                if (score > bestScore) { bestScore = score; best = spot; }
            }
            return best;
        }

        /// <summary>Jealous of its pods: a hit on one sets off thorns under whoever's hitting it.</summary>
        void OnPodHurt(GreenPod pod)
        {
            if (!IsFighting || guardWarnTime <= 0f || Time.time < nextGuardAt || !Player) return;
            if (state is State.Wilted or State.Recover or State.Burrow or State.Emerge) return;
            nextGuardAt = Time.time + guardCooldown;

            // From the surface the pod grows on, so it works on a platform too.
            float half = guardWidth * 0.5f;
            float x = Mathf.Clamp(Player.position.x, floorMinX + half, floorMaxX - half);
            FirePatch.Spawn(x, pod.Spot.y, guardWidth, thornHeight, guardWarnTime, thornTime, thornColor, thornTip);
            jolt = Mathf.Max(jolt, 0.6f); // flinches, as if it felt that
        }

        void OnPodBroken(GreenPod pod)
        {
            pods.Remove(pod);
            if (pod.Ability != AbilityId.None && stolen.Remove(pod.Ability))
            {
                loadout.Revoke(pod.Ability);
                GameEvents.RaiseAbilityReturned(pod.Ability);
            }
            // The ability streaks back to the player.
            if (Player)
            {
                var to = ((Vector2)Player.position - (Vector2)pod.transform.position).normalized;
                for (int i = 0; i < 6; i++)
                    Puff(pod.transform.position, to * Random.Range(6f, 10f) + Random.insideUnitCircle, 0.25f, 0.05f, Tint(pod.Ability), 0.5f, 12);
            }
            if (IsFighting) wiltRequested = true;
        }

        void OnPodRipened(GreenPod pod)
        {
            pods.Remove(pod);
            Vector2 at = pod.transform.position;
            for (int i = 0; i < 14; i++)
                Puff(at, Random.insideUnitCircle * Random.Range(2f, 5f), 0.35f, 1.3f, sporeColor, 0.9f, 12);
            ShakeCamera(0.1f, 0.2f);

            var pc = PlayerController.Instance;
            if (pc && Vector2.Distance(pc.transform.position, at) <= sporeRadius)
            {
                var push = ((Vector2)pc.transform.position - at).normalized * 7f + Vector2.up * 3f;
                pc.Health.TakeDamage(new DamageInfo(sporeDamage, Team.Enemy, push, gameObject));
            }
            // It keeps what's inside: a new pod sprouts somewhere else.
            if (IsFighting) SpawnPod(pod.Ability, at, PodSpot());
        }

        void ReturnAll()
        {
            foreach (var id in stolen) GameEvents.RaiseAbilityReturned(id);
            stolen.Clear();
            foreach (var p in pods)
                if (p) Destroy(p.gameObject);
            pods.Clear();
        }

        Color Tint(AbilityId id)
        {
            if (id == AbilityId.None) return bitterColor;
            var data = Game.Config ? Game.Config.colorOrder.Find(c => c && c.grantedAbility == id) : null;
            return data ? data.tint : UnityEngine.Color.white;
        }

        // ---------- Covet ----------

        IEnumerator Covet(int phase)
        {
            CurrentState = State.Covet;
            float time = PerPhase(covetTimePerPhase, phase);
            var thread = Transient(FlatSprite.Create("CovetThread", null, transform.position, Vector2.one * 0.05f, UnityEngine.Color.clear, 9));

            for (float t = 0f; t < time && Player; t += Time.deltaTime)
            {
                // The thread shows what it's about to take, and changes if the player uses something else.
                float w = t / time;
                facing = FacingSign;
                windUp01 = w;
                glowTint = Tint(NextStealTarget());
                glowAmount = 0.15f + 0.5f * w;
                var c = glowTint;
                c.a = Mathf.Lerp(0.35f, 1f, w) * (0.75f + 0.25f * Mathf.Sin(t * Mathf.Lerp(10f, 40f, w)));
                thread.color = c;
                Stretch(thread, HeartPoint, Player.position, Mathf.Lerp(0.04f, 0.16f, w));
                if (Every(ref emitA, 0.12f))
                    Puff(Player.position, Random.insideUnitCircle * 1.5f, 0.15f, 0.05f, glowTint, 0.4f, 12);
                yield return null;
            }

            // Yank.
            windUp01 = 0f;
            glowAmount = 0f;
            DestroyTransient(thread.gameObject);
            if (!Player) yield break;
            Kick();
            ShakeCamera(0.12f, 0.2f);
            Steal(NextStealTarget(), Player.position);
            CurrentState = State.Rooted;
            yield return WaitOrInterrupt(PerPhase(restTimePerPhase, phase));
        }

        // ---------- Its own attacks ----------

        IEnumerator NativeAttack(int phase)
        {
            var rotation = Rotation[Mathf.Min(phase, Rotation.Length - 1)];
            switch (rotation[nativeCycle++ % rotation.Length])
            {
                case Attack.Lash: yield return Lash(phase); break;
                case Attack.Thorns: yield return Thorns(phase); break;
            }
        }

        IEnumerator Lash(int phase)
        {
            var heights = LashHeights[Mathf.Min(phase, LashHeights.Length - 1)];
            for (int i = 0; i < heights.Length && Player && !Interrupted; i++)
            {
                if (i > 0) yield return Wait(lashGap);
                int dir = FacingSign;
                float y = groundY + (heights[i] ? highLashHeight : lowLashHeight);
                float startX = transform.position.x + dir * halfWidth * 0.5f;
                float endX = dir > 0 ? floorMaxX : floorMinX;

                // Tell: the path lights up at the height it will come, and it rears back.
                CurrentState = State.LashWindUp;
                facing = dir;
                var warn = Transient(FlatSprite.Create("LashWarning", null, new Vector2((startX + endX) * 0.5f, y),
                    new Vector2(Mathf.Abs(endX - startX), 0.08f), lashWarnColor, 3));
                float warnTime = PerPhase(lashWarnTimePerPhase, phase);
                for (float t = 0f; t < warnTime && !Interrupted; t += Time.deltaTime)
                {
                    windUp01 = t / warnTime;
                    var c = lashWarnColor;
                    c.a *= 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 30f));
                    warn.color = c;
                    yield return null;
                }
                windUp01 = 0f;
                DestroyTransient(warn.gameObject);
                if (Interrupted) break; // a pod broke: it wilts instead of lashing

                // The whip races out to the wall, hangs there a moment, and snaps back.
                CurrentState = State.Lash;
                Kick();
                var vine = Transient(FlatSprite.Create("Lash", null, new Vector2(startX, y), new Vector2(0.01f, lashThickness), vineColor, 6));
                float length = Mathf.Abs(endX - startX), reach = 0f;
                bool hit = false;
                for (float held = 0f; held < lashHold;)
                {
                    if (reach < length) reach = Mathf.Min(length, reach + lashSpeed * Time.deltaTime);
                    else held += Time.deltaTime;
                    SetVine(vine, startX, dir, reach, y);
                    if (!hit) hit = LashHits(startX, dir, reach, y);
                    yield return null;
                }
                while (reach > 0f)
                {
                    reach = Mathf.Max(0f, reach - lashSpeed * 2f * Time.deltaTime);
                    SetVine(vine, startX, dir, reach, y);
                    yield return null;
                }
                DestroyTransient(vine.gameObject);
            }
            CurrentState = State.Rooted;
        }

        void SetVine(SpriteRenderer vine, float startX, int dir, float reach, float y)
        {
            vine.transform.position = new Vector2(startX + dir * reach * 0.5f, y);
            vine.transform.localScale = new Vector3(Mathf.Max(0.01f, reach), lashThickness, 1f);
        }

        /// <summary>Hurts the player if the vine reaches them, and takes something while there's room for a pod.</summary>
        bool LashHits(float startX, int dir, float reach, float y)
        {
            var pc = PlayerController.Instance;
            if (!pc || reach <= 0f) return false;
            var vine = new Bounds(new Vector3(startX + dir * reach * 0.5f, y), new Vector3(reach, lashThickness, 1f));
            bool touching = false;
            foreach (var c in pc.GetComponentsInChildren<Collider2D>())
            {
                if (c.isTrigger) continue;
                var b = c.bounds;
                b.center = new Vector3(b.center.x, b.center.y, 0f);
                if (b.Intersects(vine)) { touching = true; break; }
            }
            if (!touching) return false;

            var info = new DamageInfo(lashDamage, Team.Enemy, new Vector2(dir * lashKnockback, lashKnockback * 0.6f), gameObject);
            if (!pc.Health.TakeDamage(info)) return true; // dashed through it
            if (PodRoom)
            {
                var id = NextStealTarget();
                if (id != AbilityId.None) Steal(id, pc.transform.position);
            }
            return true;
        }

        IEnumerator Thorns(int phase)
        {
            if (!Player) yield break;
            int count = PerPhase(thornCountPerPhase, phase);
            float centerX = Player.position.x;
            Kick(down: true);
            for (int i = 0; i < count; i++)
            {
                float x = centerX + (i - (count - 1) * 0.5f) * thornSpacing;
                if (x - thornWidth * 0.5f < floorMinX || x + thornWidth * 0.5f > floorMaxX) continue;
                FirePatch.Spawn(x, groundY, thornWidth, thornHeight, thornWarnTime, thornTime, thornColor, thornTip);
                yield return Wait(thornStagger);
            }
            yield return Wait(thornWarnTime);
        }

        // ---------- The player's stolen abilities ----------

        bool PickStolen(out AbilityId id)
        {
            id = AbilityId.None;
            var usable = new List<AbilityId>();
            foreach (var s in stolen)
                if (loadout.Has(s) && (s != AbilityId.BlazeStrike || PlayerClose() || loadout.Has(AbilityId.Dash)))
                    usable.Add(s);
            if (usable.Count == 0) return false;
            if (usable.Count > 1) usable.Remove(lastStolenUse); // shows off something different each time
            id = lastStolenUse = usable[Random.Range(0, usable.Count)];
            return true;
        }

        IEnumerator UseStolen(AbilityId id, int phase)
        {
            switch (id)
            {
                case AbilityId.LightShot: yield return Volley(phase); break;
                case AbilityId.Dash: yield return DashAt(phase); break;
                case AbilityId.BlazeStrike: yield return BlazeStrike(); break;
            }
            CurrentState = State.Rooted;
        }

        IEnumerator Volley(int phase)
        {
            CurrentState = State.Volley;
            glowTint = Tint(AbilityId.LightShot);
            for (float t = 0f; t < volleyWindUp && !Interrupted; t += Time.deltaTime)
            {
                facing = FacingSign;
                windUp01 = t / volleyWindUp;
                glowAmount = 0.5f * windUp01;
                yield return null;
            }
            windUp01 = 0f;
            for (int i = 0, n = PerPhase(shotVolleyPerPhase, phase); i < n && loadout.Has(AbilityId.LightShot) && !Interrupted; i++)
            {
                if (i > 0) yield return Wait(shotInterval);
                facing = FacingSign;
                if (loadout.TryActivate(AbilityId.LightShot)) Kick();
            }
            glowAmount = 0f;
        }

        /// <summary>Pulls up its roots and uses the player's Dash on them, a few times. Puts down roots where it stops.</summary>
        IEnumerator DashAt(int phase)
        {
            CurrentState = State.Uprooted;
            for (float t = 0f; t < uprootTime; t += Time.deltaTime)
            {
                windUp01 = t / uprootTime;
                if (Every(ref emitA, 0.05f))
                    Puff(PosePoint(Random.Range(-1f, 1f), 0f), new Vector2(Random.Range(-2f, 2f), Random.Range(1f, 2.5f)), 0.25f, 0.6f, dirtColor, 0.5f, 9);
                yield return null;
            }
            windUp01 = 0f;

            for (int i = 0, n = PerPhase(dashesPerPhase, phase); i < n && Player && loadout.Has(AbilityId.Dash) && !Interrupted; i++)
            {
                if (i > 0) yield return Wait(dashGap);
                facing = FacingSign;
                float wall = facing > 0 ? floorMaxX - halfWidth : floorMinX + halfWidth;
                if (Mathf.Abs(wall - transform.position.x) < 1f) break; // backed into a wall

                if (!loadout.TryActivate(AbilityId.Dash)) continue;
                dashHitbox.Open(float.PositiveInfinity); // closed by hand once it stops
                Kick();
                while (MovementLocked) yield return new WaitForFixedUpdate(); // the Dash unlocks it when it's done
                if (Body) Body.linearVelocity = Vector2.zero;
                dashHitbox.gameObject.SetActive(false);
            }

            // Ends with a swipe if it has that too and the player's in reach.
            if (loadout.Has(AbilityId.BlazeStrike) && PlayerClose() && !Interrupted) yield return BlazeStrike();

            Land(0.5f);
            CurrentState = State.Rooted;
        }

        IEnumerator BlazeStrike()
        {
            if (!PlayerClose()) yield break;
            CurrentState = State.StrikeWindUp;
            glowTint = Tint(AbilityId.BlazeStrike);
            for (float t = 0f; t < strikeWindUp && !Interrupted; t += Time.deltaTime)
            {
                facing = FacingSign;
                windUp01 = t / strikeWindUp;
                glowAmount = 0.2f + 0.6f * windUp01;
                if (Every(ref emitB, 0.06f))
                    Puff(PosePoint(facing * 0.9f, 0.6f), new Vector2(Random.Range(-0.5f, 0.5f), 2f), 0.15f, 0.05f, glowTint, 0.35f, 12);
                yield return null;
            }
            windUp01 = 0f;
            glowAmount = 0f;
            if (!Interrupted && loadout.TryActivate(AbilityId.BlazeStrike))
            {
                Kick();
                ShakeCamera(0.1f, 0.15f);
                for (int i = 0; i < 6; i++)
                    Puff(PosePoint(facing * 1.6f, Random.Range(0.2f, 0.8f)), new Vector2(facing * Random.Range(2f, 5f), Random.Range(-1f, 1f)),
                        0.3f, 0.1f, Tint(AbilityId.BlazeStrike), 0.3f, 12);
            }
            yield return Wait(0.3f);
        }

        bool PlayerClose() => Player && Mathf.Abs(Player.position.x - transform.position.x) <= strikeRange
                                     && Mathf.Abs(Player.position.y - transform.position.y) <= halfHeight + 1.5f;

        Hitbox MakeDashHitbox()
        {
            var go = new GameObject("DashHitbox");
            go.SetActive(false); // Hitbox.Open() turns it on
            go.transform.SetParent(transform, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(halfWidth * 1.8f, halfHeight * 1.6f);
            var hitbox = go.AddComponent<Hitbox>();
            hitbox.team = Team.Enemy;
            hitbox.damage = dashDamage;
            hitbox.knockback = dashKnockback;
            return hitbox;
        }

        // ---------- Wilt & burrow ----------

        IEnumerator Wilt()
        {
            wiltRequested = false;
            wiltHits = 0;
            CurrentState = State.Wilted;
            onWilt.Invoke();
            for (int i = 0; i < 10; i++)
                Puff(PosePoint(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)), new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 1f)),
                    0.2f, 0.1f, wiltColor, 1f, 9);

            for (float t = 0f; t < wiltTime && wiltHits < maxHitsPerWilt && !rerootRequested; t += Time.deltaTime)
            {
                if (wiltRequested) { wiltRequested = false; t = 0f; wiltHits = 0; } // another pod broke: droops longer
                yield return null;
            }

            CurrentState = State.Recover;
            yield return Wait(recoverTime);
            CurrentState = State.Rooted;
            nextCovetAt = Time.time + PerPhase(covetDelayPerPhase, Phase);
        }

        /// <summary>Sinks into the floor and comes up at the root spot farthest from the player.</summary>
        IEnumerator Reroot()
        {
            rerootRequested = false;
            cycle = nativeCycle = 0;

            CurrentState = State.Burrow;
            for (float t = 0f; t < burrowTime; t += Time.deltaTime)
            {
                sink = t / burrowTime;
                if (Every(ref emitA, 0.05f))
                    Puff(new Vector2(transform.position.x + Random.Range(-halfWidth, halfWidth), groundY), new Vector2(Random.Range(-2f, 2f), Random.Range(1f, 3f)),
                        0.3f, 0.7f, dirtColor, 0.5f, 9);
                yield return null;
            }
            sink = 1f;

            float px = Player ? Player.position.x : 0f;
            float x = rootSpots.Length > 0 ? rootSpots[0] : transform.position.x;
            foreach (var spot in rootSpots)
                if (Mathf.Abs(spot - px) > Mathf.Abs(x - px)) x = spot;
            MoveTo(new Vector2(x, GroundedY));
            yield return Wait(0.3f);

            CurrentState = State.Emerge;
            ShakeCamera(0.15f, 0.3f);
            for (float t = 0f; t < burrowTime; t += Time.deltaTime)
            {
                sink = 1f - EaseOutBack(t / burrowTime);
                if (Every(ref emitA, 0.04f))
                    Puff(new Vector2(x + Random.Range(-halfWidth, halfWidth), groundY), new Vector2(Random.Range(-3f, 3f), Random.Range(1f, 4f)),
                        0.3f, 0.8f, dirtColor, 0.6f, 9);
                yield return null;
            }
            sink = 0f;
            Land(0.4f);
            CurrentState = State.Rooted;
            nextCovetAt = Time.time; // opens the new phase by taking something
        }

        // ---------- Helpers ----------

        IEnumerator WaitOrInterrupt(float seconds)
        {
            for (float t = 0f; t < seconds && !Interrupted; t += Time.deltaTime) yield return null;
        }

        void MoveTo(Vector2 position)
        {
            if (Body) Body.MovePosition(position);
            transform.position = position;
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
            sr.transform.localScale = new Vector3(d.magnitude, width, 1f);
        }

        // ---------- Animation ----------

        void Kick(bool down = false)
        {
            punch = down ? -1f : 1f;
            HoldAttackPose(attackPoseHold);
        }

        void Land(float squash)
        {
            landSquash = squash;
            ShakeCamera(0.08f, 0.2f);
            for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 3; i++)
                Puff(PosePoint(s * 0.8f, 0f), new Vector2(s * Random.Range(2f, 4f), Random.Range(0.3f, 1f)), 0.3f, 0.8f, dirtColor, 0.5f, 9);
        }

        void LateUpdate()
        {
            if (!pose) return;
            float dt = Time.deltaTime;
            animTime += dt;
            stateTime += dt;
            punch = Mathf.MoveTowards(punch, 0f, dt / 0.25f);
            jolt = Mathf.MoveTowards(jolt, 0f, dt / 0.2f);
            flash = Mathf.MoveTowards(flash, 0f, dt / 0.1f);
            landSquash = Mathf.MoveTowards(landSquash, 0f, dt / 0.35f);

            var squash = Vector2.one; // pivot at the feet
            var offset = Vector2.zero;
            float lean = 0f;          // degrees; + tips the top toward +x
            float tremble = 0f;
            var tint = glowTint;
            float tintAlpha = glowAmount;
            float heartOpen01 = 0f;

            switch (state)
            {
                case State.Rooted:
                {
                    // Swaying like something growing: slow, a little uneven.
                    float sway = Mathf.Sin(animTime * 1.3f) + 0.4f * Mathf.Sin(animTime * 2.9f);
                    lean = sway * 4f;
                    squash = new Vector2(1f - 0.03f * Mathf.Sin(animTime * 2.2f), 1f + 0.04f * Mathf.Sin(animTime * 2.2f));
                    if (Every(ref emitB, 1.2f))
                        Puff(PosePoint(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 1f)), new Vector2(Random.Range(-0.6f, 0.6f), -0.4f), 0.15f, 0.1f, leafColor, 1.5f, 9);
                    break;
                }
                case State.Covet:
                    // Leans out toward the player, stretching for it, shaking with want.
                    lean = facing * 18f * windUp01;
                    squash = new Vector2(1f - 0.12f * windUp01, 1f + 0.18f * windUp01);
                    tremble = 2f + 8f * windUp01;
                    break;
                case State.LashWindUp:
                case State.Volley:
                case State.StrikeWindUp:
                    // Rears back and coils before it lets go.
                    lean = -facing * 14f * windUp01;
                    squash = Vector2.one * (1f + 0.15f * windUp01);
                    tremble = 1f + 5f * windUp01;
                    break;
                case State.Lash:
                    lean = facing * 14f;
                    squash = new Vector2(1.1f, 0.92f);
                    break;
                case State.Uprooted:
                {
                    // Roots up out of the ground, then leans hard into the dash.
                    float vx = smoothVelocity.x;
                    offset.y = 0.25f * windUp01 + (Mathf.Abs(vx) > 1f ? 0.25f : 0f);
                    lean = Mathf.Clamp(vx * 1.5f, -25f, 25f);
                    float stretch = Mathf.Min(Mathf.Abs(vx) * 0.015f, 0.3f);
                    squash = new Vector2(1f + stretch, 1f - stretch);
                    tremble = 3f + 4f * windUp01;
                    if (Mathf.Abs(vx) > 1f && Every(ref emitA, 0.03f))
                        Puff(PosePoint(-Mathf.Sign(vx), 0.3f), new Vector2(-Mathf.Sign(vx) * 2f, 0.5f), 0.4f, 0.1f, Tint(AbilityId.Dash), 0.3f, 7);
                    break;
                }
                case State.Wilted:
                {
                    // Droops over, brown and limp, the heart split open and blinking: "hit me now".
                    float w = Mathf.Clamp01(stateTime / 0.3f);
                    float stir = Mathf.Clamp01(1f - (wiltTime - stateTime));
                    squash = Vector2.Lerp(Vector2.one, new Vector2(1.15f, 0.72f), w);
                    lean = -facing * (24f - 6f * stir) * w + Mathf.Sin(stateTime * 3f) * 2f;
                    tremble = 6f * stir;
                    tint = wiltColor;
                    tintAlpha = 0.55f * w;
                    heartOpen01 = Mathf.Repeat(stateTime, 0.5f) < 0.1f ? 1f : 0.75f;
                    break;
                }
                case State.Recover:
                {
                    float w = Mathf.Clamp01(stateTime / recoverTime);
                    squash = Vector2.LerpUnclamped(new Vector2(1.15f, 0.72f), Vector2.one, EaseOutBack(w));
                    lean = -facing * 24f * (1f - w);
                    tint = wiltColor;
                    tintAlpha = 0.55f * (1f - w);
                    tremble = 8f * (1f - w);
                    break;
                }
                case State.Burrow:
                case State.Emerge:
                    tremble = 6f;
                    squash = new Vector2(1f + 0.15f * sink, 1f - 0.1f * sink);
                    break;
                case State.Defeated:
                {
                    float w = Mathf.Clamp01(stateTime / 1.2f);
                    squash = Vector2.Lerp(new Vector2(1.15f, 0.72f), new Vector2(1.35f, 0.45f), w);
                    lean = -facing * 28f;
                    tint = wiltColor;
                    tintAlpha = 0.6f + 0.3f * w;
                    if (Every(ref emitA, 0.2f))
                        Puff(PosePoint(Random.Range(-0.8f, 0.8f), 0.5f), new Vector2(Random.Range(-0.5f, 0.5f), -0.5f), 0.2f, 0.1f, wiltColor, 1.5f, 9);
                    break;
                }
            }

            // Impulses on top of whatever stance it's in.
            if (punch > 0f) // lashing out
            {
                squash.x *= 1f + 0.2f * punch;
                squash.y *= 1f - 0.12f * punch;
                lean += facing * 10f * punch;
            }
            else if (punch < 0f) // stamping its roots
            {
                squash.x *= 1f - 0.12f * punch;
                squash.y *= 1f + 0.2f * punch;
            }
            squash.x *= 1f + 0.3f * landSquash;
            squash.y *= 1f - 0.3f * landSquash;
            tremble += 10f * jolt;
            offset.y -= sink * halfHeight * 2.1f;

            float wobble = (Mathf.PerlinNoise(animTime * 16f, 0.71f) - 0.5f) * 2f * tremble;
            pose.localPosition = poseBasePos + (Vector3)offset;
            pose.localRotation = Quaternion.Euler(0f, 0f, -(lean + wobble));
            pose.localScale = Vector3.Scale(poseBaseScale, new Vector3(squash.x, squash.y, 1f));

            if (glow)
            {
                glow.sprite = glowSource.sprite; // follows BossSprites' pose swaps
                if (flash > 0f) { tint = UnityEngine.Color.white; tintAlpha = Mathf.Max(tintAlpha, 0.8f * flash); }
                glow.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(tintAlpha));
            }
            if (heart)
            {
                heart.color = UnityEngine.Color.Lerp(heartClosed, heartOpen, heartOpen01);
                heart.transform.localScale = Vector3.one * (0.4f + 0.25f * heartOpen01) / poseBaseScale.x;
            }
        }

        /// <summary>A point on the body in world space: x from -1 (left edge) to 1, y from 0 (feet) to 1 (top).</summary>
        Vector2 PosePoint(float x, float y) =>
            pose ? pose.TransformPoint(new Vector3(x * halfWidth / poseBaseScale.x, y * halfHeight * 2f / poseBaseScale.y, 0f))
                 : (Vector2)transform.position + new Vector2(x * halfWidth, (y - 0.5f) * halfHeight * 2f);

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
