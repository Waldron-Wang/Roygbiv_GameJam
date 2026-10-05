using UnityEngine;

namespace Roygbiv
{
    /// <summary>Which looping animation a pre-boss card plays. Saved as ints: ONLY APPEND.</summary>
    public enum InstructionDemo { None, Reflect, ShootLatch, Overheat, Steal, Climb, FlipControls }

    /// <summary>
    /// A pre-boss instruction card: a small looping demo of the mechanic plus one short caption with key
    /// icons. Show, don't tell. One per color (Data/Instructions), set on ColorData.instruction; the
    /// LevelController shows it once per play session before the boss starts. InstructionView draws it.
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

        [Header("Demo")]
        public InstructionDemo demo;
        [Tooltip("Keys the demo shows being pressed: the caption's tokens without brackets (LMB, C, Space...). " +
                 "Steal flies them to the boss; FlipControls swaps [0]<->[1] and [2]<->[3]. Matching caption keys press in sync.")]
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
    }
}
