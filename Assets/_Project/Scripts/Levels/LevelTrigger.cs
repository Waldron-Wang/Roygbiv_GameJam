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

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired && once) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            fired = true;

            switch (action)
            {
                case Action.StartBoss: LevelController.Current?.StartBoss(); break;
                case Action.CompleteLevel: LevelController.Current?.Complete(); break;
                case Action.KillPlayer: player.Health.Kill(); fired = false; break;
            }
            onTriggered.Invoke();
        }
    }
}
