using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE — excitement, youth. Auto-scrolling chase (think Chrome dino game).
    /// The level sets PlayerMotor.autoRunSpeed + CameraFollow.autoScrollSpeed.
    /// Shoot switches (ShootableSwitch, Light Shot) to drop bridges and Stun() the boss;
    /// touch it while stunned to "catch" it. Catch it 3 times to win -> give it Health max = 3.
    /// Reward: Dash.
    /// </summary>
    public class OrangeBoss : BossBase
    {
        [SerializeField] float runSpeed = 7.5f;

        float stunnedUntil;
        public bool IsStunned => Time.time < stunnedUntil;

        /// <summary>Hook this to a ShootableSwitch's onActivated UnityEvent.</summary>
        public void Stun(float seconds) => stunnedUntil = Time.time + seconds;

        // Can't be shot to death — only caught.
        protected override void OnFightStarted() => Health.Invulnerable = true;

        protected override IEnumerator RunPhase(int phase)
        {
            // TODO(Orange owner): run ahead of the player, faster each phase; stop while stunned.
            if (Body) Body.linearVelocity = IsStunned ? Vector2.zero : new Vector2(runSpeed + phase, 0f);
            yield return null;
        }

        void OnCollisionEnter2D(Collision2D c)
        {
            // "Catch" = player touches the boss while it is stunned.
            if (IsStunned && c.collider.GetComponentInParent<PlayerController>())
            {
                Health.Invulnerable = false;
                Health.TakeDamage(new DamageInfo(1, Team.Player));
                Health.Invulnerable = true;
                stunnedUntil = 0f;
            }
        }
    }
}
