using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Service locator: the one place to reach the persistent systems.
    ///   Game.Manager.EnterLevel(ColorId.Red);
    ///   Game.Audio.PlaySfx(clip);
    ///   if (Game.Progress.HasAbility(AbilityId.Dash)) ...
    /// Filled in by Bootstrapper before the first scene loads, so it is safe to use from Awake onward.
    /// </summary>
    public static class Game
    {
        public static GameConfig Config { get; internal set; }
        public static GameManager Manager { get; internal set; }
        public static SceneLoader Scenes { get; internal set; }
        public static InputReader Input { get; internal set; }
        public static ColorWorld Colors { get; internal set; }
        public static DialogueRunner Dialogue { get; internal set; }
        public static InstructionRunner Instructions { get; internal set; }
        public static AudioManager Audio { get; internal set; }

        public static GameProgress Progress => Manager.Progress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            Config = null; Manager = null; Scenes = null; Input = null;
            Colors = null; Dialogue = null; Instructions = null; Audio = null;
        }
    }
}
