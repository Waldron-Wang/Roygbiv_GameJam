using UnityEngine;

namespace Roygbiv
{
    /// <summary>PLACEHOLDER main menu (IMGUI). Lives in the MainMenu scene. Calls GameManager commands.</summary>
    public class MainMenuScreen : MonoBehaviour
    {
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 110, Screen.height / 2f - 90, 220, 180), GUI.skin.box);
            GUILayout.Label("R O Y G B I V");
            GUILayout.Space(10);
            if (GameProgress.HasSave && GUILayout.Button("Continue")) Game.Manager.ContinueGame();
            if (GUILayout.Button("New Game")) Game.Manager.NewGame();
            if (GUILayout.Button("Quit")) Application.Quit();
            GUILayout.EndArea();
        }
    }
}
