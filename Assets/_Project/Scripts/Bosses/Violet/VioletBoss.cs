using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// VIOLET — royalty, wisdom, creativity. Final boss. Mechanics TBD.
    /// Design pillar: "harder bosses test earlier abilities too" — a good place to require all of them.
    /// </summary>
    public class VioletBoss : BossBase
    {
        [SerializeField] Projectile projectilePrefab;

        protected override IEnumerator RunPhase(int phase)
        {
            // TODO(Violet owner)
            if (projectilePrefab) Fire(projectilePrefab, AimDirection);
            yield return Wait(1.5f);
        }
    }
}
