using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Plays music and one-shot SFX. It decides HOW things sound, never WHAT plays: MusicDirector picks the
    /// track from data and events. Gameplay code may still call Game.Audio.PlaySfx(clip) directly.
    ///   Game.Audio.PlayMusic(track)            crossfade to a MusicTrack (no-op if it's already playing, unless restart)
    ///   Game.Audio.StopMusic()                 fade out
    ///   Game.Audio.Duck(this[, 0.3f]) / Unduck(this)  quieter while any duck is held; the quietest one wins
    ///   Game.Audio.Muffle(this) / Unmuffle(this)      low-pass while any muffle is held (pause menu, boss intro)
    /// Each track plays on its own "deck" (main clip + adaptive stems), so an old track fades out while the new one
    /// fades in. Everything runs on unscaled time, so fades keep going while the game is paused.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const float DuckSpeed = 2f;        // volume per second
        const float StemFadeSpeed = 0.5f;  // a newly restored color's stem takes ~2s to fade in
        const float OpenCutoff = 22000f, MuffledCutoff = 900f;

        /// <summary>Player settings (0..1). Multiplied on top of each track's own volume.</summary>
        public float MusicVolume { get; set; } = 1f;
        public float SfxVolume { get => sfx.volume; set => sfx.volume = value; }

        /// <summary>The track playing or fading in (null when silent or fading out).</summary>
        public MusicTrack CurrentMusic => decks.Count > 0 && decks[^1].target > 0f ? decks[^1].track : null;

        AudioSource sfx;
        Transform musicRoot;
        readonly List<AudioLowPassFilter> lowPasses = new();
        float cutoff = OpenCutoff;
        readonly List<Deck> decks = new(); // newest last
        readonly Dictionary<object, float> ducks = new(); // owner -> volume it asks for
        readonly HashSet<object> muffles = new();
        float duck = 1f;

        class Deck
        {
            public MusicTrack track;
            public AudioSource main;
            public readonly List<(MusicTrack.Stem stem, AudioSource src, float level)> stems = new();
            public float fade, target, speed;
            public float startedAt;
        }

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            // Each music source gets its own child + low-pass (a filter only affects the AudioSource on its own
            // GameObject), so the pause muffle never touches SFX.
            musicRoot = new GameObject("Music").transform;
            musicRoot.SetParent(transform, false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float duckTarget = 1f;
            foreach (var v in ducks.Values) duckTarget = Mathf.Min(duckTarget, v);
            duck = Mathf.MoveTowards(duck, duckTarget, DuckSpeed * dt);
            cutoff = Mathf.Lerp(cutoff, muffles.Count > 0 ? MuffledCutoff : OpenCutoff, 1f - Mathf.Exp(-8f * dt));
            foreach (var lp in lowPasses) lp.cutoffFrequency = cutoff;

            for (int i = decks.Count - 1; i >= 0; i--)
            {
                var d = decks[i];
                d.fade = Mathf.MoveTowards(d.fade, d.target, d.speed * dt);
                bool ended = !d.track.loop && !d.main.isPlaying && Time.unscaledTime - d.startedAt > 0.5f;
                if ((d.target <= 0f && d.fade <= 0f) || ended)
                {
                    Release(d);
                    decks.RemoveAt(i);
                    continue;
                }

                float level = d.fade * duck * MusicVolume;
                d.main.volume = d.track.volume * level;
                for (int s = 0; s < d.stems.Count; s++)
                {
                    var (stem, src, stemLevel) = d.stems[s];
                    float want = Game.Progress.IsRestored(stem.color) ? 1f : 0f;
                    stemLevel = Mathf.MoveTowards(stemLevel, want, StemFadeSpeed * dt);
                    src.volume = stem.volume * stemLevel * level;
                    d.stems[s] = (stem, src, stemLevel);
                }
            }
        }

        // ---------- Music ----------

        /// <param name="fadeSeconds">Crossfade length. Negative = the track's own fade-in (or GameConfig's default).</param>
        /// <param name="restart">Start the track from the top even if it's already playing.</param>
        public void PlayMusic(MusicTrack track, float fadeSeconds = -1f, bool restart = false)
        {
            if (track == null || track.clip == null) { StopMusic(fadeSeconds); return; }
            if (!restart && CurrentMusic == track) return; // already playing: a scene reload doesn't restart the song

            float fadeIn = fadeSeconds >= 0f ? fadeSeconds : track.FadeIn;
            float fadeOut = fadeSeconds >= 0f ? fadeSeconds : Game.Config.musicFadeSeconds;
            foreach (var d in decks) FadeTo(d, 0f, fadeOut);

            // Still fading out from a moment ago (boss -> level -> boss)? Bring it back instead of restarting it.
            var deck = restart ? null : decks.Find(d => d.track == track);
            if (deck != null) decks.Remove(deck);
            else deck = CreateDeck(track);
            decks.Add(deck);
            FadeTo(deck, 1f, fadeIn);
        }

        public void StopMusic(float fadeSeconds = -1f)
        {
            float fadeOut = fadeSeconds >= 0f ? fadeSeconds : Game.Config.musicFadeSeconds;
            foreach (var d in decks) FadeTo(d, 0f, fadeOut);
        }

        /// <param name="volume">0..1 while held. Negative = GameConfig.musicDuckVolume.</param>
        public void Duck(object owner, float volume = -1f)
        {
            if (owner != null) ducks[owner] = volume >= 0f ? volume : Game.Config.musicDuckVolume;
        }

        /// <param name="instant">Jump straight back to full volume (if nothing else holds a duck) instead of easing.</param>
        public void Unduck(object owner, bool instant = false)
        {
            if (owner != null && ducks.Remove(owner) && instant && ducks.Count == 0) duck = 1f;
        }

        public void Muffle(object owner) { if (owner != null) muffles.Add(owner); }

        public void Unmuffle(object owner, bool instant = false)
        {
            if (owner != null && muffles.Remove(owner) && instant && muffles.Count == 0) cutoff = OpenCutoff;
        }

        Deck CreateDeck(MusicTrack track)
        {
            var deck = new Deck { track = track, main = NewSource(track.clip, track.loop), startedAt = Time.unscaledTime };
            foreach (var stem in track.stems)
                if (stem.clip) deck.stems.Add((stem, NewSource(stem.clip, track.loop), Game.Progress.IsRestored(stem.color) ? 1f : 0f));

            if (deck.stems.Count == 0)
            {
                deck.main.Play(); // waits for a streamed / background-loaded clip on its own
                return deck;
            }

            // Stems: load everything first, then start on the same DSP tick so they stay sample-locked.
            deck.main.clip.LoadAudioData();
            foreach (var (_, src, _) in deck.stems) src.clip.LoadAudioData();
            double start = AudioSettings.dspTime + 0.2;
            deck.main.PlayScheduled(start);
            foreach (var (_, src, _) in deck.stems) src.PlayScheduled(start);
            return deck;
        }

        AudioSource NewSource(AudioClip clip, bool loop)
        {
            var go = new GameObject(clip.name);
            go.transform.SetParent(musicRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.playOnAwake = false;
            src.volume = 0f;
            src.ignoreListenerPause = true;
            var lp = go.AddComponent<AudioLowPassFilter>(); // after the AudioSource: Unity requires one first
            lp.cutoffFrequency = cutoff;
            lowPasses.Add(lp);
            return src;
        }

        static void FadeTo(Deck d, float target, float seconds)
        {
            d.target = target;
            d.speed = seconds > 0f ? 1f / seconds : float.PositiveInfinity;
        }

        void Release(Deck d)
        {
            Free(d.main);
            foreach (var (_, src, _) in d.stems) Free(src);
        }

        void Free(AudioSource src)
        {
            lowPasses.Remove(src.GetComponent<AudioLowPassFilter>());
            Destroy(src.gameObject);
        }

        // ---------- SFX ----------

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip) sfx.PlayOneShot(clip, volume);
        }
    }
}
