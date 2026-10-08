using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// The optional how-to card for the current level. Nothing pops up on its own: during a level the player can
    /// open the color's card (ColorData.instruction) from the on-screen Tip button, as often as they like.
    /// It's earned, so it doesn't spoil a first try (InstructionData.nudge / nudgeAfterDeaths / demoAfterDeaths):
    /// deaths during the boss fight are counted per visit to the level (a reload keeps the count, any other scene
    /// clears it), and Stage says what the button offers: nothing, the nudge, or the full card. HasUnread is true
    /// while it offers more than the player has opened (the button shows a dot).
    /// While it's open, gameplay input is blocked and time is frozen (the Orange chase must not scroll) through a pause
    /// held on Game.Time; closing lifts both, which puts any slow motion (Serenity) back exactly as it was.
    /// Like DialogueRunner it only owns state and raises events: InstructionView draws the card and the button
    /// and reports clicks (Toggle / Close).
    ///
    /// Closes on: the card's X or the Tip button (view), confirm (Z / Enter) or Esc (here), the level ending,
    /// or the scene changing. PauseMenu checks BlocksPause so the Esc that closes the card doesn't also pause.
    /// </summary>
    public class InstructionRunner : MonoBehaviour
    {
        public enum TipStage { Hidden, Nudge, Demo }

        [Tooltip("Confirm / Esc are ignored for this long (unscaled) after the card opens.")]
        [SerializeField] float minShowTime = 0.25f;

        ColorId levelColor;
        Scene levelScene;
        bool inLevel, paused, cinematic;
        float openedAt;
        int closedFrame = -1;
        int deaths;
        string deathScene;             // the level the death count belongs to
        TipStage seen = TipStage.Hidden; // the most the player has opened this visit

        public bool IsOpen { get; private set; }
        /// <summary>What the open card shows (fixed while it's open).</summary>
        public TipStage OpenStage { get; private set; }

        /// <summary>The current level's card while there's one to offer: in a level that hasn't been won or lost.</summary>
        public InstructionData LevelCard
        {
            get
            {
                if (!inLevel || SceneManager.GetActiveScene() != levelScene) return null;
                var data = Game.Config.Get(levelColor);
                return data ? data.instruction : null;
            }
        }

        /// <summary>
        /// The Tip button shows (and works) only when this is true, or while the card is open. Never while something
        /// else holds the controls: dialogue, the pause menu, a cinematic (letterbox), or any other input block
        /// (Violet's intro camera pull, its arrival, the beat before a checkpoint duel).
        /// </summary>
        public bool CanOpen => !IsOpen && LevelCard && Stage != TipStage.Hidden && !paused && !cinematic && Game.Input.GameplayEnabled
                               && !Game.Dialogue.IsPlaying && !Game.Scenes.IsLoading;

        /// <summary>What the Tip button offers right now, from the deaths in this boss fight.</summary>
        public TipStage Stage
        {
            get
            {
                var card = LevelCard;
                if (!card) return TipStage.Hidden;
                var boss = LevelController.Current ? LevelController.Current.Boss : null;
                if (card.onlyDuringBossFight && !(boss && boss.IsFighting)) return TipStage.Hidden;
                if (deaths >= card.demoAfterDeaths) return TipStage.Demo;
                if (!string.IsNullOrEmpty(card.nudge) && deaths >= card.nudgeAfterDeaths) return TipStage.Nudge;
                return TipStage.Hidden;
            }
        }

        /// <summary>The button offers more than the player has opened yet.</summary>
        public bool HasUnread => Stage > seen;

        /// <summary>Deaths still needed before the full card (0 once it's offered).</summary>
        public int DeathsUntilDemo => LevelCard ? Mathf.Max(0, LevelCard.demoAfterDeaths - deaths) : 0;

        /// <summary>True while open, and on the frame it closed: the Esc that closed it must not open the pause menu.</summary>
        public bool BlocksPause => IsOpen || closedFrame == Time.frameCount;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (!CanOpen) return;
            var card = LevelCard;
            IsOpen = true;
            OpenStage = Stage;
            if (OpenStage > seen) seen = OpenStage;
            openedAt = Time.unscaledTime;
            Game.Time.Pause(this);
            Game.Input.BlockGameplay();
            GameEvents.RaiseInstructionShown(levelColor, card);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
            Game.Time.Resume(this);
            Game.Input.UnblockGameplay();
            GameEvents.RaiseInstructionClosed();
        }

        void OnEnable()
        {
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelCompleted += OnLevelEnded;
            GameEvents.LevelFailed += OnLevelEnded;
            GameEvents.PauseChanged += OnPauseChanged;
            GameEvents.CinematicChanged += OnCinematicChanged;
            GameEvents.SceneLoaded += OnSceneLoaded;
            GameEvents.PlayerDied += OnPlayerDied;
        }

        void OnDisable()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelCompleted -= OnLevelEnded;
            GameEvents.LevelFailed -= OnLevelEnded;
            GameEvents.PauseChanged -= OnPauseChanged;
            GameEvents.CinematicChanged -= OnCinematicChanged;
            GameEvents.SceneLoaded -= OnSceneLoaded;
            GameEvents.PlayerDied -= OnPlayerDied;
        }

        void OnLevelStarted(ColorId color)
        {
            levelColor = color;
            levelScene = SceneManager.GetActiveScene(); // a reload or another scene is a different one
            inLevel = true;
            if (levelScene.name != deathScene) ResetDeaths(levelScene.name);
        }

        // Only deaths in the boss fight count: a pit in the intro level says nothing about the boss.
        void OnPlayerDied()
        {
            var boss = LevelController.Current ? LevelController.Current.Boss : null;
            if (inLevel && boss && boss.IsFighting) deaths++;
        }

        void ResetDeaths(string scene)
        {
            deathScene = scene;
            deaths = 0;
            seen = TipStage.Hidden;
        }

        void OnCinematicChanged(bool playing) => cinematic = playing;
        void OnSceneLoaded(string scene)
        {
            cinematic = false;
            if (scene != deathScene) ResetDeaths(null); // left the level (hub, menu): the next visit starts fresh
        }

        // Won or lost (F9 too): no more tips, and get out of the way of the reclaim / respawn flow.
        void OnLevelEnded(ColorId _)
        {
            inLevel = false;
            Close();
        }

        void OnPauseChanged(bool isPaused) => paused = isPaused;

        void Update()
        {
            if (!IsOpen) return;
            if (Game.Scenes.IsLoading || !LevelCard) { Close(); return; } // the level went away under it

            var intent = Game.Input.Intent; // InputReader runs first, so this is this frame's
            if (Time.unscaledTime - openedAt >= minShowTime && (intent.confirmPressed || intent.pausePressed)) Close();
        }
    }
}
