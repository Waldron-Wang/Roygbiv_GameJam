using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// Violet's checkpoint memory. Dying reloads the whole scene (the normal flow); for the final level that's too
    /// punishing, so this remembers the furthest checkpoint reached across reloads of the level:
    ///   0..SegmentCount-1   the start of each approach segment
    ///   ArenaIndex          the arena entrance (the duel, boss at full health)
    ///   TwinBladesIndex     the start of Twin Blades (boss at its threshold, cape gone)
    /// plus whether the intro has played (it never replays on a respawn).
    /// Cleared when any other scene loads (new game, F10, the hub, the menu, the ending) and when the level is won.
    /// Static on purpose (it outlives the scene), so it's reset at the start of every play session: domain reload is off.
    /// </summary>
    public static class VioletCheckpoint
    {
        public const int Start = -1;

        static string sceneName;

        /// <summary>The furthest checkpoint reached, or Start.</summary>
        public static int Index { get; private set; } = Start;
        public static bool IntroSeen { get; set; }
        /// <summary>The arrival cinematic has played once this session: from now on confirm skips it. Never cleared mid-session.</summary>
        public static bool ArrivalSeen { get; set; }
        /// <summary>Approach checkpoints in the course (set when the course is built).</summary>
        public static int SegmentCount { get; set; }
        public static int ArenaIndex => SegmentCount;
        public static int TwinBladesIndex => SegmentCount + 1;

        /// <summary>The memory belongs to this scene: loading any other one wipes it.</summary>
        public static void Bind(string scene) => sceneName = scene;

        public static void Reach(int index)
        {
            if (index > Index) Index = index;
        }

        /// <summary>Debug: go straight to a checkpoint (the caller reloads the scene).</summary>
        public static void JumpTo(int index)
        {
            Index = Mathf.Clamp(index, Start, TwinBladesIndex);
            IntroSeen = true;
        }

        public static void Clear()
        {
            Index = Start;
            IntroSeen = false;
            sceneName = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            SegmentCount = 0;
            ArrivalSeen = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Unity's own scene event (not GameEvents): this memory has no MonoBehaviour to subscribe from.
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
