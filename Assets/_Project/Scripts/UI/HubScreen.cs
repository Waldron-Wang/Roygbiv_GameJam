using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// PLACEHOLDER hub / district select (IMGUI). Lives in the Hub scene.
    /// Later this can become a walkable city where each district door calls Game.Manager.EnterLevel(color).
    /// </summary>
    public class HubScreen : MonoBehaviour
    {
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 160, 60, 320, 400), GUI.skin.box);
            GUILayout.Label("The Gray City — choose a district");
            GUILayout.Space(8);

            foreach (var data in Game.Config.colorOrder)
            {
                bool restored = Game.Progress.IsRestored(data.id);
                bool unlocked = Game.Manager.IsUnlocked(data.id);
                GUI.enabled = unlocked;
                var label = $"{data.displayName} — {data.emotion}" + (restored ? "  ✓" : unlocked ? "" : "  (locked)");
                if (GUILayout.Button(label)) Game.Manager.EnterLevel(data.id);
            }
            GUI.enabled = true;

            GUILayout.Space(8);
            if (GUILayout.Button("Main menu")) Game.Manager.ReturnToMenu();
            GUILayout.EndArea();
        }
    }
}
