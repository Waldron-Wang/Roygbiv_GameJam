using UnityEngine;

namespace Roygbiv
{
    /// <summary>PLACEHOLDER dialogue box (IMGUI). Listens to dialogue events only.</summary>
    public class DialogueView : MonoBehaviour
    {
        DialogueLine? current;

        void OnEnable()
        {
            GameEvents.DialogueLineShown += Show;
            GameEvents.DialogueEnded += Hide;
        }

        void OnDisable()
        {
            GameEvents.DialogueLineShown -= Show;
            GameEvents.DialogueEnded -= Hide;
        }

        void Show(DialogueLine line) => current = line;
        void Hide() => current = null;

        void OnGUI()
        {
            if (current is not { } line) return;
            var rect = new Rect(40, Screen.height - 160, Screen.width - 80, 120);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 15, rect.y + 10, rect.width - 30, rect.height - 20));
            if (!string.IsNullOrEmpty(line.speaker)) GUILayout.Label($"<b>{line.speaker}</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label(line.text);
            GUILayout.FlexibleSpace();
            GUILayout.Label("[Z / Enter]");
            GUILayout.EndArea();
        }
    }
}
