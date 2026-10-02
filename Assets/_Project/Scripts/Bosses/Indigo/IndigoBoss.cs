using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// INDIGO — calm, perception, spirituality. Disorientation: scrambles controls each phase
    /// (via IInputModifier on Game.Input) and the visuals (shake, odd colors — VFX owner).
    /// Reward: TBD.
    /// </summary>
    public class IndigoBoss : BossBase
    {
        [SerializeField] Projectile projectilePrefab;

        IInputModifier activeModifier;

        protected override void OnPhaseChanged(int newPhase)
        {
            SetModifier(newPhase switch
            {
                1 => new InvertHorizontalModifier(),
                2 => new SwapJumpAndAttackModifier(),
                _ => null,
            });
        }

        protected override IEnumerator RunPhase(int phase)
        {
            // TODO(Indigo owner): real patterns.
            if (projectilePrefab) Fire(projectilePrefab, AimDirection);
            yield return Wait(1.5f);
        }

        protected override void OnDefeated() => SetModifier(null);
        void OnDestroy() => SetModifier(null);

        void SetModifier(IInputModifier modifier)
        {
            if (Game.Input == null) return;
            if (activeModifier != null) Game.Input.RemoveModifier(activeModifier);
            activeModifier = modifier;
            if (activeModifier != null) Game.Input.AddModifier(activeModifier);
        }
    }
}
