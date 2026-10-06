using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Blue's reward. Hold Down + press Dash: a surfboard. Shares the dash button with Dash; AbilityLoadout decides
    /// who gets each press (Down held + this unlocked = this one, always).
    ///
    ///   DIVE  (used in the air): shoots diagonally down and forward, gravity off, until it touches the ground.
    ///         A wall cancels it cleanly. Landing sends out a small shockwave.
    ///   SURF  (on landing, or right away on the ground): a low, fast slide that eases from surfStartSpeed down to
    ///         run speed. The body collider drops to lowHeight of its height (anchored at the feet), so the player
    ///         fits under low gaps and beams. Steer a little, never reverse; walls end it.
    ///         Jump while surfing = SURF JUMP: the momentum carries into the jump, plus a small boost.
    ///   LOW   after the surf, while there's a ceiling overhead or Down is still held: a slow slide, still low.
    ///         The body only stands back up where there's room, so it can never be pushed into geometry.
    /// Not invulnerable (that's Dash's thing): its value is the low profile and the speed.
    ///
    /// Works for any IActor (AI: TryActivate). The player path only reads PlayerIntent, so Indigo's input
    /// modifiers apply to it like to everything else. If it's revoked mid-move the move still finishes.
    /// </summary>
    public class DownDashAbility : AbilityBase
    {
        enum Move { Idle, Dive, Surf, SurfJump, LowSlide }

        [Header("Input")]
        [Tooltip("How far down the stick / keys must point for the dash button to mean Down Dash.")]
        [SerializeField, Range(0.1f, 0.95f)] float downThreshold = 0.5f;

        [Header("Dive (in the air)")]
        [Tooltip("Dive speed, units per second.")]
        [SerializeField] float diveSpeed = 22f;
        [Tooltip("Degrees below horizontal, toward the facing direction.")]
        [SerializeField, Range(10f, 89f)] float diveAngle = 55f;
        [Tooltip("The dive gives up after this long without touching the ground.")]
        [SerializeField] float maxDiveTime = 0.6f;
        [Tooltip("Damage of the little shockwave when a dive lands. 0 = none.")]
        [SerializeField] int landingShockDamage = 1;
        [Tooltip("Size of the landing shockwave, centered on the feet.")]
        [SerializeField] Vector2 landingShockSize = new(2.4f, 0.9f);
        [Tooltip("Knockback of the landing shockwave.")]
        [SerializeField] float landingShockKnockback = 6f;

        [Header("Surf (on the ground)")]
        [Tooltip("Speed the surf starts at.")]
        [SerializeField] float surfStartSpeed = 16f;
        [Tooltip("Speed the surf eases down to: the normal run speed (PlayerMotor.runSpeed on the player).")]
        [SerializeField] float surfEndSpeed = 10f;
        [Tooltip("Seconds to ease from the start speed to the end speed. The surf ends then.")]
        [SerializeField] float surfTime = 0.55f;
        [Tooltip("How much holding left / right speeds the surf up or slows it down (units per second). It never reverses.")]
        [SerializeField] float steerSpeed = 2.5f;
        [Tooltip("Body collider height while low, as a fraction of its normal height. The feet stay put.")]
        [SerializeField, Range(0.2f, 0.9f)] float lowHeight = 0.45f;
        [Tooltip("Seconds off the ground before a surf counts as having left the floor (small bumps don't end it).")]
        [SerializeField] float groundGrace = 0.1f;

        [Header("Surf jump")]
        [Tooltip("Upward speed of a jump out of a surf (the normal jump is 13).")]
        [SerializeField] float surfJumpVelocity = 13f;
        [Tooltip("Added to the surf's speed when jumping out of it.")]
        [SerializeField] float surfJumpBoost = 2.5f;
        [Tooltip("Seconds the surf's horizontal speed is held after the jump, before normal air control takes over.")]
        [SerializeField] float surfJumpCarryTime = 0.35f;

        [Header("Staying low")]
        [Tooltip("Speed of the slow slide while low after a surf (under a ceiling, or holding Down).")]
        [SerializeField] float lowSlideSpeed = 4.5f;
        [Tooltip("Keep holding Down after a surf to stay low (slide under long beams).")]
        [SerializeField] bool holdDownToStayLow = true;

        [Header("Look")]
        [Tooltip("Visual scale while surfing: wide and flat.")]
        [SerializeField] Vector2 surfSquash = new(1.3f, 0.55f);
        [Tooltip("Degrees the visual leans forward while surfing.")]
        [SerializeField] float surfTilt = 14f;
        [Tooltip("Degrees the visual leans into the dive.")]
        [SerializeField] float diveTilt = 35f;
        [Tooltip("How fast the visual squashes in and springs back (higher = snappier).")]
        [SerializeField] float lookSharpness = 20f;
        [Tooltip("Afterimage tint during the dive. Uses the DashAfterImage on the owner, if any.")]
        [SerializeField] Color diveAfterimageTint = new(0.35f, 0.65f, 1f, 0.55f);
        [Tooltip("Spray thrown up behind the feet while surfing.")]
        [SerializeField] Color wakeColor = new(0.45f, 0.75f, 1f, 0.75f);
        [Tooltip("Seconds between spray puffs while surfing (the slow slide sprays half as often).")]
        [SerializeField] float wakeInterval = 0.03f;

        public override AbilityId Id => AbilityId.DownDash;

        readonly List<ContactPoint2D> contacts = new();
        readonly List<Collider2D> overlaps = new();
        readonly HashSet<IDamageable> shocked = new();
        ContactFilter2D solids;

        Move move;
        Coroutine routine;
        int generation;
        int dir = 1;
        float baseGravity = 1f;
        bool low, jumpQueued, downHeld;
        float steerInput, nextWake;

        BoxCollider2D box;
        Vector2 boxSize, boxOffset;
        Transform visual;
        Vector3 visualPos, visualScale;
        Quaternion visualRotation;
        float squashAmount, diveAmount;
        bool visualTouched;
        DashAfterImage afterimage;

        /// <summary>In the middle of a dive, surf or surf jump: it owns the body's velocity right now.</summary>
        public bool IsBusy => move is Move.Dive or Move.Surf or Move.SurfJump;
        /// <summary>Doing anything at all, the slow low slide included.</summary>
        public bool IsActive => move != Move.Idle;
        /// <summary>The body collider is in its low shape.</summary>
        public bool IsLow => low;

        /// <summary>True when this intent holds Down far enough for the dash button to mean Down Dash.</summary>
        public bool IsDownHeld(in PlayerIntent intent) => intent.move.y < -downThreshold;

        void Reset() => cooldown = 0.6f;

        protected override void Awake()
        {
            base.Awake();
            solids = ContactFilter2D.noFilter;
            solids.useTriggers = false;
            // Read from the Rigidbody itself: the owner's Body property may not be set up yet this early.
            var rb = GetComponentInParent<Rigidbody2D>();
            if (rb) baseGravity = rb.gravityScale;
        }

        void OnDisable() => RestoreVisual();

        // ---------- Input ----------

        public override bool HandleInput(in PlayerIntent intent)
        {
            steerInput = intent.move.x;
            downHeld = IsDownHeld(intent);
            if (intent.jumpPressed && move is Move.Surf or Move.LowSlide) jumpQueued = true;

            if (!intent.dashPressed || !downHeld) return false;
            if (IsBusy) return true; // the press is ours, but a dive / surf is already running
            return TryActivate();
        }

        protected override void Activate()
        {
            if (IsBusy || Owner == null || !Owner.Body) return;
            FindParts();
            generation++; // a slow slide still running bows out
            if (routine != null) StopCoroutine(routine);
            dir = Owner.FacingSign >= 0 ? 1 : -1;
            jumpQueued = false;
            routine = StartCoroutine(Run(generation, !OnGround()));
        }

        /// <summary>
        /// Stops the move now and hands the body back (a Dash fired mid-surf, a knockback that must win).
        /// The body stays low until there's room to stand.
        /// </summary>
        public void Cancel()
        {
            if (move == Move.Idle) return;
            generation++;
            if (routine != null) StopCoroutine(routine);
            Release(true);
            routine = low ? StartCoroutine(StandWhenRoom(generation)) : null;
        }

        // ---------- The move ----------
        // Each step re-checks its generation after every physics step. Activate / Cancel bump it, so a step that
        // outlives a restart (a nested coroutine isn't always stopped with its parent) quits on its own.

        IEnumerator Run(int gen, bool inAir)
        {
            if (inAir)
            {
                bool landed = false;
                yield return Dive(gen, result => landed = result);
                if (gen != generation) yield break;
                if (!landed)
                {
                    Release(!Dead);
                    if (low) yield return StandWhenRoom(gen);
                    yield break;
                }
                LandingShock();
            }

            bool endedInAir = false;
            yield return Surf(gen, result => endedInAir = result);
            if (gen != generation) yield break;
            if (Dead) { Release(false); yield break; }

            if (move == Move.SurfJump) yield return SurfJump(gen);
            else if (!endedInAir && low && (!HasHeadroom() || WantsLow)) yield return LowSlide(gen);
            if (gen != generation) yield break;

            Release(!Dead);
            if (low) yield return StandWhenRoom(gen);
        }

        IEnumerator Dive(int gen, System.Action<bool> landed)
        {
            move = Move.Dive;
            var body = Owner.Body;
            Owner.MovementLocked = true;
            body.gravityScale = 0f;
            float rad = diveAngle * Mathf.Deg2Rad;
            var velocity = new Vector2(dir * Mathf.Cos(rad), -Mathf.Sin(rad)) * diveSpeed;
            if (afterimage) afterimage.Play(maxDiveTime, diveAfterimageTint);

            bool touchedDown = false;
            for (float t = 0f; t < maxDiveTime;)
            {
                Owner.MovementLocked = true; // a Dash ending mid-move would hand the body back
                body.linearVelocity = velocity;
                yield return new WaitForFixedUpdate();
                if (gen != generation) yield break;
                t += Time.fixedDeltaTime;
                if (Dead) break;
                if (OnGround()) { touchedDown = true; break; }
                if (WallAhead()) break; // cancel cleanly: drop from here
            }
            if (afterimage) afterimage.StopTrail();
            body.gravityScale = baseGravity;
            if (touchedDown) landed(true);
            else body.linearVelocity = new Vector2(body.linearVelocity.x * 0.2f, Mathf.Min(0f, body.linearVelocity.y * 0.5f));
        }

        /// <param name="endedInAir">Reports true if the surf ran off the floor.</param>
        IEnumerator Surf(int gen, System.Action<bool> endedInAir)
        {
            move = Move.Surf;
            var body = Owner.Body;
            Owner.MovementLocked = true;
            body.gravityScale = baseGravity;
            SetLow(true);

            float airTime = 0f;
            for (float t = 0f; t < surfTime;)
            {
                float u = t / Mathf.Max(0.01f, surfTime);
                float speed = Mathf.Lerp(surfStartSpeed, surfEndSpeed, 1f - (1f - u) * (1f - u)); // eases out
                speed = Mathf.Max(surfEndSpeed * 0.5f, speed + Mathf.Clamp(steerInput * dir, -1f, 1f) * steerSpeed);
                Owner.MovementLocked = true;
                body.linearVelocity = new Vector2(dir * speed, body.linearVelocity.y);

                yield return new WaitForFixedUpdate();
                if (gen != generation) yield break;
                t += Time.fixedDeltaTime;
                if (Dead) yield break;

                if (jumpQueued)
                {
                    jumpQueued = false;
                    move = Move.SurfJump;
                    body.linearVelocity = new Vector2(dir * (speed + surfJumpBoost), surfJumpVelocity);
                    yield break;
                }
                if (WallAhead())
                {
                    body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                    yield break;
                }
                airTime = OnGround() ? 0f : airTime + Time.fixedDeltaTime;
                if (airTime > groundGrace) { endedInAir(true); yield break; } // off a ledge: keep the momentum
            }
        }

        IEnumerator SurfJump(int gen)
        {
            var body = Owner.Body;
            if (low && HasHeadroom()) SetLow(false);
            float vx = body.linearVelocity.x;
            for (float t = 0f; t < surfJumpCarryTime;)
            {
                yield return new WaitForFixedUpdate();
                if (gen != generation) yield break;
                t += Time.fixedDeltaTime;
                if (Dead || WallAhead() || (t > 0.06f && OnGround())) yield break;
                Owner.MovementLocked = true;
                body.linearVelocity = new Vector2(vx, body.linearVelocity.y); // gravity still pulls; a double jump still works
                if (low && HasHeadroom()) SetLow(false);
            }
        }

        IEnumerator LowSlide(int gen)
        {
            move = Move.LowSlide;
            var body = Owner.Body;
            float airTime = 0f;
            while (low)
            {
                if (Dead) yield break;
                bool room = HasHeadroom();
                if (room && jumpQueued)
                {
                    // Jumping out of the slow slide is a plain jump.
                    SetLow(false);
                    body.linearVelocity = new Vector2(body.linearVelocity.x, surfJumpVelocity);
                    yield break;
                }
                jumpQueued = false;
                if (room && !WantsLow) { SetLow(false); yield break; }
                if (!room && WallAhead()) dir = -dir; // walled in under a ceiling: slide back out
                Owner.MovementLocked = true;
                body.linearVelocity = new Vector2(dir * lowSlideSpeed, body.linearVelocity.y);

                yield return new WaitForFixedUpdate();
                if (gen != generation) yield break;
                airTime = OnGround() ? 0f : airTime + Time.fixedDeltaTime;
                if (airTime > groundGrace) yield break; // slid off an edge: fall normally, stand once there's room
            }
        }

        /// <summary>Low but not moving on its own: stands up the moment the full body fits again.</summary>
        IEnumerator StandWhenRoom(int gen)
        {
            while (low && gen == generation)
            {
                if (HasHeadroom()) { SetLow(false); break; }
                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>Gives the body back to its owner's own movement code.</summary>
        void Release(bool unlock)
        {
            if (afterimage) afterimage.StopTrail();
            if (Owner != null && Owner.Body)
            {
                Owner.Body.gravityScale = baseGravity;
                if (unlock) Owner.MovementLocked = false;
            }
            move = Move.Idle;
            jumpQueued = false;
        }

        bool Dead => Owner.Health && Owner.Health.IsDead;
        bool WantsLow => holdDownToStayLow && downHeld && enabled;

        // ---------- Landing shockwave ----------

        void LandingShock()
        {
            Vector2 feet = FeetPoint();
            for (int i = 0; i < 6; i++)
                HeatPuff.Spawn(feet + new Vector2(Random.Range(-0.6f, 0.6f), 0.05f), new Vector2(Random.Range(-4f, 4f), Random.Range(0.5f, 2f)),
                    0.25f, 0.6f, wakeColor, 0.4f, 12);
            if (landingShockDamage <= 0) return;

            shocked.Clear();
            Physics2D.OverlapBox(feet + Vector2.up * landingShockSize.y * 0.5f, landingShockSize, 0f, solids, overlaps);
            foreach (var c in overlaps)
            {
                if (!c || c.transform.IsChildOf(Owner.Root)) continue;
                var target = c.GetComponentInParent<IDamageable>();
                if (target == null || !shocked.Add(target) || !Combat.CanHurt(Owner.Team, target.Team)) continue;
                float side = Mathf.Sign(c.transform.position.x - feet.x);
                target.TakeDamage(new DamageInfo(landingShockDamage, Owner.Team, new Vector2(side * landingShockKnockback, landingShockKnockback * 0.5f), gameObject));
            }
        }

        // ---------- Body shape ----------

        void FindParts()
        {
            if (!box && Owner.Body)
            {
                foreach (var b in Owner.Body.GetComponents<BoxCollider2D>())
                    if (!b.isTrigger) { box = b; break; }
                if (box) { boxSize = box.size; boxOffset = box.offset; }
            }
            if (!visual && Owner.Root)
            {
                visual = Owner.Root.Find("Visual");
                if (visual) { visualPos = visual.localPosition; visualScale = visual.localScale; visualRotation = visual.localRotation; }
            }
            if (!afterimage)
            {
                afterimage = GetComponentInChildren<DashAfterImage>();
                if (!afterimage) afterimage = GetComponentInParent<DashAfterImage>();
            }
        }

        /// <summary>Shrinks the body collider to lowHeight of its height (or back), keeping the feet where they are.</summary>
        void SetLow(bool value)
        {
            if (low == value) return;
            low = value;
            if (!box) return;
            if (value)
            {
                float h = boxSize.y * lowHeight;
                box.size = new Vector2(boxSize.x, h);
                box.offset = new Vector2(boxOffset.x, boxOffset.y - (boxSize.y - h) * 0.5f);
            }
            else
            {
                box.size = boxSize;
                box.offset = boxOffset;
            }
        }

        /// <summary>Would the full-height body fit where the feet are now? Touching floor and walls doesn't count.</summary>
        bool HasHeadroom()
        {
            if (!box) return true;
            var t = box.transform;
            Vector2 scale = new(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
            Vector2 center = t.TransformPoint(boxOffset);
            Vector2 size = Vector2.Scale(boxSize, scale) - new Vector2(0.1f, 0.1f);
            Physics2D.OverlapBox(center, size, 0f, solids, overlaps);
            foreach (var c in overlaps)
                if (c && c != box && !c.transform.IsChildOf(Owner.Root) && !Physics2D.GetIgnoreCollision(c, box)) return false;
            return true;
        }

        Vector2 FeetPoint()
        {
            if (!box) return Owner.Root.position;
            var t = box.transform;
            return t.TransformPoint(new Vector2(boxOffset.x, boxOffset.y - boxSize.y * 0.5f));
        }

        bool OnGround()
        {
            if (Owner.IsGrounded) return true; // bosses answer this themselves; the player's motor checks contacts
            int n = Owner.Body.GetContacts(solids, contacts);
            for (int i = 0; i < n; i++)
                if (contacts[i].normal.y > 0.6f) return true;
            return false;
        }

        bool WallAhead()
        {
            int n = Owner.Body.GetContacts(solids, contacts);
            for (int i = 0; i < n; i++)
                if (contacts[i].normal.x * dir < -0.7f) return true;
            return false;
        }

        // ---------- Look ----------

        void LateUpdate()
        {
            if (!visual) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float k = 1f - Mathf.Exp(-lookSharpness * dt);
            squashAmount = Mathf.Lerp(squashAmount, low || move is Move.Surf or Move.LowSlide ? 1f : 0f, k);
            diveAmount = Mathf.Lerp(diveAmount, move == Move.Dive ? 1f : 0f, k);
            if (squashAmount < 0.002f && diveAmount < 0.002f && move == Move.Idle && !low)
            {
                RestoreVisual();
                return;
            }

            // Squash and lean around the feet, so the body hugs the floor instead of shrinking in mid-air.
            float feetY = box ? boxOffset.y - boxSize.y * 0.5f : visualPos.y - 0.5f;
            float sx = Mathf.Lerp(1f, surfSquash.x, squashAmount) * Mathf.Lerp(1f, 0.85f, diveAmount);
            float sy = Mathf.Lerp(1f, surfSquash.y, squashAmount) * Mathf.Lerp(1f, 1.15f, diveAmount);
            visual.localScale = new Vector3(visualScale.x * sx, visualScale.y * sy, visualScale.z);
            visual.localPosition = new Vector3(visualPos.x, feetY + (visualPos.y - feetY) * sy, visualPos.z);
            visual.localRotation = Quaternion.Euler(0f, 0f, -dir * (surfTilt * squashAmount + diveTilt * diveAmount)) * visualRotation;
            visualTouched = true;

            if ((move is Move.Surf or Move.LowSlide) && Time.time >= nextWake && OnGround())
            {
                nextWake = Time.time + (move == Move.Surf ? wakeInterval : wakeInterval * 2f);
                var feet = FeetPoint() + new Vector2(-dir * 0.35f, 0.05f);
                HeatPuff.Spawn(feet, new Vector2(-dir * Random.Range(1f, 3.5f), Random.Range(0.6f, 2.2f)),
                    Random.Range(0.12f, 0.22f), Random.Range(0.35f, 0.55f), wakeColor, 0.35f, 9);
            }
        }

        /// <summary>Puts the visual back exactly as it was.</summary>
        void RestoreVisual()
        {
            if (!visualTouched || !visual) return;
            visual.localPosition = visualPos;
            visual.localScale = visualScale;
            visual.localRotation = visualRotation;
            squashAmount = diveAmount = 0f;
            visualTouched = false;
        }
    }
}
