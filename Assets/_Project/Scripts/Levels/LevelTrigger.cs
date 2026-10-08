using UnityEngine;
using UnityEngine.Events;

namespace Roygbiv
{
    /// <summary>
    /// Drop-in trigger zone for level designers. Fires once when the player enters.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelTrigger : MonoBehaviour
    {
        public enum Action { None, StartBoss, CompleteLevel, KillPlayer }

        [SerializeField] Action action;
        [SerializeField] bool once = true;
        [Tooltip("Extra hooks: open doors, play dialogue, start music...")]
        [SerializeField] UnityEvent onTriggered = new();

        bool fired;

        void Awake() => GetComponent<Collider2D>().isTrigger = true;

        public Action TriggerAction => action;

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            Fire(player);
        }

        /// <summary>Fires as if the player just walked in (BossCheckpoint uses it to re-close the arena gate).</summary>
        public void Fire() => Fire(PlayerController.Instance);

        void Fire(PlayerController player)
        {
            if (fired && once) return;
            fired = true;

            switch (action)
            {
                case Action.StartBoss: LevelController.Current?.StartBoss(); break;
                case Action.CompleteLevel: LevelController.Current?.Complete(); break;
                case Action.KillPlayer: if (player) player.Health.Kill(); fired = false; break;
            }
            onTriggered.Invoke();
        }
    }
}
