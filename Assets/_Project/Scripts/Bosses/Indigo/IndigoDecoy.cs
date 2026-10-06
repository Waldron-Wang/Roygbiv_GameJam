using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// One of the Indigo boss's illusions. It takes a hit like the real thing, but a hit only tells the boss,
    /// which bursts it in a ring of orbs: the punishment for misreading which one is real.
    /// </summary>
    public class IndigoDecoy : MonoBehaviour, IDamageable
    {
        bool popped;

        public event Action<IndigoDecoy> Hit;

        public Team Team => Team.Enemy;

        public bool TakeDamage(in DamageInfo info)
        {
            if (popped || !Combat.CanHurt(info.sourceTeam, Team)) return false;
            popped = true;
            Hit?.Invoke(this);
            return true;
        }
    }
}
