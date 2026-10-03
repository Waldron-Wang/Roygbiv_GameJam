using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// BLUE — loneliness, sadness. Rising platformer: climb while something (water? gloom?) rises.
    /// Obstacles need momentum from Blaze Strike + Dash. The level may end at a LevelTrigger
    /// (CompleteLevel) at the top instead of a boss kill — the LevelController supports both.
    /// This stub is the "rising hazard" if you want one: it climbs and kills on touch.
    /// Reward: Heavy Slam (tentative).
    /// </summary>
    public class BlueBoss : BossBase
    {
        [SerializeField] float riseSpeed = 0.6f;

        protected override void OnFightStarted() => Health.Invulnerable = true;

        protected override IEnumerator RunPhase(int phase)
        {
            // TODO(Blue owner): design the climb.
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;
            yield return null;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsFighting && other.GetComponentInParent<PlayerController>() is { } p) p.Health.Kill();
        }
    }
}
