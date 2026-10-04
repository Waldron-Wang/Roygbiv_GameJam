using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Drives the Animator + sprite facing on the player's "Visual" child from PlayerMotor state.
    /// Animator param: "Speed" (float, abs horizontal velocity) — Idle -> Run when Speed > 0.1.
    /// Purely cosmetic — reads the motor, never writes to it.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [Tooltip("Defaults to the Animator / SpriteRenderer on the \"Visual\" child.")]
        public Animator animator;
        public SpriteRenderer sprite;
        [Tooltip("Tick if the source art faces left.")]
        public bool artFacesLeft;

        PlayerMotor motor;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            var visual = transform.Find("Visual");
            if (!animator) animator = visual ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();
            if (!sprite) sprite = visual ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (sprite) sprite.flipX = (motor.FacingSign < 0) != artFacesLeft;
            if (!animator) return;
            animator.SetFloat(SpeedId, Mathf.Abs(motor.Body.linearVelocity.x));
        }
    }
}
