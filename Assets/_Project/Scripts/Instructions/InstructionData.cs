using UnityEngine;

namespace Roygbiv
{
    /// <summary>Which looping animation a how-to card plays. Saved as ints: ONLY APPEND.</summary>
    public enum InstructionDemo { None, Reflect, ShootLatch, Overheat, Steal, Climb, FlipControls, Gauntlet }

    /// <summary>
    /// A how-to card for a boss: a small looping demo of the mechanic plus one short caption with key icons.
    /// Show, don't tell. One per color (Data/Instructions), set on ColorData.instruction. It never pops up
    /// by itself: the player opens it from the on-screen Tip button (InstructionRunner), InstructionView draws it.
    ///
    /// No spoilers on a first try: the Tip button stays hidden until the player has died in the boss fight
    /// `nudgeAfterDeaths` times; then it offers only the `nudge` (a vague line, no demo). After `demoAfterDeaths`
    /// deaths it offers the full card. 0 / 0 = the full card from the start (Yellow's parry tutorial, Violet).
    ///
    /// Key tokens in caption / subCaption are drawn as keycaps:
    ///   [LMB] [RMB]                  left / right mouse button
    ///   [Left] [Right] [Up] [Down]   arrow keys
    ///   [anything else]              a key with that label: [Space] [Shift] [C] [Z]
    /// Rich text works between tokens (keep a token outside any color tag).
    /// </summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Instruction", fileName = "Instruction_")]
    public class InstructionData : ScriptableObject
    {
        [Header("Text")]
        [Tooltip("Main line under the demo. One line; rich text and key tokens allowed.")]
        [TextArea(1, 2)] public string caption;
        [Tooltip("Optional smaller second line, e.g. \"[Space] jump\".")]
        [TextArea(1, 2)] public string subCaption;
        [Tooltip("Card frame and highlight color.")]
        public Color accent = Color.white;

        [Header("Unlocking (no spoilers)")]
        [Tooltip("A vague one-line hint shown before the full card: points the player the right way without " +
                 "giving the answer. Empty = no nudge stage.")]
        [TextArea(1, 2)] public string nudge;
        [Tooltip("Deaths in this boss fight (this visit to the level) before the Tip button offers the nudge.")]
        public int nudgeAfterDeaths = 1;
        [Tooltip("Deaths before it offers the full card with the demo. 0 (with nudge 0) = available from the start.")]
        public int demoAfterDeaths = 3;
        [Tooltip("Hide the Tip button until the boss fight has started (not in the intro level before the arena gate).")]
        public bool onlyDuringBossFight;

        [Header("Demo")]
        public InstructionDemo demo;
        [Tooltip("Keys the demo shows being pressed: the caption's tokens without brackets (LMB, C, Space...). " +
                 "Steal: the ability key it takes, then the attack key. FlipControls: Left, Right, Space, LMB, RMB, Shift " +
                 "(the curses trade them around). Gauntlet: one key per ability, in the order the run uses them. " +
                 "Matching caption keys press in sync.")]
        public string[] keys = { };
        [Tooltip("The player's looping frames: idle, or run in a chase.")]
        public Sprite[] playerLoop = { };
        public float playerLoopFps = 8f;
        [Tooltip("Frames played when the key is pressed: attack, jump, hurt...")]
        public Sprite[] playerAction = { };
        public float playerActionFps = 20f;
        [Tooltip("Empty = a placeholder blob in the accent color.")]
        public Sprite boss;
        [Tooltip("Shown when it's hit or trapped. Empty = boss.")]
        public Sprite bossHurt;
        [Tooltip("Looping boss frames while it runs (Orange). Empty = boss.")]
        public Sprite[] bossMove = { };
        [Tooltip("Round things: orbs, shots. Empty = a plain disc.")]
        public Sprite prop;

        [Header("More player frames (Steal, Climb, FlipControls, Gauntlet)")]
        [Tooltip("Running. Empty = playerLoop.")]
        public Sprite[] playerRun = { };
        public float playerRunFps = 12f;
        [Tooltip("One jump, take-off to landing (held on the last frame). Empty = playerLoop.")]
        public Sprite[] playerJump = { };
        public float playerJumpFps = 15f;
        [Tooltip("Knocked back. Empty = the first playerLoop frame.")]
        public Sprite playerHurt;
    }
}
