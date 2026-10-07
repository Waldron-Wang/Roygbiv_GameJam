using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Creates the persistent "[Systems]" object before ANY scene loads — so you can press Play
    /// in any scene (a boss level, the sandbox) and everything still works. No bootstrap scene needed.
    /// </summary>
    static class Bootstrapper
    {
#if UNITY_EDITOR
        // BeforeSceneLoad does not run when scripts are recompiled during Play mode.
        [UnityEditor.Callbacks.DidReloadScripts]
        static void RebindAfterScriptReload()
        {
            if (!Application.isPlaying) return;
            var manager = Object.FindAnyObjectByType<GameManager>();
            if (!manager) return;
            var systems = manager.gameObject;
            Game.Config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            if (!Game.Config) Game.Config = ScriptableObject.CreateInstance<GameConfig>();
            Game.Input = systems.GetComponent<InputReader>();
            Game.Time = systems.GetComponent<TimeController>();
            Game.Scenes = systems.GetComponent<SceneLoader>();
            Game.Manager = manager;
            Game.Colors = systems.GetComponent<ColorWorld>();
            Game.Dialogue = systems.GetComponent<DialogueRunner>();
            Game.Instructions = systems.GetComponent<InstructionRunner>();
            Game.Audio = systems.GetComponent<AudioManager>();
            manager.EnsureProgress();
            var view = systems.GetComponent<InstructionView>();
            if (view) view.RebindPointerBlocker();
        }
#endif

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
            Game.Time = systems.AddComponent<TimeController>();
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
            systems.AddComponent<SerenityView>();
            systems.AddComponent<TitleCardView>();
            systems.AddComponent<CinematicView>();
            systems.AddComponent<DebugHud>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            systems.AddComponent<DebugCheats>();
#endif
        }
    }
}
