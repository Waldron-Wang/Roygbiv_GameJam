using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The main menu (IMGUI, UiKit look). Lives in the MainMenu scene and only calls GameManager commands.
    ///   The title, UiKit.GameTitle, starts colorless and glitches (GlitchTitle): color bleeds in for a split second and
    ///   falls away; every restored color stays a little more, all seven = a title in full color. A heart above it
    ///   flickers the same way. A strip under it shows the seven colors.
    ///   Continue (with a save) / New Game / Quit: Up / Down + Z / Enter, or the mouse. New Game over a save asks first.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        enum Item { Continue, NewGame, Quit, ConfirmErase, Cancel }

        readonly GlitchTitle title = new();
        readonly MenuNav nav = new();
        readonly List<Item> items = new();
        int selected;
        bool confirming;
        float openedAt = -1f;
        Vector2 lastMouse;

        static float Now => Time.unscaledTime;

        void Update()
        {
            if (openedAt < 0f) openedAt = Now;
            Refresh();
            if (!Game.Input || Game.Scenes.IsLoading) return;

            int step = nav.Step(-Game.Input.Navigate.y);
            if (step != 0) selected = (selected + step + items.Count) % items.Count;
            var intent = Game.Input.Intent;
            if (intent.confirmPressed && Now - openedAt > 0.3f) Choose(items[selected]);
            else if (intent.pausePressed && confirming) SetConfirming(false);
        }

        void Refresh()
        {
            items.Clear();
            if (confirming)
            {
                items.Add(Item.ConfirmErase);
                items.Add(Item.Cancel);
            }
            else
            {
                if (GameProgress.HasSave) items.Add(Item.Continue);
                items.Add(Item.NewGame);
                items.Add(Item.Quit);
            }
            selected = Mathf.Clamp(selected, 0, items.Count - 1);
        }

        void Choose(Item item)
        {
            switch (item)
            {
                case Item.Continue: Game.Manager.ContinueGame(); break;
                case Item.NewGame:
                    if (GameProgress.HasSave) SetConfirming(true);
                    else Game.Manager.NewGame();
                    break;
                case Item.ConfirmErase: Game.Manager.NewGame(); break;
                case Item.Cancel: SetConfirming(false); break;
                case Item.Quit: Application.Quit(); break;
            }
        }

        void SetConfirming(bool on)
        {
            confirming = on;
            selected = on ? 1 : (GameProgress.HasSave ? 1 : 0); // the safe choice: Cancel; back on New Game
            Refresh();
        }

        static string Label(Item item) => item switch
        {
            Item.Continue => "Continue",
            Item.NewGame => "New game",
            Item.Quit => "Quit",
            Item.ConfirmErase => "Erase and start over",
            _ => "Cancel",
        };

        void OnGUI()
        {
            if (openedAt < 0f) return;
            var e = Event.current;
            title.Tick(Now);

            var full = UiKit.Fill();
            var view = UiKit.Fit();
            var buttons = ButtonRects();

            // Mouse: hover selects (once it moves), a click chooses.
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = 0; i < buttons.Length; i++)
                    if (buttons[i].Contains(view.Mouse)) { selected = i; Choose(items[i]); e.Use(); return; }
            }
            if (e.type != EventType.Repaint) return;
            if ((view.Mouse - lastMouse).sqrMagnitude > 1f)
                for (int i = 0; i < buttons.Length; i++)
                    if (buttons[i].Contains(view.Mouse)) selected = i;
            lastMouse = view.Mouse;

            // Backdrop: night, a faint glow of whatever color has come back, drifting dust, scanlines.
            full.Begin();
            var palette = UiKit.RestoredColors();
            var glow = UiKit.Average(palette, UiKit.Gray);
            UiKit.Gradient(full.Rect, UiKit.Night, UiKit.Backdrop(glow, 0.14f));
            CardGui.Glow(new Vector2(full.Rect.center.x, full.Rect.height * 0.3f), full.Rect.height * 0.6f, UiKit.WithAlpha(glow, 0.07f));
            UiKit.Dust(full.Rect, Now, 50, UiKit.Gray, palette.Count > 0 ? palette : null, 0.16f);
            UiKit.Scanlines(full.Rect, new Color(1f, 1f, 1f, 0.018f), 4f, 1f);
            UiKit.Vignette(full.Rect, new Color(0f, 0f, 0f, 0.7f));

            view.Begin();
            float beat = UiKit.Heartbeat(Now);
            var heart = new Rect(960f - 78f - beat * 3f, 168f - beat * 3f, 156f + beat * 6f, 146f + beat * 6f);
            CardGui.Glow(heart.center, 150f, UiKit.WithAlpha(glow, 0.12f + 0.08f * beat));
            title.Heart(heart, Now, UiKit.WithAlpha(UiKit.Neutral, 0.5f), beat);
            title.Draw(view, new Vector2(960f, 410f), 92, Now, UiKit.Night);

            // The seven colors, as a strip under the title.
            float a = CardGui.Alpha;
            CardGui.Alpha = title.FadeIn(Now);
            const float stripW = 560f;
            for (int i = 0; i < 7; i++)
            {
                var id = UiKit.Spectrum[i];
                var seg = new Rect(960f - stripW * 0.5f + i * (stripW / 7f) + 3f, 488f, stripW / 7f - 6f, 6f);
                CardGui.Box(seg, UiKit.Restored(id) ? UiKit.Accent(id) : new Color(0.2f, 0.21f, 0.25f));
            }
            CardGui.Alpha = a;

            // The menu.
            float menuIn = UiKit.Smooth((Now - openedAt - 0.5f) / 0.4f);
            CardGui.Alpha = menuIn;
            var panel = MenuPanel();
            UiKit.Panel(panel, UiKit.Neutral, Now, 22f, 0.85f);
            if (confirming)
            {
                CardGui.Text(new Rect(panel.x, panel.y + 18f, panel.width, 34f), "START OVER?", 24, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                CardGui.Text(new Rect(panel.x, panel.y + 50f, panel.width, 26f), "Your restored colors will be lost.", 18, UiKit.TextDim);
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                bool danger = items[i] == Item.ConfirmErase;
                UiKit.Button(buttons[i], Label(items[i]), danger ? UiKit.Danger : UiKit.Neutral, i == selected, true, 24, Now);
            }
            UiKit.Hint(new Vector2(960f, 1036f), confirming ? "[Up] [Down] Select   [Z] Confirm   [Esc] Cancel" : "[Up] [Down] Select   [Z] / [Enter] Confirm",
                       UiKit.Neutral, 20, 0.85f);
            UiKit.End();
        }

        Rect MenuPanel()
        {
            float top = confirming ? 92f : 26f;
            float h = top + items.Count * 76f + 20f;
            return new Rect(960f - 260f, 560f, 520f, h);
        }

        Rect[] ButtonRects()
        {
            var panel = MenuPanel();
            float top = confirming ? 92f : 26f;
            var rects = new Rect[items.Count];
            for (int i = 0; i < rects.Length; i++) rects[i] = new Rect(panel.x + 30f, panel.y + top + i * 76f, panel.width - 60f, 62f);
            return rects;
        }
    }
}
