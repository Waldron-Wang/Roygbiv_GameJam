using UnityEngine;

namespace Roygbiv
{
    /// <summary>PLACEHOLDER pause menu (IMGUI). Esc / Start toggles while in a level.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public bool IsPaused { get; private set; }

        void Update()
        {
            if (!Game.Input || !Game.Scenes || !Game.Instructions) return;
            // Not over the instruction card: it holds its own pause while open, and the Esc that closes it shouldn't also pause.
            if (Game.Input.Intent.pausePressed && LevelController.Current != null && !Game.Scenes.IsLoading && !Game.Instructions.BlocksPause)
                SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            if (paused)
            {
                Game.Time.Pause(this); // through Game.Time, so resuming brings back any slow motion (Serenity)
                Game.Input.BlockGameplay();
            }
            else
            {
                Game.Time.Resume(this);
                Game.Input.UnblockGameplay();
            }
            GameEvents.RaisePauseChanged(paused);
        }

        void OnGUI()
        {
            if (!IsPaused) return;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 100, Screen.height / 2f - 70, 200, 140), GUI.skin.box);
            GUILayout.Label("Paused");
            if (GUILayout.Button("Resume")) SetPaused(false);
            if (GUILayout.Button("Restart level")) { SetPaused(false); Game.Scenes.Reload(); }
            if (GUILayout.Button("Back to hub")) { SetPaused(false); Game.Manager.ReturnToHub(); }
            GUILayout.EndArea();
        }
    }
}
