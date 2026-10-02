using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Music + one-shot SFX. Gameplay code may call Game.Audio.PlaySfx(clip) directly.
    /// Adaptive music: every restored color fades in its ColorData.musicLayer on top of the base track
    /// (all layers should share tempo/length so they stay in sync).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] float layerFadeSpeed = 0.5f;

        AudioSource music, sfx;
        readonly Dictionary<ColorId, AudioSource> layers = new();

        void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            sfx = gameObject.AddComponent<AudioSource>();
        }

        void Start()
        {
            foreach (var data in Game.Config.colorOrder)
            {
                if (data == null || data.musicLayer == null) continue;
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = data.musicLayer;
                src.loop = true;
                src.volume = 0f;
                layers[data.id] = src;
            }
        }

        void Update()
        {
            foreach (var (color, src) in layers)
            {
                float target = Game.Progress.IsRestored(color) && music.isPlaying ? 1f : 0f;
                src.volume = Mathf.MoveTowards(src.volume, target, layerFadeSpeed * Time.unscaledDeltaTime);
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || music.clip == clip) return;
            music.clip = clip;
            music.Play();
            foreach (var src in layers.Values) { src.timeSamples = 0; src.Play(); }
        }

        public void StopMusic()
        {
            music.Stop();
            foreach (var src in layers.Values) src.Stop();
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip) sfx.PlayOneShot(clip, volume);
        }
    }
}
