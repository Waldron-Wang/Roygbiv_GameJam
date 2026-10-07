using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The dialogue box (IMGUI, UiKit look). Listens to dialogue events only: DialogueRunner owns the lines and the
    /// confirm press. A panel along the bottom in the level's color (neutral outside levels): the speaker in a header
    /// strip, the line (rich text: story fragments color their color word) wrapped to fit, and a blinking
    /// [Z] / [Enter] hint. Each line slides in; there's no typewriter, so the confirm that advances never skips text.
    /// </summary>
    public class DialogueView : MonoBehaviour
    {
        const int TextSize = 32;

        DialogueLine? current;
        float shownAt;

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

        void Show(DialogueLine line)
        {
            current = line;
            shownAt = Time.unscaledTime;
        }

        void Hide() => current = null;

        void OnGUI()
        {
            if (current is not { } line || Event.current.type != EventType.Repaint) return;
            GUI.depth = -50; // over the HUD
            var view = UiKit.Fill();
            view.Begin();
            var accent = UiKit.CurrentAccent;
            float t = Time.unscaledTime - shownAt;
            float enter = UiKit.Smooth(t / 0.18f);

            bool speaker = !string.IsNullOrEmpty(line.speaker);
            float width = Mathf.Min(1480f, view.Rect.width - 120f);
            float textWidth = width - 120f;
            float textHeight = Mathf.Max(TextSize * 1.4f, CardGui.WrappedHeight(line.text ?? "", TextSize, textWidth));
            float height = (speaker ? 46f : 0f) + 34f + textHeight + 54f;
            var panel = new Rect(view.Rect.center.x - width * 0.5f, view.Rect.height - height - 44f + (1f - enter) * 24f, width, height);

            CardGui.Alpha = enter;
            UiKit.Panel(panel, accent, Time.unscaledTime, 24f);
            float y = panel.y + 26f;
            if (speaker)
            {
                UiKit.Header(panel, line.speaker, accent, 46f, 22);
                y += 46f;
            }
            CardGui.Text(new Rect(panel.x + 60f, y, textWidth, textHeight), line.text, TextSize, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true);

            // Continue: blinking, with a chevron nudging down.
            const string hint = "[Z] / [Enter]";
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            float hw = UiKit.HintWidth(hint, 18);
            var at = new Vector2(panel.xMax - 70f - hw * 0.5f, panel.yMax - 30f);
            UiKit.Hint(at, hint, accent, 18, Mathf.Clamp01((t - 0.2f) / 0.2f) * blink);
            var tip = new Vector2(panel.xMax - 40f, panel.yMax - 26f + Mathf.Sin(Time.unscaledTime * 5f) * 2f);
            CardGui.Line(tip + new Vector2(-8f, -7f), tip, 2.5f, UiKit.WithAlpha(accent, blink));
            CardGui.Line(tip + new Vector2(8f, -7f), tip, 2.5f, UiKit.WithAlpha(accent, blink));
            UiKit.End();
        }
    }
}
