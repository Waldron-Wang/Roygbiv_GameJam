using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>Blue's reward (tentative). Down + Attack in mid-air: plunge, then burst on landing.</summary>
    public class HeavySlamAbility : AbilityBase
    {
        [SerializeField] Hitbox landingHitbox;
        [SerializeField] float slamSpeed = 25f;
        [SerializeField] float maxFallTime = 1.5f;

        public override AbilityId Id => AbilityId.HeavySlam;

        public override bool HandleInput(in PlayerIntent intent) =>
            intent.attackPressed && intent.move.y < -0.5f && !Owner.IsGrounded && TryActivate();

        protected override void Activate() => StartCoroutine(SlamRoutine());

        IEnumerator SlamRoutine()
        {
            Owner.MovementLocked = true;
            Owner.Body.linearVelocity = new Vector2(0f, -slamSpeed);

            float t = 0f;
            yield return new WaitForFixedUpdate();
            while (!Owner.IsGrounded && t < maxFallTime)
            {
                Owner.Body.linearVelocity = new Vector2(0f, -slamSpeed);
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (landingHitbox != null)
            {
                landingHitbox.team = Owner.Team;
                landingHitbox.Open(0.15f);
            }
            Owner.MovementLocked = false;
        }
    }
}
