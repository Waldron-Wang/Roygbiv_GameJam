using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// RED — hot-blooded, anger. Rage meter rises as it takes damage, so it charges harder.
    /// It rushes the player and throws fire everywhere (dash through it). When rage maxes out it
    /// OVERHEATS: briefly vulnerable. Only hittable while overheated.
    /// Reward: Blaze Strike.
    /// </summary>
    public class RedBoss : BossBase
    {
        [SerializeField] Projectile firePrefab;
        [SerializeField] float rageMax = 100f;
        [SerializeField] float ragePerSecond = 12f;
        [SerializeField] float overheatDuration = 3f;

        public float Rage { get; private set; }
        public float RageFraction => Rage / rageMax; // for UI
        public bool Overheated { get; private set; }

        protected override void OnFightStarted() => Health.Invulnerable = true;

        protected override IEnumerator RunPhase(int phase)
        {
            if (Overheated) yield break;

            // TODO(Red owner): real charge + fire patterns. Placeholder: spray fire, build rage.
            if (firePrefab)
                for (int i = -1; i <= 1; i++)
                    Fire(firePrefab, Quaternion.Euler(0, 0, i * 15f) * AimDirection);

            Rage += ragePerSecond * (1f + phase * 0.5f);
            yield return Wait(1f);

            if (Rage >= rageMax) yield return Overheat();
        }

        IEnumerator Overheat()
        {
            Overheated = true;
            Health.Invulnerable = false;
            yield return Wait(overheatDuration);
            Health.Invulnerable = true;
            Overheated = false;
            Rage = 0f;
        }
    }
}
