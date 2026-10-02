using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// GREEN — envy, disgust. Brambles that steal.
    /// Each phase it steals one of the player's abilities (Light Shot -> Dash -> Blaze Strike)
    /// and uses it against them. Everything is returned on defeat.
    ///
    /// How: the boss has its own AbilityLoadout with copies of the abilities as children.
    ///   GameEvents.RaiseAbilityStolen(id) -> player's loadout disables it
    ///   bossLoadout.Grant(id)             -> boss can now TryActivate(id)
    /// Reward: TBD.
    /// </summary>
    [RequireComponent(typeof(AbilityLoadout))]
    public class GreenBoss : BossBase
    {
        [SerializeField] AbilityId[] stealOrder = { AbilityId.LightShot, AbilityId.Dash, AbilityId.BlazeStrike };

        AbilityLoadout loadout;
        readonly List<AbilityId> stolen = new();

        protected override void Awake()
        {
            base.Awake();
            loadout = GetComponent<AbilityLoadout>();
        }

        protected override void OnFightStarted() => Steal(0);
        protected override void OnPhaseChanged(int newPhase) => Steal(newPhase);

        void Steal(int index)
        {
            if (index >= stealOrder.Length) return;
            var id = stealOrder[index];
            if (!Game.Progress.HasAbility(id)) return; // nothing to steal (e.g. testing out of order)
            stolen.Add(id);
            GameEvents.RaiseAbilityStolen(id);
            loadout.Grant(id);
        }

        protected override IEnumerator RunPhase(int phase)
        {
            // TODO(Green owner): real patterns. Placeholder: use every stolen ability in turn.
            foreach (var id in stolen)
            {
                loadout.TryActivate(id);
                yield return Wait(1.2f);
            }
            yield return Wait(1f);
        }

        protected override void OnDefeated() => ReturnAll();
        void OnDestroy() => ReturnAll(); // also when the scene unloads mid-fight (player died)

        void ReturnAll()
        {
            foreach (var id in stolen) GameEvents.RaiseAbilityReturned(id);
            stolen.Clear();
        }
    }
}
