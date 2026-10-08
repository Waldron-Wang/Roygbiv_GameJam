using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// The arena-gate checkpoint (ColorData.respawnAtBoss): once the boss fight has been started in a level, dying
    /// (or the pause menu's Restart) reloads the scene and LevelController puts the player back at the gate, which
    /// starts the fight again (boss at full health, reveal and all) instead of replaying the whole intro level.
    /// Remembers where the player stood when the gate fired. Cleared when any other scene loads (the hub after a win
    /// or quitting, the menu). Static on purpose (it outlives the scene); reset every play session: domain reload is off.
    /// Violet has its own, VioletCheckpoint.
    /// </summary>
    public static class BossCheckpoint
    {
        static string sceneName;

        /// <summary>Where the player stood when the boss fight started.</summary>
        public static Vector2 Position { get; private set; }

        /// <summary>True when the scene being played has a boss checkpoint.</summary>
        public static bool Active => sceneName != null && sceneName == SceneManager.GetActiveScene().name;

        public static void Reach(Vector2 playerPosition)
        {
            sceneName = SceneManager.GetActiveScene().name;
            Position = playerPosition;
        }

        public static void Clear() => sceneName = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Unsubscribe first, so a second play session never ends up with two handlers.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (sceneName != null && scene.name != sceneName) Clear();
        }
    }
}
