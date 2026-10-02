using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>PLACEHOLDER ending. Plays the ending dialogue, then offers a way back.</summary>
    public class EndingScreen : MonoBehaviour
    {
        [SerializeField] DialogueData endingDialogue;

        bool done;

        IEnumerator Start()
        {
            if (endingDialogue) yield return Game.Dialogue.Play(endingDialogue);
            done = true;
        }

        void OnGUI()
        {
            if (!done) return;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 120, Screen.height / 2f - 50, 240, 100), GUI.skin.box);
            GUILayout.Label("The world is in color again.");
            if (GUILayout.Button("Main menu")) Game.Manager.ReturnToMenu();
            GUILayout.EndArea();
        }
    }
}
