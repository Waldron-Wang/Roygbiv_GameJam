using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The pause menu (IMGUI, UiKit look). Esc / Start toggles it while in a level. It holds a pause on Game.Time and
    /// blocks gameplay input while open, and raises PauseChanged.
    ///   A dimmed screen, the game's title, a panel in the level's color: Resume / Restart / Back to hub, with the
    ///   controls you have so far underneath. Up / Down + Z / Enter, or the mouse; Esc resumes.
    ///   Restart reloads the level exactly like dying does (GameManager.RestartLevel): in Violet you go back to the
    ///   last checkpoint you reached, so the button says so.
    /// Menu keys come from InputReader.Navigate (gameplay input is blocked while paused).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        enum Item { Resume, Restart, Hub }

        static readonly Item[] Items = { Item.Resume, Item.Restart, Item.Hub };

        readonly MenuNav nav = new();
        int selected;
        float openedAt;
        Vector2 lastMouse;

        public bool IsPaused { get; private set; }

        static float Now => Time.unscaledTime;

        void Update()
        {
            if (!Game.Input || !Game.Scenes || !Game.Instructions) return;
            // Not over the instruction card: it holds its own pause while open, and the Esc that closes it shouldn't also pause.
            if (Game.Input.Intent.pausePressed && LevelController.Current != null && !Game.Scenes.IsLoading && !Game.Instructions.BlocksPause)
            {
                SetPaused(!IsPaused);
                return;
            }
            if (!IsPaused) return;

            int step = nav.Step(-Game.Input.Navigate.y);
            if (step != 0) selected = (selected + step + Items.Length) % Items.Length;
            if (Game.Input.Intent.confirmPressed && Now - openedAt > 0.15f) Choose(Items[selected]);
        }

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            if (paused)
            {
                selected = 0;
                openedAt = Now;
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

        void Choose(Item item)
        {
            switch (item)
            {
                case Item.Resume: SetPaused(false); break;
                case Item.Restart: SetPaused(false); Game.Manager.RestartLevel(); break;
                case Item.Hub: SetPaused(false); Game.Manager.ReturnToHub(); break;
            }
        }

        /// <summary>Violet remembers the last checkpoint across a restart (as across a death).</summary>
        static bool AtCheckpoint => LevelController.Current && LevelController.Current.Color == ColorId.Violet && VioletCheckpoint.Index >= 0;

        static string Label(Item item) => item switch
        {
            Item.Resume => "Resume",
            Item.Restart => AtCheckpoint ? "Restart from checkpoint" : "Restart level",
            _ => "Back to hub",
        };

        void OnGUI()
        {
            if (!IsPaused) return;
            GUI.depth = -600; // over the HUD, dialogue and the letterbox; under the scene fade
            var e = Event.current;
            var full = UiKit.Fill();
            var view = UiKit.Fit();
            var accent = UiKit.CurrentAccent;
            var panel = new Rect(960f - 330f, 300f, 660f, 476f);
            var rows = new Rect[Items.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = new Rect(panel.x + 40f, panel.y + 84f + i * 80f, panel.width - 80f, 64f);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = 0; i < rows.Length; i++)
                    if (rows[i].Contains(view.Mouse)) { selected = i; Choose(Items[i]); e.Use(); return; }
            }
            if (e.type != EventType.Repaint) return;
            if ((view.Mouse - lastMouse).sqrMagnitude > 1f)
                for (int i = 0; i < rows.Length; i++)
                    if (rows[i].Contains(view.Mouse)) selected = i;
            lastMouse = view.Mouse;

            float t = Now - openedAt;
            full.Begin();
            CardGui.Alpha = UiKit.Smooth(t / 0.15f);
            CardGui.Box(full.Rect, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Scanlines(full.Rect, new Color(1f, 1f, 1f, 0.02f), 4f, 1f);
            UiKit.Vignette(full.Rect, new Color(0f, 0f, 0f, 0.6f));

            view.Begin();
            CardGui.Text(new Rect(0f, 228f, 1920f, 30f), UiKit.Spaced(UiKit.GameTitle), 18, UiKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            float open = UiKit.Smooth(t / 0.2f);
            var frame = panel;
            frame.height = Mathf.Max(4f, panel.height * open);
            frame.y += (panel.height - frame.height) * 0.5f;
            UiKit.Panel(frame, accent, Now);
            CardGui.Alpha *= UiKit.Smooth((t - 0.12f) / 0.15f);
            var header = UiKit.Header(panel, "Paused", accent);
            if (LevelController.Current)
            {
                var level = Game.Config.Get(LevelController.Current.Color);
                if (level)
                {
                    CardGui.Text(new Rect(header.xMax - 260f, header.y, 220f, header.height), UiKit.Spaced(level.displayName.ToUpperInvariant()), 16,
                                 UiKit.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
                    UiKit.Gem(new Vector2(header.xMax - 24f, header.center.y), 18f, accent, UiKit.Restored(level.id));
                }
            }
            for (int i = 0; i < rows.Length; i++) UiKit.Button(rows[i], Label(Items[i]), accent, i == selected, true, 24, Now);

            // The controls you have so far.
            float y = rows[rows.Length - 1].yMax + 26f;
            CardGui.Box(new Rect(panel.x + 40f, y - 10f, panel.width - 80f, 1.5f), UiKit.WithAlpha(accent, 0.3f));
            UiKit.Hint(new Vector2(panel.center.x, y + 18f), "[Left] [Right] Move   [Space] Jump   [LMB] Attack", accent, 18);
            var unlocked = UnlockedAbilities();
            for (int line = 0; line * 3 < unlocked.Count; line++) // three to a line
                UiKit.Hint(new Vector2(panel.center.x, y + 52f + line * 34f),
                           string.Join("   ", unlocked.GetRange(line * 3, Mathf.Min(3, unlocked.Count - line * 3))), accent, 17);

            UiKit.Hint(new Vector2(960f, 830f), "[Up] [Down] Select   [Z] Confirm   [Esc] Resume", UiKit.Neutral, 20, 0.85f);
            UiKit.End();
        }

        /// <summary>"[RMB] Light shot", "[Shift] Dash", ... for the abilities the player has right now.</summary>
        static List<string> UnlockedAbilities()
        {
            var parts = new List<string>();
            var pc = PlayerController.Instance;
            if (!pc || !Game.Config) return parts;
            foreach (var data in Game.Config.colorOrder)
            {
                if (!data || data.grantedAbility == AbilityId.None || !pc.Loadout.Has(data.grantedAbility)) continue;
                var info = UiKit.Ability(data.grantedAbility);
                var keys = "";
                foreach (var k in info.Keys) keys += $"[{k}] ";
                string name = info.Tag == "HOLD" ? "Hold: " + Title(info.Name) : Title(info.Name);
                parts.Add(keys + name);
            }
            return parts;
        }

        static string Title(string caps) => caps.Length == 0 ? caps : caps[0] + caps.Substring(1).ToLowerInvariant();
    }
}
