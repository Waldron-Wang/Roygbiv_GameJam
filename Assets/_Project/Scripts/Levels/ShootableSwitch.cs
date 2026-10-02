using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// Hit it (Light Shot, melee...) to fire a UnityEvent. Orange chase: drop a bridge, stun the boss.
    /// Shows that "hittable" is just IDamageable on Team.Neutral — no special case in combat code.
    /// </summary>
    public class ShootableSwitch : MonoBehaviour, IDamageable
    {
        [SerializeField] bool once = true;
        [SerializeField] UnityEvent onActivated;

        bool used;

        public Team Team => Team.Neutral;
        public UnityEvent OnActivated => onActivated;

        public bool TakeDamage(in DamageInfo info)
        {
            if (used && once) return false;
            used = true;
            onActivated.Invoke();
            return true;
        }
    }
}
