using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Side-view platformer movement: run, variable-height jump, coyote time, jump buffer.
    /// Knows nothing about input devices — PlayerController feeds it every frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Run")]
        public float runSpeed = 7f;
        public float groundAcceleration = 70f;
        public float airAcceleration = 40f;

        [Header("Jump")]
        public float jumpVelocity = 13f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.12f;
        public float fallGravityMultiplier = 1.8f;
        [Tooltip("Extra gravity when jump is released early -> short hop.")]
        public float lowJumpGravityMultiplier = 2.5f;

        [Header("Level overrides")]
        [Tooltip(">0 forces the player to run right at this speed (Orange auto-scrolling chase).")]
        public float autoRunSpeed;

        readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
        Rigidbody2D rb;
        float baseGravity;
        float lastGroundedTime = -1f, lastJumpPressedTime = -1f;
        Vector2 moveInput;
        bool jumpHeld, launched;

        public Rigidbody2D Body => rb;
        public bool IsGrounded { get; private set; }
        public int FacingSign { get; private set; } = 1;
        public bool Locked { get; set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            baseGravity = rb.gravityScale;
        }

        public void SetInput(Vector2 move, bool jumpPressed, bool jumpIsHeld)
        {
            moveInput = move;
            jumpHeld = jumpIsHeld;
            if (jumpPressed) lastJumpPressedTime = Time.time;
            if (!Locked && Mathf.Abs(move.x) > 0.1f) FacingSign = move.x > 0 ? 1 : -1;
        }

        /// <summary>Springs etc.: throws the player up to full height whether or not jump is held.</summary>
        public void Launch(float upVelocity)
        {
            var v = rb.linearVelocity;
            v.y = upVelocity;
            rb.linearVelocity = v;
            launched = true;
            lastJumpPressedTime = lastGroundedTime = -1f; // no extra jump stacked on top
        }

        void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
            if (IsGrounded) lastGroundedTime = Time.time;
            if (Locked) return; // a dash / slam / knockback owns the velocity right now

            var v = rb.linearVelocity;
            if (launched && v.y <= 0f) launched = false;

            float target = autoRunSpeed > 0f ? autoRunSpeed : moveInput.x * runSpeed;
            float accel = IsGrounded ? groundAcceleration : airAcceleration;
            v.x = Mathf.MoveTowards(v.x, target, accel * Time.fixedDeltaTime);

            bool buffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
            bool coyote = Time.time - lastGroundedTime <= coyoteTime;
            if (buffered && coyote)
            {
                v.y = jumpVelocity;
                lastJumpPressedTime = lastGroundedTime = -1f;
            }

            if (v.y < 0f) rb.gravityScale = baseGravity * fallGravityMultiplier;
            else if (v.y > 0f && !jumpHeld && !launched) rb.gravityScale = baseGravity * lowJumpGravityMultiplier;
            else rb.gravityScale = baseGravity;

            rb.linearVelocity = v;
        }

        public void ResetGravity() => rb.gravityScale = baseGravity;

        bool CheckGrounded()
        {
            int count = rb.GetContacts(contacts);
            for (int i = 0; i < count; i++)
                if (!contacts[i].collider.isTrigger && contacts[i].normal.y > 0.6f) return true;
            return false;
        }
    }
}
