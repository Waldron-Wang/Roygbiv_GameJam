using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Shows a pre-boss instruction card and waits for the player to confirm. Meanwhile gameplay input is
    /// blocked and time is frozen (the Orange chase must not scroll under the card). Like DialogueRunner it
    /// only raises events; InstructionView (or any later UI) draws the card, animating on unscaled time.
    ///   yield return Game.Instructions.Show(color, data, this);   // from a coroutine: waits until closed
    ///
    /// Each color's card shows once per play session, so retries after a death go straight to the fight.
    /// GameManager.NewGame calls ForgetShown (F10 goes through NewGame too).
    /// </summary>
    public class InstructionRunner : MonoBehaviour
    {
        [Tooltip("Confirm is ignored for this long (unscaled) after a card opens, so a key still held from before doesn't skip it.")]
        [SerializeField] float minShowTime = 0.4f;

        // An instance field, not a static: [Systems] is rebuilt every Play session, so it resets itself.
        readonly HashSet<ColorId> shown = new();
        bool closeRequested;

        public bool IsShowing { get; private set; }

        public bool WasShown(ColorId color) => shown.Contains(color);
        public void ForgetShown() => shown.Clear();

        /// <summary>Shows the card unless this color's was already shown this session.</summary>
        /// <param name="owner">If this is destroyed (scene change), the card closes by itself.</param>
        public Coroutine Show(ColorId color, InstructionData data, Object owner = null) => StartCoroutine(Run(color, data, owner));

        void OnEnable()
        {
            GameEvents.LevelCompleted += OnLevelEnded;
            GameEvents.LevelFailed += OnLevelEnded;
        }

        void OnDisable()
        {
            GameEvents.LevelCompleted -= OnLevelEnded;
            GameEvents.LevelFailed -= OnLevelEnded;
        }

        // F9 etc. while a card is up: get out of the way of the reclaim / respawn flow.
        void OnLevelEnded(ColorId _)
        {
            if (IsShowing) closeRequested = true;
        }

        IEnumerator Run(ColorId color, InstructionData data, Object owner)
        {
            bool owned = owner != null;
            while (IsShowing) yield return null; // one card at a time
            if (data == null || (owned && !owner) || !shown.Add(color)) yield break;

            IsShowing = true;
            closeRequested = false;
            float timeScale = Time.timeScale;
            Time.timeScale = 0f;
            Game.Input.BlockGameplay();
            GameEvents.RaiseInstructionShown(color, data);

            float openedAt = Time.unscaledTime;
            while (!closeRequested && !(owned && !owner))
            {
                yield return null;
                if (Time.unscaledTime - openedAt >= minShowTime && Game.Input.Intent.confirmPressed) break;
            }

            Time.timeScale = timeScale;
            Game.Input.UnblockGameplay();
            IsShowing = false;
            GameEvents.RaiseInstructionClosed();
        }
    }
}
