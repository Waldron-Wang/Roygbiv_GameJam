using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Base for every color ability. An ability lives as a component under its owner (player OR boss)
    /// and talks to the owner only through IActor, so the same ability works for both.
    ///
    ///  - enabled == unlocked. AbilityLoadout flips this; don't do it yourself.
    ///  - Player path: AbilityLoadout calls HandleInput(intent) every frame.
    ///  - AI path:     a boss calls TryActivate() whenever its pattern says so.
    ///
    /// To add an ability: add a value to AbilityId, subclass this, put it on the Player prefab
    /// (under "Abilities"), and point a ColorData's grantedAbility at it.
    /// </summary>
    public abstract class AbilityBase : MonoBehaviour
    {
        [SerializeField] protected float cooldown = 0.5f;

        float readyAt;

        public abstract AbilityId Id { get; }
        public IActor Owner { get; private set; }
        public bool IsReady => enabled && Time.time >= readyAt;

        protected virtual void Awake() => Owner = GetComponentInParent<IActor>();

        /// <summary>Player input hook. Return true if this ability consumed the input this frame.</summary>
        public virtual bool HandleInput(in PlayerIntent intent) => false;

        /// <summary>Fire now if unlocked and off cooldown.</summary>
        public bool TryActivate()
        {
            if (!IsReady) return false;
            readyAt = Time.time + cooldown;
            Activate();
            return true;
        }

        protected abstract void Activate();
    }
}
