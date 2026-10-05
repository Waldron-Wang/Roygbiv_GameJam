using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>Orange's reward. Fast horizontal burst with invulnerability (dash through Red's fire).</summary>
    public class DashAbility : AbilityBase
    {
        [SerializeField] float speed = 18f;
        [SerializeField] float duration = 0.18f;
        [SerializeField] bool invulnerableWhileDashing = true;
        [SerializeField] DashAfterImage afterimage;

        public override AbilityId Id => AbilityId.Dash;

        public override bool HandleInput(in PlayerIntent intent) => intent.dashPressed && TryActivate();

        protected override void Activate() => StartCoroutine(DashRoutine());

        void FindAfterimage()
        {
            if (afterimage) return;
            afterimage = GetComponentInChildren<DashAfterImage>();
            if (!afterimage) afterimage = GetComponentInParent<DashAfterImage>();
            if (!afterimage)
                Debug.LogWarning("DashAbility: no DashAfterImage found. Add the component to the player or drag it into the Afterimage field.", this);
        }

        IEnumerator DashRoutine()
        {
            FindAfterimage();
            var body = Owner.Body;
            float gravity = body.gravityScale;

            Owner.MovementLocked = true;
            if (invulnerableWhileDashing && Owner.Health) Owner.Health.Invulnerable = true;
            body.gravityScale = 0f;
            body.linearVelocity = new Vector2(Owner.FacingSign * speed, 0f);

            // start trail
            if (afterimage) afterimage.Play(duration);

            yield return new WaitForSeconds(duration);

            body.gravityScale = gravity;
            body.linearVelocity = new Vector2(body.linearVelocity.x * 0.3f, 0f);
            if (Owner.Health) Owner.Health.Invulnerable = false;
            Owner.MovementLocked = false;
        }
    }
}