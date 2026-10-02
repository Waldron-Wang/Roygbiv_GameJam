using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// YELLOW — warmth, joy. Tutorial boss: a cheerful sun-construct.
    /// Fires slow, bouncy light orbs; the player punches them back (basic melee reflects) to deal damage.
    /// Reward: Light Shot.
    /// </summary>
    public class YellowBoss : BossBase
    {
        [SerializeField] Projectile orbPrefab;   // must have reflectable = true
        [SerializeField] float timeBetweenVolleys = 2f;

        protected override IEnumerator RunPhase(int phase)
        {
            int orbs = 1 + phase; // phase 0: 1 orb, phase 1: 2, phase 2: 3
            for (int i = 0; i < orbs; i++)
            {
                if (orbPrefab) Fire(orbPrefab, AimDirection);
                yield return Wait(0.4f);
            }
            yield return Wait(timeBetweenVolleys);
        }
    }
}
