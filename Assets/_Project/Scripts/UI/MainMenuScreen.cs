using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The main menu (IMGUI, UiKit look). Lives in the MainMenu scene and only calls GameManager commands.
    ///   The title, UiKit.GameTitle, starts colorless and glitches (GlitchTitle): color bleeds in for a split second and
    ///   falls away; every restored color stays a little more, all seven = a title in full color. A heart above it
    ///   flickers the same way. A strip under it shows the seven colors.
    ///   One button, START: keeps the save and goes to the hub (a fresh save is just an empty one). Erasing progress
    ///   lives in the hub ("Reset progress"). Click it; Z / Enter work too, without a hint.
    ///   On a fresh save START first plays the Prologue (the story, GameConfig.prologue) over the menu, then goes
    ///   straight into the first level (GameManager.ShouldPlayPrologue / FinishPrologue).
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        static readonly Rect StartButton = new(960f - 230f, 600f, 460f, 96f);

        readonly GlitchTitle title = new();
        readonly Prologue prologue = new();
        float openedAt = -1f, startedAt = -1f;
        bool started, leaving;

        static float Now => Time.unscaledTime;

        void Update()
        {
            if (openedAt < 0f) openedAt = Now;
            if (!Game.Input || Game.Scenes.IsLoading) return;
            if (prologue.Playing)
            {
                if (Now - startedAt < 0.3f) return; // not the press that started it
                prologue.Tick();
                // Keep drawing its dark last frame under the scene fade, so the menu never flashes back.
                if (prologue.Finished && !leaving) { leaving = true; Game.Manager.FinishPrologue(); }
                return;
            }
            if (Game.Input.Intent.confirmPressed && Now - openedAt > 0.5f) StartGame();
        }

        void StartGame()
        {
            if (started || !Game.Manager) return;
            started = true;
            startedAt = Now;
            if (Game.Manager.ShouldPlayPrologue) prologue.Begin(Game.Config.prologue);
            else Game.Manager.ContinueGame();
        }

        void OnGUI()
        {
            if (openedAt < 0f) return;
            var e = Event.current;
            if (prologue.Playing)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && Now - startedAt > 0.3f) { prologue.Click(); e.Use(); return; }
                if (e.type == EventType.Repaint) prologue.Draw();
                return;
            }
            title.Tick(Now);
            var full = UiKit.Fill();
            var view = UiKit.Fit();
            bool hover = StartButton.Contains(view.Mouse);

            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                StartGame();
                e.Use();
                return;
            }
            if (e.type != EventType.Repaint) return;

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
            CardGui.Alpha = title.FadeIn(Now);
            const float stripW = 560f;
            for (int i = 0; i < 7; i++)
            {
                var id = UiKit.Spectrum[i];
                var seg = new Rect(960f - stripW * 0.5f + i * (stripW / 7f) + 3f, 488f, stripW / 7f - 6f, 6f);
                CardGui.Box(seg, UiKit.Restored(id) ? UiKit.Accent(id) : new Color(0.2f, 0.21f, 0.25f));
            }

            // START.
            CardGui.Alpha = UiKit.Smooth((Now - openedAt - 0.5f) / 0.4f);
            if (hover || started) CardGui.Glow(StartButton.center, 300f, UiKit.WithAlpha(glow, 0.12f));
            UiKit.Button(StartButton, "Start", UiKit.Neutral, hover || started, true, 40, Now);
            UiKit.End();
        }
    }
}
