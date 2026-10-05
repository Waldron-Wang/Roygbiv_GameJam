using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Drives the Animator + sprite facing on the player's "Visual" child from PlayerMotor / PlayerCombat state.
    /// Animator params (built by ROYGBIV > Build Player Animations):
    ///   "Speed" (float)       abs horizontal velocity — Idle/Run, Attack/RunAttack.
    ///   "Grounded" (bool)     smoothed IsGrounded — Jump / Land.
    ///   "JumpProgress" (float) 0 = rising fast, 0.5 = apex, 1 = falling fast — scrubs the Jump clip.
    ///   "Attack" (trigger)    a swing started (grounded only; there's no air-attack art).
    ///   "Hurt" (bool)         true for hurtPoseTime after a hit, and while dead. Overrides everything.
    /// Its one write to the motor: Rooted (no running/turning) while the standing Attack state plays.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpProgressId = Animator.StringToHash("JumpProgress");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int AttackStateHash = Animator.StringToHash("Attack");
        static readonly int HurtId = Animator.StringToHash("Hurt");

        [Tooltip("Defaults to the Animator / SpriteRenderer on the \"Visual\" child.")]
        public Animator animator;
        public SpriteRenderer sprite;
        [Tooltip("Tick if the source art faces left.")]
        public bool artFacesLeft;
        [Tooltip("How long the player can be off the ground (without moving up) before the jump/fall pose shows. Hides 1-frame contact flicker and tiny ledge drops.")]
        public float airGrace = 0.08f;
        [Tooltip("Stop the player running/turning while the standing Attack animation plays. RunAttack never roots.")]
        public bool rootDuringAttack = true;
        [Tooltip("How long a hit shows the hurt pose.")]
        public float hurtPoseTime = 0.3f;

        PlayerMotor motor;
        PlayerCombat combat;
        Health health;
        float lastGroundedTime, hurtUntil;

        bool Grounded => motor.IsGrounded
            || (motor.Body.linearVelocity.y <= 0.01f && Time.time - lastGroundedTime < airGrace);

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
            var visual = transform.Find("Visual");
            if (!animator) animator = visual ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();
            if (!sprite) sprite = visual ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (combat) combat.Attacked += OnAttacked;
            if (health) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (combat) combat.Attacked -= OnAttacked;
            if (health) health.Damaged -= OnDamaged;
            motor.Rooted = false;
        }

        void OnDamaged(DamageInfo _) => hurtUntil = Time.time + hurtPoseTime;

        void OnAttacked()
        {
            if (animator && Grounded) animator.SetTrigger(AttackId);
        }

        void LateUpdate()
        {
            if (motor.IsGrounded) lastGroundedTime = Time.time;
            if (sprite) sprite.flipX = (motor.FacingSign < 0) != artFacesLeft;
            if (!animator) return;

            var v = motor.Body.linearVelocity;
            bool grounded = Grounded;
            bool hurt = Time.time < hurtUntil || (health && health.IsDead);
            animator.SetFloat(SpeedId, Mathf.Abs(v.x));
            animator.SetBool(GroundedId, grounded);
            animator.SetFloat(JumpProgressId, Mathf.Min(Mathf.InverseLerp(motor.jumpVelocity, -motor.jumpVelocity, v.y), 0.999f));
            animator.SetBool(HurtId, hurt);
            // An unconsumed trigger would fire a stray swing on landing / when the hurt pose ends.
            if (!grounded || hurt) animator.ResetTrigger(AttackId);

            // Checked after the Animator has updated this frame, so it's in place before the next FixedUpdate.
            // Jumping leaves the Attack state, which releases the root.
            motor.Rooted = rootDuringAttack && InState(AttackStateHash);
        }

        bool InState(int shortNameHash)
        {
            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == shortNameHash
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == shortNameHash);
        }
    }
}
