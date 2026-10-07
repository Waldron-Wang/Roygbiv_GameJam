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
        [Tooltip("Placed in a scene: wear the shared \"shoot this\" target (ShootSwitchLook), this wide. 0 = no look (code dresses it).")]
        [SerializeField] float lookSize;
        [SerializeField] UnityEvent onActivated = new();

        ShootSwitchLook look;
        bool used;

        public Team Team => Team.Neutral;
        public UnityEvent OnActivated => onActivated;

        void Awake()
        {
            if (lookSize <= 0f) return;
            if (TryGetComponent<SpriteRenderer>(out var square)) square.enabled = false;
            look = ShootSwitchLook.Create(transform, Vector2.zero, lookSize, 8);
        }

        public bool TakeDamage(in DamageInfo info)
        {
            if (used && once) return false;
            used = true;
            if (look) look.Spend();
            onActivated.Invoke();
            return true;
        }
    }
}
