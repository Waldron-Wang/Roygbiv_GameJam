using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE — excitement, youth. Auto-scrolling chase (think Chrome dino game).
    /// The level sets PlayerMotor.autoRunSpeed + CameraFollow.autoScrollSpeed.
    /// Shoot switches (ShootableSwitch, Light Shot) to drop bridges and Stun() the boss;
    /// touch it while stunned to "catch" it. Catch it 3 times to win -> give it Health max = 3.
    ///
    /// Pacing: the boss runs at the same speed as the scroll, so it stays on screen at a fixed gap.
    /// A stun lets the player close that gap; after each catch it bolts ahead to re-open it.
    /// Reward: Dash.
    /// </summary>
    public class OrangeBoss : BossBase
    {
        [Tooltip("Keep equal to the level's auto-scroll speed, or the boss drifts off screen.")]
        [SerializeField] float runSpeed = 6f;
        [Tooltip("Gap (world units) ahead of the player when the chase starts.")]
        [SerializeField] float startLead = 9f;
        [Tooltip("After a catch the boss sprints at this speed for escapeTime to re-open the gap.")]
        [SerializeField] float escapeSpeed = 12f;
        [SerializeField] float escapeTime = 1f;

        float stunnedUntil, escapingUntil;
        public bool IsStunned => Time.time < stunnedUntil;

        /// <summary>Hook this to a ShootableSwitch's onActivated UnityEvent.</summary>
        public void Stun(float seconds)
        {
            if (IsFighting) stunnedUntil = Time.time + seconds;
        }

        protected override void OnFightStarted()
        {
            Health.Invulnerable = true; // can't be shot to death — only caught
            if (Player) transform.position = new Vector3(Player.position.x + startLead, transform.position.y, transform.position.z);
        }

        protected override IEnumerator RunPhase(int phase)
        {
            // TODO(Orange owner): use `phase` to make later laps harder (shorter stuns, obstacles...).
            float speed = IsStunned ? 0f : Time.time < escapingUntil ? escapeSpeed : runSpeed;
            if (Body) Body.linearVelocity = new Vector2(speed, 0f);
            yield return null;
        }

        protected override void OnDefeated()
        {
            if (Body) Body.linearVelocity = Vector2.zero;
        }

        // Stay as well as Enter: the player may already be pressed against the boss when it gets stunned.
        void OnCollisionEnter2D(Collision2D c) => TryCatch(c);
        void OnCollisionStay2D(Collision2D c) => TryCatch(c);

        void TryCatch(Collision2D c)
        {
            if (!IsStunned || !c.collider.GetComponentInParent<PlayerController>()) return;

            stunnedUntil = 0f;
            escapingUntil = Time.time + escapeTime;
            Health.Invulnerable = false;
            Health.TakeDamage(new DamageInfo(1, Team.Player));
            Health.Invulnerable = true;
        }
    }
}
