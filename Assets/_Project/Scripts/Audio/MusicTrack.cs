using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// One piece of music and how to play it. One asset per song in Assets/_Project/Audio/Music.
    /// Hook it up in data, not code: ColorData.levelMusic / bossMusic, GameConfig menu/hub/ending, or a SceneMusic override.
    /// </summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Music Track", fileName = "Music_")]
    public class MusicTrack : ScriptableObject
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.8f;
        public bool loop = true;
        [Tooltip("Seconds to fade in when this track starts. Negative = use GameConfig.musicFadeSeconds.")]
        public float fadeIn = -1f;

        [Header("Adaptive stems (optional)")]
        [Tooltip("Extra parts that fade in once their color is restored (e.g. the hub gains an instrument per color). " +
                 "Each stem must match the main clip's length and tempo: they all start together.")]
        public List<Stem> stems = new();

        [Serializable]
        public struct Stem
        {
            public ColorId color;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        public float FadeIn => fadeIn >= 0f ? fadeIn : Game.Config.musicFadeSeconds;
    }
}
