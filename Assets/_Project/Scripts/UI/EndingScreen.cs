using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The ending (IMGUI, UiKit look). Plays the ending dialogue over a beating heart, then shows the game's title,
    /// in color now (GlitchTitle: with every color restored it barely glitches), "the world is in color again" and a way
    /// back to the main menu (click it; Z / Enter work too, without a hint).
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        [SerializeField] DialogueData endingDialogue;

        readonly GlitchTitle title = new();
        bool done;
        float doneAt = -1f, openedAt = -1f;

        static float Now => Time.unscaledTime;

        IEnumerator Start()
        {
            openedAt = Now;
            if (endingDialogue) yield return Game.Dialogue.Play(endingDialogue);
            done = true;
            doneAt = Now;
        }

        void Update()
        {
            // Not the confirm that closed the last line.
            if (done && Now - doneAt > 0.6f && Game.Input && Game.Input.Intent.confirmPressed) Game.Manager.ReturnToMenu();
        }

        Rect Button => new(960f - 220f, 850f, 440f, 80f);

        void OnGUI()
        {
            if (openedAt < 0f) return;
            GUI.depth = 20; // the dialogue box draws over it
            var e = Event.current;
            var full = UiKit.Fill();
            var view = UiKit.Fit();
            if (done && e.type == EventType.MouseDown && e.button == 0 && Button.Contains(view.Mouse))
            {
                Game.Manager.ReturnToMenu();
                e.Use();
                return;
            }
            if (e.type != EventType.Repaint) return;
            title.Tick(Now);

            // Every color's back: the night picks them all up.
            full.Begin();
            var palette = UiKit.RestoredColors();
            UiKit.Gradient(full.Rect, UiKit.Night, UiKit.Backdrop(UiKit.Average(palette, UiKit.Gray), 0.18f));
            for (int i = 0; i < UiKit.Spectrum.Length; i++)
            {
                float x = full.Rect.width * (i + 0.5f) / UiKit.Spectrum.Length + Mathf.Sin(Now * 0.25f + i) * 50f;
                CardGui.Glow(new Vector2(x, full.Rect.height * 0.95f), full.Rect.height * 0.45f, UiKit.WithAlpha(UiKit.Accent(UiKit.Spectrum[i]), 0.08f));
            }
            UiKit.Dust(full.Rect, Now, 80, UiKit.Gray, palette.Count > 0 ? palette : null, 0.22f);
            UiKit.Scanlines(full.Rect, new Color(1f, 1f, 1f, 0.015f), 4f, 1f);
            UiKit.Vignette(full.Rect, new Color(0f, 0f, 0f, 0.6f));

            view.Begin();
            float beat = UiKit.Heartbeat(Now);
            float rise = done ? UiKit.Smooth((Now - doneAt) / 0.8f) : 0f;
            var heart = new Rect(960f - 120f - beat * 4f, Mathf.Lerp(340f, 150f, rise) - beat * 4f, 240f + beat * 8f, 224f + beat * 8f);
            CardGui.Glow(heart.center, 300f, UiKit.WithAlpha(Color.white, 0.08f + 0.06f * beat));
            CardGui.Alpha = UiKit.Smooth((Now - openedAt) / 1f);
            title.Heart(heart, Now, UiKit.WithAlpha(Color.white, 0.6f), beat);

            if (done)
            {
                CardGui.Alpha = rise;
                title.Draw(view, new Vector2(960f, 520f), 88, Now, UiKit.Night);
                CardGui.Alpha = rise;
                UiKit.Label(new Rect(0f, 596f, 1920f, 48f), "THE WORLD IS IN COLOR AGAIN", UiKit.TextTitle, UiKit.TextColor, TextAnchor.MiddleCenter, true, true);
                for (int i = 0; i < UiKit.Spectrum.Length; i++)
                    UiKit.Gem(new Vector2(960f + (i - 3) * 46f, 700f), 28f, UiKit.Accent(UiKit.Spectrum[i]), true, 0.3f * beat);

                CardGui.Alpha = UiKit.Smooth((Now - doneAt - 0.8f) / 0.4f);
                UiKit.Button(Button, "Main menu", UiKit.Neutral, true, true, 28, Now);
            }
            UiKit.End();
        }
    }
}
