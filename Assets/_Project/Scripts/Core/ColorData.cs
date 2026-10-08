using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Everything the game knows about one color. One asset per color in Assets/_Project/Data/Colors.
    /// Designers edit these in the Inspector; code reads them through Game.Config.
    /// </summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Color Data", fileName = "Color_")]
    public class ColorData : ScriptableObject
    {
        public ColorId id;
        public string displayName;
        public Color tint = Color.white;
        [TextArea] public string emotion;

        [Header("Level")]
        [Tooltip("Scene that holds this color's district + boss fight. Must be in Build Settings.")]
        public string sceneName;
        [Tooltip("Optional how-to card the player can open from the on-screen Tip button during this level. Empty = no Tip button.")]
        public InstructionData instruction;

        [Header("Reward")]
        public AbilityId grantedAbility;
        [Tooltip("Story fragment played after the color is reclaimed.")]
        public DialogueData storyFragment;

        [Header("Boss")]
        [Tooltip("Arena gate: freeze, pan + zoom to the boss, start the boss music, then fight (BossIntro). " +
                 "Only for levels that start the boss with a LevelTrigger, not startBossImmediately.")]
        public bool bossIntro;
        [Tooltip("After the boss fight has started, dying (or Restart) puts the player back at the arena gate and restarts " +
                 "the fight instead of the whole level (BossCheckpoint).")]
        public bool respawnAtBoss;

        [Header("Music")]
        [Tooltip("Plays in this color's level scene (see MusicDirector).")]
        public MusicTrack levelMusic;
        [Tooltip("Takes over when the boss fight starts. Empty = keep the level music.")]
        public MusicTrack bossMusic;
        [Tooltip("Intro level: play the music quiet and muffled (heard through a wall) until the boss fight starts, " +
                 "then open it up. Turn off for levels whose boss is there from the start.")]
        public bool muffleUntilBoss = true;
        [Tooltip("When the fight starts after a muffled intro: fade out, a beat of silence, then the song fades in from the top " +
                 "(timings in GameConfig). Off = keep playing and just open up.")]
        public bool restartOnBoss = true;
    }
}
