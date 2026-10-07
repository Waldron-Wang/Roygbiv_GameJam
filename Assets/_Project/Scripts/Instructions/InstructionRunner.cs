using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// The optional how-to card for the current level. Nothing pops up on its own: during a level the player can
    /// open the color's card (ColorData.instruction) from the on-screen Tip button, as often as they like.
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
        [Tooltip("Confirm / Esc are ignored for this long (unscaled) after the card opens.")]
        [SerializeField] float minShowTime = 0.25f;

        ColorId levelColor;
        Scene levelScene;
        bool inLevel, paused, cinematic;
        float openedAt;
        int closedFrame = -1;

        public bool IsOpen { get; private set; }

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
        public bool CanOpen => !IsOpen && LevelCard && !paused && !cinematic && Game.Input.GameplayEnabled
                               && !Game.Dialogue.IsPlaying && !Game.Scenes.IsLoading;

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
        }

        void OnDisable()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelCompleted -= OnLevelEnded;
            GameEvents.LevelFailed -= OnLevelEnded;
            GameEvents.PauseChanged -= OnPauseChanged;
            GameEvents.CinematicChanged -= OnCinematicChanged;
            GameEvents.SceneLoaded -= OnSceneLoaded;
        }

        void OnLevelStarted(ColorId color)
        {
            levelColor = color;
            levelScene = SceneManager.GetActiveScene(); // a reload or another scene is a different one
            inLevel = true;
        }

        void OnCinematicChanged(bool playing) => cinematic = playing;
        void OnSceneLoaded(string _) => cinematic = false;

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
