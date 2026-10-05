using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Creates the persistent "[Systems]" object before ANY scene loads — so you can press Play
    /// in any scene (a boss level, the sandbox) and everything still works. No bootstrap scene needed.
    /// </summary>
    static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            if (config == null)
            {
                Debug.LogWarning("[ROYGBIV] No Resources/GameConfig found. Run menu ROYGBIV > Build Skeleton.");
                config = ScriptableObject.CreateInstance<GameConfig>();
            }
            Game.Config = config;

            var systems = new GameObject("[Systems]");
            Object.DontDestroyOnLoad(systems);

            // Order matters a little: anything later may use earlier services in its Awake.
            Game.Input = systems.AddComponent<InputReader>();
            Game.Scenes = systems.AddComponent<SceneLoader>();
            Game.Manager = systems.AddComponent<GameManager>();
            Game.Colors = systems.AddComponent<ColorWorld>();
            Game.Dialogue = systems.AddComponent<DialogueRunner>();
            Game.Instructions = systems.AddComponent<InstructionRunner>();
            Game.Audio = systems.AddComponent<AudioManager>();

            // Placeholder UI (IMGUI) — replace with real UI once the art style is chosen.
            systems.AddComponent<PauseMenu>();
            systems.AddComponent<DialogueView>();
            systems.AddComponent<InstructionView>();
            systems.AddComponent<DebugHud>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            systems.AddComponent<DebugCheats>();
#endif
        }
    }
}
