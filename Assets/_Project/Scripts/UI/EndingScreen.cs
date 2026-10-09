using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The ending (IMGUI, UiKit look). Plays the ending dialogue over a beating heart, then shows the game's title,
    /// in color now (GlitchTitle: with every color restored it barely glitches), "the world is in color again" and a
    /// Credits button (click it; Z / Enter work too, without a hint). The credits roll up the screen and end on the heart,
    /// "thank you for playing" and the way back to the main menu. During the roll Z / Enter skips to the end, Esc leaves.
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        [SerializeField] DialogueData endingDialogue;

        /// <summary>The credits, top to bottom: a role, then who.</summary>
        static readonly (string role, string[] names)[] Credits =
        {
            ("Programming", new[] { "Waldron Wang", "Junyoung Oh", "Arjun Prinjha" }),
            ("Art", new[] { "Jaden Nhan" }),
            ("Music", new[] { "Regis Gurung" }),
        };

        const float RollSpeed = 70f; // canvas units per second
        const int RoleSize = 24, NameSize = 40;

        readonly GlitchTitle title = new();
        bool done, rolling;
        float doneAt = -1f, openedAt = -1f, creditsAt = -1f, pressedAt = -1f;

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
            if (!done || !Game.Input) return;
            var intent = Game.Input.Intent;
            if (!rolling)
            {
                // Not the confirm that closed the last line.
                if (Now - doneAt > 0.6f && intent.confirmPressed) StartCredits();
                return;
            }
            if (intent.pausePressed) Game.Manager.ReturnToMenu();
            else if (intent.confirmPressed && Now - pressedAt > 0.4f)
            {
                pressedAt = Now; // a quick double press skips, it doesn't also leave
                if (RollEnded) Game.Manager.ReturnToMenu();
                else creditsAt = Now - RollLength / RollSpeed; // skip to the end
            }
        }

        void StartCredits()
        {
            rolling = true;
            creditsAt = pressedAt = Now;
        }

        Rect Button => new(960f - 220f, 850f, 440f, 80f);
        Rect MenuButton => new(960f - 220f, 900f, 440f, 80f);

        // The roll: the game's title, then each role and its names, then the heart and a thank-you, which stops at Ending.
        // Offsets are from the top of the roll; it starts just below the screen and moves up until Ending reaches EndY.
        const float StartY = 1120f, EndY = 300f;
        static float Ending
        {
            get
            {
                float y = 260f; // title + gap
                foreach (var (_, names) in Credits) y += 56f + names.Length * 58f + 90f;
                return y + 60f;
            }
        }
        static float RollLength => StartY - EndY + Ending;
        float Roll => Mathf.Min((Now - creditsAt) * RollSpeed, RollLength);
        bool RollEnded => rolling && Roll >= RollLength;

        void OnGUI()
        {
            if (openedAt < 0f) return;
            GUI.depth = 20; // the dialogue box draws over it
            var e = Event.current;
            var full = UiKit.Fill();
            var view = UiKit.Fit();
            if (done && e.type == EventType.MouseDown && e.button == 0)
            {
                if (!rolling && Button.Contains(view.Mouse)) StartCredits();
                else if (RollEnded && MenuButton.Contains(view.Mouse)) Game.Manager.ReturnToMenu();
                else return;
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
            // The title screen fades out as the credits start.
            float stay = rolling ? 1f - UiKit.Smooth((Now - creditsAt) / 0.6f) : 1f;
            if (stay > 0f)
            {
                float rise = done ? UiKit.Smooth((Now - doneAt) / 0.8f) : 0f;
                var heart = new Rect(960f - 120f - beat * 4f, Mathf.Lerp(340f, 150f, rise) - beat * 4f, 240f + beat * 8f, 224f + beat * 8f);
                CardGui.Alpha = stay;
                CardGui.Glow(heart.center, 300f, UiKit.WithAlpha(Color.white, 0.08f + 0.06f * beat));
                CardGui.Alpha = UiKit.Smooth((Now - openedAt) / 1f) * stay;
                title.Heart(heart, Now, UiKit.WithAlpha(Color.white, 0.6f), beat);

                if (done)
                {
                    CardGui.Alpha = rise * stay;
                    title.Draw(view, new Vector2(960f, 520f), 88, Now, UiKit.Night);
                    CardGui.Alpha = rise * stay;
                    UiKit.Label(new Rect(0f, 596f, 1920f, 48f), "THE WORLD IS IN COLOR AGAIN", UiKit.TextTitle, UiKit.TextColor, TextAnchor.MiddleCenter, true, true);
                    for (int i = 0; i < UiKit.Spectrum.Length; i++)
                        UiKit.Gem(new Vector2(960f + (i - 3) * 46f, 700f), 28f, UiKit.Accent(UiKit.Spectrum[i]), true, 0.3f * beat);

                    CardGui.Alpha = UiKit.Smooth((Now - doneAt - 0.8f) / 0.4f) * stay;
                    UiKit.Button(Button, "Credits", UiKit.Neutral, true, true, 28, Now);
                }
            }
            if (rolling) DrawCredits(view, beat);
            UiKit.End();
        }

        void DrawCredits(UiKit.View view, float beat)
        {
            float top = StartY - Roll;
            float y = top;
            if (Visible(y + 60f, out float a))
            {
                CardGui.Alpha = a;
                title.Draw(view, new Vector2(960f, y + 60f), 64, Now, UiKit.Night);
            }
            y += 260f;
            for (int k = 0; k < Credits.Length; k++)
            {
                var (role, names) = Credits[k];
                if (Visible(y + 20f, out a))
                {
                    CardGui.Alpha = a;
                    UiKit.Label(new Rect(0f, y, 1920f, 40f), role.ToUpperInvariant(), RoleSize, UiKit.Accent(UiKit.Spectrum[k * 2 % UiKit.Spectrum.Length]), TextAnchor.MiddleCenter, true, true);
                }
                y += 56f;
                foreach (var name in names)
                {
                    if (Visible(y + 25f, out a))
                    {
                        CardGui.Alpha = a;
                        UiKit.Label(new Rect(0f, y, 1920f, 50f), name, NameSize, UiKit.TextColor);
                    }
                    y += 58f;
                }
                y += 90f;
            }

            // The end: the heart, a thank-you and the spectrum, held in the middle of the screen.
            y = top + Ending;
            if (Visible(y + 75f, out a))
            {
                var heart = new Rect(960f - 80f - beat * 3f, y - beat * 3f, 160f + beat * 6f, 150f + beat * 6f);
                CardGui.Alpha = a;
                CardGui.Glow(heart.center, 220f, UiKit.WithAlpha(Color.white, 0.06f + 0.05f * beat));
                title.Heart(heart, Now, UiKit.WithAlpha(Color.white, 0.6f), beat);
            }
            if (Visible(y + 214f, out a))
            {
                CardGui.Alpha = a;
                UiKit.Label(new Rect(0f, y + 190f, 1920f, 48f), "THANK YOU FOR PLAYING", UiKit.TextTitle, UiKit.TextColor, TextAnchor.MiddleCenter, true, true);
            }
            if (Visible(y + 290f, out a))
            {
                CardGui.Alpha = a;
                for (int i = 0; i < UiKit.Spectrum.Length; i++)
                    UiKit.Gem(new Vector2(960f + (i - 3) * 46f, y + 290f), 28f, UiKit.Accent(UiKit.Spectrum[i]), true, 0.3f * beat);
            }

            if (RollEnded)
            {
                CardGui.Alpha = UiKit.Smooth((Now - creditsAt - RollLength / RollSpeed) / 0.4f);
                UiKit.Button(MenuButton, "Main menu", UiKit.Neutral, true, true, 28, Now);
            }
            else
            {
                CardGui.Alpha = UiKit.Smooth((Now - creditsAt - 1f) / 0.5f);
                UiKit.Hint(new Vector2(960f, 1040f), "[Z] / [Enter] skip      [Esc] menu", UiKit.Neutral, 18, 0.7f);
            }
        }

        /// <summary>Whether a line centered at canvas height `y` is on screen, and how faded it is near the edges.</summary>
        static bool Visible(float y, out float alpha)
        {
            alpha = UiKit.Smooth((y - 40f) / 140f) * UiKit.Smooth((1040f - y) / 140f);
            return alpha > 0f;
        }
    }
}
