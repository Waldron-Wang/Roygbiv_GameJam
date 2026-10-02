using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Plays DialogueData line by line and blocks gameplay input meanwhile.
    /// It only raises events — any view (DialogueView now, a styled UI later) draws the lines.
    ///   yield return Game.Dialogue.Play(data);   // from a coroutine: waits until finished
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }

        public Coroutine Play(DialogueData data) => StartCoroutine(Run(data));

        IEnumerator Run(DialogueData data)
        {
            while (IsPlaying) yield return null; // queue behind any running dialogue
            if (data == null || data.lines.Count == 0) yield break;

            IsPlaying = true;
            Game.Input.BlockGameplay();
            GameEvents.RaiseDialogueStarted(data);

            foreach (var line in data.lines)
            {
                GameEvents.RaiseDialogueLineShown(line);
                yield return null; // don't let the key that opened it also skip line 1
                while (!Game.Input.Intent.confirmPressed) yield return null;
            }

            Game.Input.UnblockGameplay();
            IsPlaying = false;
            GameEvents.RaiseDialogueEnded();
        }
    }
}
