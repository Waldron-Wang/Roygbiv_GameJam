using UnityEngine;

namespace Roygbiv
{
    /// <summary>Red's reward. Hold Attack to charge, release for a heavy strike.</summary>
    public class BlazeStrikeAbility : AbilityBase
    {
        [SerializeField] Hitbox strikeHitbox;
        [SerializeField] float chargeTime = 0.6f;
        [SerializeField] float activeTime = 0.2f;

        float heldFor;

        public override AbilityId Id => AbilityId.BlazeStrike;
        public float ChargeFraction => Mathf.Clamp01(heldFor / chargeTime); // for UI / VFX

        public override bool HandleInput(in PlayerIntent intent)
        {
            if (intent.attackHeld) heldFor += Time.deltaTime;
            if (!intent.attackReleased) return false;

            bool charged = heldFor >= chargeTime;
            heldFor = 0f;
            return charged && TryActivate();
        }

        protected override void Activate()
        {
            if (strikeHitbox == null) { Debug.LogWarning("BlazeStrike has no hitbox."); return; }
            var p = strikeHitbox.transform.localPosition;
            p.x = Mathf.Abs(p.x) * Owner.FacingSign;
            strikeHitbox.transform.localPosition = p;
            strikeHitbox.team = Owner.Team; // stays correct when Green steals it
            strikeHitbox.Open(activeTime);
        }
    }
}
