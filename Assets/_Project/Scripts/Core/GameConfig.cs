using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Global, designer-tunable settings. Lives at Assets/_Project/Resources/GameConfig.asset
    /// and is loaded automatically by the Bootstrapper. Access it via Game.Config.
    /// </summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Tooltip("Play order. A color unlocks once every color before it is restored.")]
        public List<ColorData> colorOrder = new();

        [Header("Scenes")]
        public string mainMenuScene = "MainMenu";
        public string hubScene = "Hub";
        public string endingScene = "Ending";

        [Header("Story")]
        [Tooltip("The story before the first level (Prologue): plays on START for a save that hasn't seen it and has " +
                 "nothing restored, then goes straight into the first color's level. Empty = straight to the hub.")]
        public DialogueData prologue;

        [Header("Flow")]
        [Tooltip("Seconds to admire the recolored world before returning to the hub.")]
        public float returnToHubDelay = 2.5f;
        public float recolorDuration = 2f;
        public float respawnDelay = 1.5f;

        [Header("Music")]
        [Tooltip("Level and boss music live on each ColorData; see MusicDirector for the full picking order.")]
        public MusicTrack menuMusic;
        public MusicTrack hubMusic;
        public MusicTrack endingMusic;
        [Tooltip("Default crossfade length in seconds (a MusicTrack can set its own fade-in).")]
        public float musicFadeSeconds = 1.5f;
        [Tooltip("Music volume while dialogue, a Tip card, a cinematic or the pause menu is up.")]
        [Range(0f, 1f)] public float musicDuckVolume = 0.4f;
        [Tooltip("Music volume during a level's intro, before the boss fight (ColorData.muffleUntilBoss).")]
        [Range(0f, 1f)] public float introMusicVolume = 0.35f;
        [Tooltip("Boss start after a muffled intro (ColorData.restartOnBoss), or the boss reveal: seconds for the intro music to fade out...")]
        public float bossMusicFadeOut = 0.8f;
        [Tooltip("...seconds of silence...")]
        public float bossMusicSilence = 0.8f;
        [Tooltip("...and seconds for the boss song to fade in from the top.")]
        public float bossMusicFadeIn = 1.5f;

        [Header("Boss reveal (ColorData.bossIntro)")]
        [Tooltip("Camera size at the boss, as a fraction of normal (0.6 = zoomed in to 60%).")]
        [Range(0.3f, 1f)] public float bossIntroZoom = 0.6f;
        [Tooltip("Seconds to pan + zoom onto the boss.")]
        public float bossIntroPanSeconds = 1.2f;
        [Tooltip("Seconds on the boss, in silence, before the music starts.")]
        public float bossIntroBeforeMusic = 0.6f;
        [Tooltip("Seconds on the boss after the music starts.")]
        public float bossIntroAfterMusic = 1.4f;
        [Tooltip("Seconds to ease back to the player.")]
        public float bossIntroReturnSeconds = 0.9f;
        [Header("Final showcase (after the last color, before the Ending)")]
        [Tooltip("How far the camera rises off the level, in screen heights.")]
        public float finalShowcaseRise = 1.5f;
        [Tooltip("Seconds for the camera to rise while the background eases into its whole picture.")]
        public float finalShowcaseRiseSeconds = 3f;
        [Tooltip("Seconds to hold on the full-color background before the Ending loads.")]
        public float finalShowcaseHoldSeconds = 4f;

        [Tooltip("Fade the music out when a boss dies, so the recolor + story beat plays over silence.")]
        public bool stopMusicOnBossDefeated = true;
        [Tooltip("One-shot played when a color is restored (optional).")]
        public AudioClip colorRestoredStinger;

        public ColorData Get(ColorId id) => colorOrder.Find(c => c != null && c.id == id);
        public int IndexOf(ColorId id) => colorOrder.FindIndex(c => c != null && c.id == id);
    }
}
