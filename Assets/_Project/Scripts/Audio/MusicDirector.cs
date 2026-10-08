using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// Decides WHAT music plays. It only listens (scene loads + GameEvents) and tells Game.Audio, so gameplay
    /// never has to mention music. Where each scene's track comes from, first match wins:
    ///   1. a SceneMusic component in the scene (plays exactly what it says; empty = silence)
    ///   2. GameConfig.menuMusic / hubMusic / endingMusic for those scenes
    ///   3. the ColorData whose sceneName is this scene: levelMusic, then bossMusic once the fight starts
    /// Nothing found = silence (the Sandbox).
    /// Boss intro (ColorData.muffleUntilBoss): until the fight starts the music is quiet and muffled, as if heard through
    /// a wall. When the fight starts (the arena gate shuts) it opens up, or with restartOnBoss: fade out -> a beat of
    /// silence -> the boss song fades in from the top (timings in GameConfig).
    /// With a boss reveal cutscene (BossIntro) the same happens on its beats instead: BossIntroStarted fades the intro
    /// out, BossRevealed (camera on the boss) fades the boss song in from the top; the cutscene's letterbox doesn't duck it.
    /// Also: boss defeated -> fade out; color restored -> stinger; dialogue / Tip card / cinematic -> duck; pause -> muffle.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        const string Dialogue = "dialogue", Instruction = "instruction", Cinematic = "cinematic", Pause = "pause", Intro = "intro";
        const float MinIntroSeconds = 1f;  // a fight that starts sooner than this just opens up (no stutter at level start)

        MusicTrack sceneMusic, bossMusic;
        bool muffleUntilBoss, restartOnBoss;
        bool inIntro;
        bool bossIntroPlaying, revealed;
        float introStartedAt;
        Coroutine reveal;
        Scene resolvedScene; // the scene whose music was last picked

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.BossIntroStarted += OnBossIntroStarted;
            GameEvents.BossRevealed += OnBossRevealed;
            GameEvents.BossFightStarted += OnBossFightStarted;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.ColorRestored += OnColorRestored;
            GameEvents.DialogueStarted += OnDialogueStarted;
            GameEvents.DialogueEnded += OnDialogueEnded;
            GameEvents.InstructionShown += OnInstructionShown;
            GameEvents.InstructionClosed += OnInstructionClosed;
            GameEvents.CinematicChanged += OnCinematicChanged;
            GameEvents.PauseChanged += OnPauseChanged;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.BossIntroStarted -= OnBossIntroStarted;
            GameEvents.BossRevealed -= OnBossRevealed;
            GameEvents.BossFightStarted -= OnBossFightStarted;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.ColorRestored -= OnColorRestored;
            GameEvents.DialogueStarted -= OnDialogueStarted;
            GameEvents.DialogueEnded -= OnDialogueEnded;
            GameEvents.InstructionShown -= OnInstructionShown;
            GameEvents.InstructionClosed -= OnInstructionClosed;
            GameEvents.CinematicChanged -= OnCinematicChanged;
            GameEvents.PauseChanged -= OnPauseChanged;
        }

        // The scene you press Play in doesn't always raise sceneLoaded (Enter Play Mode Options), so catch it here.
        void Start()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene == resolvedScene) return;
            OnSceneLoaded(scene, LoadSceneMode.Single);
            // A boss that starts immediately may already have raised BossFightStarted before this ran.
            var boss = LevelController.Current ? LevelController.Current.Boss : null;
            if (boss && boss.IsFighting) OnBossFightStarted(boss);
        }

        // Runs after the new scene's Awake/OnEnable and before its Start.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            resolvedScene = scene;
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            bossIntroPlaying = revealed = false;
            Game.Audio.Unduck(Cinematic); // the cinematic's owner went away with the old scene
            Resolve(scene.name);
            SetIntro(muffleUntilBoss && sceneMusic); // also re-muffles after a death mid-fight, while the level restarts
            Debug.Log($"[Music] {scene.name}: {Describe(sceneMusic)}{(bossMusic ? $" (boss: {Describe(bossMusic)})" : "")}" +
                      (inIntro ? "  muffled until the boss" : ""));
            Game.Audio.PlayMusic(sceneMusic);
        }

        void SetIntro(bool on)
        {
            inIntro = on;
            if (on)
            {
                introStartedAt = Time.unscaledTime;
                Game.Audio.Duck(Intro, Game.Config.introMusicVolume);
                Game.Audio.Muffle(Intro);
            }
            else
            {
                Game.Audio.Unduck(Intro);
                Game.Audio.Unmuffle(Intro);
            }
        }

        void Resolve(string sceneName)
        {
            var config = Game.Config;
            var overrideMusic = FindAnyObjectByType<SceneMusic>();
            if (overrideMusic)
            {
                sceneMusic = overrideMusic.Music;
                bossMusic = overrideMusic.BossMusic;
                muffleUntilBoss = overrideMusic.MuffleUntilBoss;
                restartOnBoss = overrideMusic.RestartOnBoss;
                return;
            }

            bossMusic = null;
            muffleUntilBoss = restartOnBoss = false;
            if (sceneName == config.mainMenuScene) sceneMusic = config.menuMusic;
            else if (sceneName == config.hubScene) sceneMusic = config.hubMusic;
            else if (sceneName == config.endingScene) sceneMusic = config.endingMusic;
            else
            {
                var color = config.colorOrder.Find(c => c != null && c.sceneName == sceneName);
                if (!color) { sceneMusic = null; return; }
                sceneMusic = color.levelMusic;
                bossMusic = color.bossMusic;
                muffleUntilBoss = color.muffleUntilBoss;
                restartOnBoss = color.restartOnBoss;
            }
        }

        static string Describe(MusicTrack t) =>
            !t ? "silence" : !t.clip ? $"{t.name} has NO CLIP (silence)" : $"{t.name} ({t.clip.name})";

        // BossIntroStarted is raised before the cutscene's CinematicChanged(true), so the letterbox won't duck the reveal.
        void OnBossIntroStarted(BossBase _)
        {
            bossIntroPlaying = true;
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            Game.Audio.StopMusic(Game.Config.bossMusicFadeOut);
        }

        void OnBossRevealed(BossBase _)
        {
            revealed = true;
            inIntro = false;
            Game.Audio.Unduck(Intro, instant: true);
            Game.Audio.Unmuffle(Intro, instant: true);
            Game.Audio.PlayMusic(bossMusic ? bossMusic : sceneMusic, Game.Config.bossMusicFadeIn, restart: true);
        }

        void OnBossFightStarted(BossBase _)
        {
            bossIntroPlaying = false;
            if (revealed) return; // the reveal already started the boss song
            var track = bossMusic ? bossMusic : sceneMusic;
            bool restart = inIntro && restartOnBoss && track && Time.unscaledTime - introStartedAt >= MinIntroSeconds;
            if (restart) { reveal = StartCoroutine(RestartForBoss(track)); return; }

            // Open up smoothly; a separate bossMusic crossfades in and sweeps open as it arrives.
            SetIntro(false);
            Game.Audio.PlayMusic(track);
        }

        // Muffled intro fades out as the gate shuts -> silence -> the boss song fades in from the top, clear and full.
        IEnumerator RestartForBoss(MusicTrack track)
        {
            var config = Game.Config;
            inIntro = false;
            Game.Audio.StopMusic(config.bossMusicFadeOut);
            yield return new WaitForSecondsRealtime(config.bossMusicFadeOut + config.bossMusicSilence);
            Game.Audio.Unduck(Intro, instant: true);
            Game.Audio.Unmuffle(Intro, instant: true);
            Game.Audio.PlayMusic(track, config.bossMusicFadeIn, restart: true);
            reveal = null;
        }

        void OnBossDefeated(BossBase _)
        {
            if (Game.Config.stopMusicOnBossDefeated) Game.Audio.StopMusic();
        }

        void OnColorRestored(ColorId _) => Game.Audio.PlaySfx(Game.Config.colorRestoredStinger);

        void OnDialogueStarted(DialogueData _) => Game.Audio.Duck(Dialogue);
        void OnDialogueEnded() => Game.Audio.Unduck(Dialogue);
        void OnInstructionShown(ColorId _, InstructionData __) => Game.Audio.Duck(Instruction);
        void OnInstructionClosed() => Game.Audio.Unduck(Instruction);

        void OnCinematicChanged(bool playing)
        {
            if (playing && bossIntroPlaying) return; // the boss reveal wants the music loud
            if (playing) Game.Audio.Duck(Cinematic);
            else Game.Audio.Unduck(Cinematic);
        }

        void OnPauseChanged(bool paused)
        {
            if (paused) { Game.Audio.Duck(Pause); Game.Audio.Muffle(Pause); }
            else { Game.Audio.Unduck(Pause); Game.Audio.Unmuffle(Pause); }
        }
    }
}
