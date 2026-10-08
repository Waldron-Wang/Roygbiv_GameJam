using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Optional per-scene override. Most scenes don't need one: MusicDirector already plays the ColorData /
    /// GameConfig music for them. Drop this in a scene to play something else (leave Music empty for silence).
    /// </summary>
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] MusicTrack music;
        [Tooltip("Takes over when this scene's boss fight starts. Empty = keep Music playing.")]
        [SerializeField] MusicTrack bossMusic;
        [Tooltip("Quiet + muffled until the boss fight starts (see ColorData.muffleUntilBoss).")]
        [SerializeField] bool muffleUntilBoss;
        [Tooltip("When the fight starts after a muffled intro: fade out, silence, then fade the song in from the top.")]
        [SerializeField] bool restartOnBoss = true;

        public MusicTrack Music => music;
        public MusicTrack BossMusic => bossMusic;
        public bool MuffleUntilBoss => muffleUntilBoss;
        public bool RestartOnBoss => restartOnBoss;
    }
}
