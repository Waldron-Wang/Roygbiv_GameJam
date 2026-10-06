using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Which abilities an actor currently has. Collects every AbilityBase in its children and
    /// enables only the unlocked ones.
    ///  - Player (syncWithProgress = true): mirrors the save file and reacts to
    ///    AbilityUnlocked / AbilityStolen / AbilityReturned events.
    ///  - Bosses (syncWithProgress = false): call Grant / Revoke from boss code.
    /// </summary>
    public class AbilityLoadout : MonoBehaviour
    {
        [SerializeField] bool syncWithProgress;
        [Tooltip("TESTING ONLY: enables every ability found, ignoring the save. Untick before shipping.")]
        [SerializeField] bool debugUnlockAll;

        readonly Dictionary<AbilityId, AbilityBase> abilities = new();
        readonly HashSet<AbilityId> stolen = new();

        public IEnumerable<AbilityBase> All => abilities.Values;

        void Awake()
        {
            foreach (var a in GetComponentsInChildren<AbilityBase>(true))
            {
                if (abilities.ContainsKey(a.Id)) { Debug.LogWarning($"Duplicate ability {a.Id} on {name}"); continue; }
                abilities.Add(a.Id, a);
                a.enabled = false;
            }
            Debug.Log($"[AbilityLoadout] {name} registered: {string.Join(", ", abilities.Keys)}", this);
        }

        void Start()
        {
            if (!syncWithProgress) return;
            foreach (var id in abilities.Keys)
                abilities[id].enabled = debugUnlockAll || (Game.Progress.HasAbility(id) && !stolen.Contains(id));
        }

        void OnEnable()
        {
            if (!syncWithProgress) return;
            GameEvents.AbilityUnlocked += Grant;
            GameEvents.AbilityStolen += OnStolen;
            GameEvents.AbilityReturned += OnReturned;
        }

        void OnDisable()
        {
            if (!syncWithProgress) return;
            GameEvents.AbilityUnlocked -= Grant;
            GameEvents.AbilityStolen -= OnStolen;
            GameEvents.AbilityReturned -= OnReturned;
        }

        public bool Has(AbilityId id) => abilities.TryGetValue(id, out var a) && a.enabled;
        public AbilityBase Get(AbilityId id) => abilities.TryGetValue(id, out var a) ? a : null;

        public void Grant(AbilityId id)
        {
            if (id == AbilityId.None) return;
            if (abilities.TryGetValue(id, out var a)) a.enabled = !stolen.Contains(id);
            else Debug.LogWarning($"{name} has no component for ability {id}.");
        }

        public void Revoke(AbilityId id)
        {
            if (abilities.TryGetValue(id, out var a)) a.enabled = false;
        }

        public bool TryActivate(AbilityId id) => abilities.TryGetValue(id, out var a) && a.TryActivate();

        /// <summary>Player path. Returns true if any ability consumed the input.</summary>
        public bool HandleInput(in PlayerIntent intent)
        {
            // Dash and Down Dash share the dash button. Who gets a press is decided here, once, never by
            // dictionary order: Down Dash unlocked + Down held = Down Dash's press (even while it cools down, so it
            // never turns into a plain Dash); anything else = Dash's. A Dash fired mid Down Dash ends that first.
            var downDash = Get(AbilityId.DownDash) as DownDashAbility;
            bool pressIsDownDash = intent.dashPressed && downDash && downDash.enabled && downDash.IsDownHeld(intent);
            if (intent.dashPressed && !pressIsDownDash && downDash && downDash.IsActive && Get(AbilityId.Dash) is { IsReady: true })
                downDash.Cancel();

            bool consumed = false;
            foreach (var a in abilities.Values)
            {
                if (!a.enabled) continue;
                var routed = intent;
                if (a.Id == AbilityId.Dash) routed.dashPressed = intent.dashPressed && !pressIsDownDash;
                else if (a.Id == AbilityId.DownDash) routed.dashPressed = pressIsDownDash;
                if (a.HandleInput(routed)) consumed = true;
            }
            return consumed;
        }

        void OnStolen(AbilityId id)
        {
            stolen.Add(id);
            Revoke(id);
        }

        void OnReturned(AbilityId id)
        {
            stolen.Remove(id);
            if (Game.Progress.HasAbility(id)) Grant(id);
        }
    }
}