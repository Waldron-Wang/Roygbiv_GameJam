using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE — excitement, youth. Auto-scrolling chase (think Chrome dino game), run by ChaseDirector
    /// (speed) and ChaseCourse (the endless track).
    ///
    /// The boss holds a spot `lead` units right of the screen center, swaying forward and back (wider and
    /// faster each phase), and hops along over the obstacles. It can't be hurt: drop a CageTrap on it
    /// (see CageTrap) to trap it, then run into it to "catch" it (1 damage). After a catch, or if the trap
    /// runs out, it dashes back to its spot, passing through the player. 3 HP = 3 catches = 3 phases.
    ///
    /// Attacks (held while a cage latch is coming up, so the player can focus on the shot):
    ///   Phase 0: none, it just runs.
    ///   Phase 1: firecrackers lobbed back at where the player will be (marked on the ground).
    ///            Jump them, shoot them down, or punch them back.
    ///   Phase 2: more firecrackers, plus barrels rolled back along the ground.
    /// Reward: Dash.
    /// </summary>
    [DefaultExecutionOrder(100)] // LateUpdate after CameraFollow's, so it reads this frame's scroll
    public class OrangeBoss : BossBase
    {
        [Header("Chase position")]
        [Tooltip("Where it runs: this far right of the screen center. The player's home is CameraFollow.autoScrollLead left of center.")]
        [SerializeField] float lead = 4f;
        [Tooltip("Forward/back sway around its spot, per phase (world units).")]
        [SerializeField] float[] swayPerPhase = { 1f, 1.5f, 2f };
        [Tooltip("Seconds per sway, per phase.")]
        [SerializeField] float[] swayPeriodPerPhase = { 3.2f, 2.6f, 2f };
        [Tooltip("How fast it slides toward its spot (units/s relative to the scroll).")]
        [SerializeField] float followRate = 8f;
        [Tooltip("How fast it dashes back after a catch or a trap.")]
        [SerializeField] float rejoinRate = 12f;
        [SerializeField] float hopHeight = 0.8f;
        [SerializeField] float hopTime = 0.45f;

        [Header("Trap")]
        [SerializeField] float trapStunTime = 2.5f;

        [Header("Attacks")]
        [SerializeField] Projectile firecrackerPrefab; // shootable + reflectable (homes on reflect) recommended
        [SerializeField] Projectile barrelPrefab;      // speed = world roll speed; destroyOnWorld off
        [Tooltip("Seconds between attacks, per phase. 0 = no attacks.")]
        [SerializeField] float[] attackIntervalPerPhase = { 0f, 2.8f, 2.2f };
        [SerializeField] float windUpTime = 0.5f;
        [SerializeField] UnityEvent onWindUp = new();
        [SerializeField] int[] firecrackersPerPhase = { 0, 1, 2 };
        [Tooltip("How high above the boss each firecracker climbs. Higher = longer in the air = more time to react.")]
        [SerializeField] float firecrackerApexHeight = 4f;
        [SerializeField] float firecrackerGravityScale = 1.5f;
        [SerializeField] float firecrackerStagger = 0.35f;
        [Tooltip("Each firecracker lands this far (random in [x, y]) from where the player will be.")]
        [SerializeField] Vector2 firecrackerScatter = new(-0.5f, 1.5f);
        [SerializeField] Color landingMarkerColor = new(1f, 0.4f, 0.1f, 0.8f);
        [SerializeField] int barrelMinPhase = 2;
        [Tooltip("World speed the barrel rolls back at (the player closes in at this + the scroll speed).")]
        [SerializeField] float barrelRollSpeed = 1.5f;
        [SerializeField, Range(0f, 1f)] float barrelChance = 0.4f;

        [Tooltip("Placeholder telegraph squashes this transform. Defaults to the child named Visual.")]
        [SerializeField] Transform visual;

        Collider2D bodyCollider;
        CageTrap trap;
        Vector3 visualScale;
        float stunnedUntil, currentLead, swayAngle, hopClock, halfHeight = 1f;
        bool rejoining;

        public bool IsStunned => Time.time < stunnedUntil;
        /// <summary>Art: always running in the chase (no attack sprite); dazed while caged.</summary>
        public override BossPose Pose => IsStunned ? BossPose.Hurt : IsFighting ? BossPose.Move : BossPose.Idle;
        public float Lead => lead;
        ChaseDirector Chase => ChaseDirector.Current;

        bool CanAttack => IsFighting && !IsStunned && !rejoining && Chase && Player
                          && !(ChaseCourse.Current && ChaseCourse.Current.CageInPlay);

        protected override void Awake()
        {
            base.Awake();
            bodyCollider = GetComponent<Collider2D>();
            // Cached: bounds read empty while the collider is switched off (dashing back).
            if (bodyCollider is BoxCollider2D box) halfHeight = box.size.y * 0.5f * transform.lossyScale.y;
            if (!visual) visual = transform.Find("Visual");
            if (visual) visualScale = visual.localScale;
        }

        /// <summary>Stops it in place for a while (kept for switches wired in scenes; CageTrap uses TryTrap).</summary>
        public void Stun(float seconds)
        {
            if (IsFighting) stunnedUntil = Time.time + seconds;
        }

        /// <summary>A cage landed at `cage.X`: trapped if it's close enough and not already busy.</summary>
        public bool TryTrap(CageTrap cage, float radius)
        {
            if (!IsFighting || IsStunned || rejoining) return false;
            if (Mathf.Abs(transform.position.x - cage.X) > radius) return false;
            trap = cage;
            Stun(trapStunTime);
            return true;
        }

        protected override void OnFightStarted()
        {
            Health.Invulnerable = true; // can't be shot to death — only caught
            if (Body) Body.linearVelocity = Vector2.zero;
            currentLead = lead;
        }

        protected override IEnumerator RunPhase(int phase)
        {
            float interval = PerPhase(attackIntervalPerPhase, phase);
            if (interval <= 0f) { yield return Wait(0.5f); yield break; }

            yield return Wait(interval);
            // Held (cage shot coming up, trapped, dashing back)? Attack as soon as it clears, not a whole cycle later.
            yield return new WaitUntil(() => CanAttack || !IsFighting || Phase != phase);
            if (!CanAttack || Phase != phase) yield break;

            bool barrel = barrelPrefab && phase >= barrelMinPhase && Random.value < barrelChance;
            onWindUp.Invoke();
            if (visual) visual.localScale = new Vector3(visualScale.x * 1.15f, visualScale.y * 0.8f, visualScale.z); // crouch
            yield return Wait(windUpTime);
            if (visual) visual.localScale = visualScale;
            if (!CanAttack) yield break;

            if (barrel) RollBarrel();
            else yield return Firecrackers(PerPhase(firecrackersPerPhase, phase));
        }

        protected override void OnDefeated()
        {
            if (visual) visual.localScale = visualScale;
            if (trap) trap.Shatter();
        }

        void LateUpdate()
        {
            if (!IsFighting || !Chase) return;
            float dt = Time.deltaTime;
            float groundedY = Chase.GroundY + halfHeight;
            var pos = transform.position;

            if (IsStunned)
            {
                pos.y = groundedY; // stays put in the world while the screen scrolls on...
                currentLead = pos.x - Chase.ScrollX; // ...so its spot relative to the screen slides back
            }
            else
            {
                if (trap) { trap.Shatter(); trap = null; } // broke out before being caught

                int phase = Phase;
                swayAngle += dt * Mathf.PI * 2f / PerPhase(swayPeriodPerPhase, phase);
                float target = lead + PerPhase(swayPerPhase, phase) * Mathf.Sin(swayAngle);

                // currentLead is relative to the screen, so the scroll itself never drags it back,
                // however fast the chase gets. followRate / rejoinRate are on top of the scroll.
                rejoining = currentLead < target - 1f;
                currentLead = Mathf.MoveTowards(currentLead, target, (rejoining ? rejoinRate : followRate) * dt);
                pos.x = Chase.ScrollX + currentLead;

                hopClock += dt;
                pos.y = groundedY + hopHeight * Mathf.Abs(Mathf.Sin(hopClock * Mathf.PI / hopTime));
            }

            // Dashing back, it passes through the player instead of shoving them.
            if (bodyCollider) bodyCollider.enabled = IsStunned || !rejoining;
            transform.position = pos;
        }

        // ---------- Attacks ----------

        IEnumerator Firecrackers(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!CanAttack || !firecrackerPrefab) yield break;
                Lob();
                yield return Wait(firecrackerStagger);
            }
        }

        /// <summary>
        /// Lobs a firecracker up to firecrackerApexHeight and down onto where the player will be when it lands
        /// (they can't change pace, only jump). The height sets the flight time, and so the reaction time.
        /// </summary>
        void Lob()
        {
            var from = (Vector2)transform.position + Vector2.up * halfHeight;
            float g = -Physics2D.gravity.y * firecrackerGravityScale;
            float up = Mathf.Sqrt(2f * g * firecrackerApexHeight);
            float fall = firecrackerApexHeight + from.y - Chase.GroundY;
            float flightTime = up / g + Mathf.Sqrt(2f * fall / g);

            float landX = Player.position.x + Chase.Speed * flightTime + Random.Range(firecrackerScatter.x, firecrackerScatter.y);
            var target = new Vector2(landX, Chase.GroundY);
            var velocity = new Vector2((landX - from.x) / flightTime, up);
            Fire(firecrackerPrefab, velocity, from).Arc(velocity, firecrackerGravityScale);

            var marker = FlatSprite.Create("LandingMarker", null, target + Vector2.up * 0.06f, new Vector2(1f, 0.12f), landingMarkerColor, 15);
            Destroy(marker.gameObject, flightTime);
        }

        /// <summary>Drops a barrel at its back heel (it lands on the ground right there), rolling back toward the player.</summary>
        void RollBarrel()
        {
            float radius = barrelPrefab.transform.localScale.y * 0.5f;
            float backHeel = bodyCollider ? bodyCollider.bounds.extents.x * 0.5f : 0.5f;
            var from = new Vector2(transform.position.x - backHeel, Chase.GroundY + radius);
            var barrel = Fire(barrelPrefab, Vector2.left, from);
            barrel.ScaleSpeed(barrelRollSpeed / barrel.speed);
        }

        // ---------- Catching ----------

        // Stay as well as Enter: the player may already be pressed against the boss when it gets trapped.
        void OnCollisionEnter2D(Collision2D c) => TryCatch(c);
        void OnCollisionStay2D(Collision2D c) => TryCatch(c);

        void TryCatch(Collision2D c)
        {
            if (!IsStunned || !c.collider.GetComponentInParent<PlayerController>()) return;

            stunnedUntil = 0f;
            if (trap) { trap.Shatter(); trap = null; }
            Health.Invulnerable = false;
            Health.TakeDamage(new DamageInfo(1, Team.Player));
            Health.Invulnerable = true;
        }
    }
}
