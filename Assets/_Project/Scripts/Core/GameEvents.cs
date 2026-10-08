using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>Where Indigo's Serenity is in its cycle, for the HUD. Not saved anywhere.</summary>
    public enum SerenityState { Unavailable, Ready, Active, Recharging }

    /// <summary>
    /// Global event bus: one-to-many broadcasts between systems that should not know about each other
    /// (gameplay -> UI / audio / world / save).
    ///
    /// Rules:
    ///  - Raise with the Raise* methods. Subscribe in OnEnable, unsubscribe in OnDisable. Always.
    ///  - Events describe something that HAPPENED (past tense). To ask a system to DO something,
    ///    call it directly through Game.* instead.
    /// </summary>
    public static class GameEvents
    {
        // ---------- Level flow ----------
        public static event Action<ColorId> LevelStarted;
        public static event Action<ColorId> LevelCompleted;
        public static event Action<ColorId> LevelFailed;

        // ---------- Progression ----------
        public static event Action<ColorId> ColorRestored;
        public static event Action<AbilityId> AbilityUnlocked;
        /// <summary>Temporarily take an ability away from the player (Green boss).</summary>
        public static event Action<AbilityId> AbilityStolen;
        public static event Action<AbilityId> AbilityReturned;
        /// <summary>Serenity's state and how far along it is: Active = time left (1 -> 0), Recharging = refilled (0 -> 1),
        /// Ready = 1, Unavailable = the player doesn't have it. Raised every frame while active or recharging.</summary>
        public static event Action<SerenityState, float> SerenityChanged;
        /// <summary>Serenity was pressed but can't be used yet ("not ready" blip).</summary>
        public static event Action SerenityDenied;

        // ---------- Combat ----------
        public static event Action<int, int> PlayerHealthChanged; // current, max
        public static event Action PlayerDied;
        /// <summary>The boss reveal cutscene began (arena gate): the player is frozen, the camera heads for the boss.</summary>
        public static event Action<BossBase> BossIntroStarted;
        /// <summary>The reveal's big moment: the camera is on the boss (the boss music starts here).</summary>
        public static event Action<BossBase> BossRevealed;
        public static event Action<BossBase> BossFightStarted;
        public static event Action<BossBase> BossHealthChanged;
        public static event Action<BossBase> BossPhaseChanged;
        public static event Action<BossBase> BossDefeated;

        // ---------- Dialogue ----------
        public static event Action<DialogueData> DialogueStarted;
        public static event Action<DialogueLine> DialogueLineShown;
        public static event Action DialogueEnded;

        // ---------- Instructions (how-to cards, opened from the Tip button) ----------
        public static event Action<ColorId, InstructionData> InstructionShown;
        public static event Action InstructionClosed;

        // ---------- Meta ----------
        public static event Action<string> SceneLoaded;
        public static event Action<bool> PauseChanged;
        /// <summary>A big title should flash on screen (a boss's name, a phase): title, subtitle (may be empty).</summary>
        public static event Action<string, string> TitleCardShown;
        /// <summary>A cinematic took the controls (true) or gave them back (false): letterbox bars, Serenity ends.</summary>
        public static event Action<bool> CinematicChanged;
        /// <summary>A full-screen wipe in this color over this many seconds: covered at the halfway point (a scene cut).</summary>
        public static event Action<UnityEngine.Color, float> ScreenWipe;

        public static void RaiseLevelStarted(ColorId c) => LevelStarted?.Invoke(c);
        public static void RaiseLevelCompleted(ColorId c) => LevelCompleted?.Invoke(c);
        public static void RaiseLevelFailed(ColorId c) => LevelFailed?.Invoke(c);

        public static void RaiseColorRestored(ColorId c) => ColorRestored?.Invoke(c);
        public static void RaiseAbilityUnlocked(AbilityId a) => AbilityUnlocked?.Invoke(a);
        public static void RaiseAbilityStolen(AbilityId a) => AbilityStolen?.Invoke(a);
        public static void RaiseAbilityReturned(AbilityId a) => AbilityReturned?.Invoke(a);
        public static void RaiseSerenityChanged(SerenityState s, float fraction) => SerenityChanged?.Invoke(s, fraction);
        public static void RaiseSerenityDenied() => SerenityDenied?.Invoke();

        public static void RaisePlayerHealthChanged(int current, int max) => PlayerHealthChanged?.Invoke(current, max);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseBossIntroStarted(BossBase b) => BossIntroStarted?.Invoke(b);
        public static void RaiseBossRevealed(BossBase b) => BossRevealed?.Invoke(b);
        public static void RaiseBossFightStarted(BossBase b) => BossFightStarted?.Invoke(b);
        public static void RaiseBossHealthChanged(BossBase b) => BossHealthChanged?.Invoke(b);
        public static void RaiseBossPhaseChanged(BossBase b) => BossPhaseChanged?.Invoke(b);
        public static void RaiseBossDefeated(BossBase b) => BossDefeated?.Invoke(b);

        public static void RaiseDialogueStarted(DialogueData d) => DialogueStarted?.Invoke(d);
        public static void RaiseDialogueLineShown(DialogueLine l) => DialogueLineShown?.Invoke(l);
        public static void RaiseDialogueEnded() => DialogueEnded?.Invoke();

        public static void RaiseInstructionShown(ColorId c, InstructionData d) => InstructionShown?.Invoke(c, d);
        public static void RaiseInstructionClosed() => InstructionClosed?.Invoke();

        public static void RaiseSceneLoaded(string scene) => SceneLoaded?.Invoke(scene);
        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
        public static void RaiseTitleCardShown(string title, string subtitle) => TitleCardShown?.Invoke(title, subtitle);
        public static void RaiseCinematicChanged(bool playing) => CinematicChanged?.Invoke(playing);
        public static void RaiseScreenWipe(UnityEngine.Color color, float seconds) => ScreenWipe?.Invoke(color, seconds);

        // Domain reload is disabled in this project (faster Play mode), so statics survive between
        // play sessions. Wipe all subscribers at the start of each session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            LevelStarted = null; LevelCompleted = null; LevelFailed = null;
            ColorRestored = null; AbilityUnlocked = null; AbilityStolen = null; AbilityReturned = null;
            SerenityChanged = null; SerenityDenied = null;
            PlayerHealthChanged = null; PlayerDied = null;
            BossIntroStarted = null; BossRevealed = null;
            BossFightStarted = null; BossHealthChanged = null; BossPhaseChanged = null; BossDefeated = null;
            DialogueStarted = null; DialogueLineShown = null; DialogueEnded = null;
            InstructionShown = null; InstructionClosed = null;
            SceneLoaded = null; PauseChanged = null; TitleCardShown = null;
            CinematicChanged = null; ScreenWipe = null;
        }
    }
}
