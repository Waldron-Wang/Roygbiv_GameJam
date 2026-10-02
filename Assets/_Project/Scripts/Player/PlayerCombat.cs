using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The basic attack every player has from the start: a short melee swing that also
    /// punches reflectable projectiles back (the Yellow tutorial boss is built around this).
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] Hitbox meleeHitbox;
        [SerializeField] float activeTime = 0.12f;
        [SerializeField] float cooldown = 0.3f;

        float readyAt;

        public bool TryAttack(int facingSign)
        {
            if (meleeHitbox == null || Time.time < readyAt) return false;
            readyAt = Time.time + cooldown;

            var p = meleeHitbox.transform.localPosition;
            p.x = Mathf.Abs(p.x) * facingSign;
            meleeHitbox.transform.localPosition = p;
            meleeHitbox.Open(activeTime);
            return true;
        }
    }
}
