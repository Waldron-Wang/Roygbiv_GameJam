#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roygbiv
{
    /// <summary>
    /// Editor and development builds only (see Bootstrapper; release builds don't even contain it). A debug readout in
    /// the UiKit look, hidden until F12 toggles it: scene and level, the boss (type, state, HP, phase, fighting),
    /// the player (HP, abilities on / stolen), Serenity, time scale and pause, input blocks and modifiers, Violet's
    /// checkpoint, and the cheat keys (DebugCheats). Reads only.
    /// </summary>
    public class DevOverlay : MonoBehaviour
    {
        static bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => visible = false;

        BossBase boss;
        SerenityState serenity = SerenityState.Unavailable;
        float serenityFraction, fps;
        readonly StringBuilder text = new();

        void OnEnable()
        {
            GameEvents.BossFightStarted += OnBoss;
            GameEvents.SerenityChanged += OnSerenity;
            GameEvents.SceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.BossFightStarted -= OnBoss;
            GameEvents.SerenityChanged -= OnSerenity;
            GameEvents.SceneLoaded -= OnSceneLoaded;
        }

        void OnBoss(BossBase b) => boss = b;
        void OnSerenity(SerenityState s, float f) { serenity = s; serenityFraction = f; }
        void OnSceneLoaded(string _) { boss = null; serenity = SerenityState.Unavailable; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f12Key.wasPressedThisFrame) visible = !visible;
            if (Time.unscaledDeltaTime > 0f) fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.1f);
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;
            GUI.depth = -20; // over the HUD, under dialogue and cards
            if (!boss && LevelController.Current) boss = LevelController.Current.Boss;

            text.Clear();
            Line("SCENE", $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}   {fps:0} fps");
            if (LevelController.Current) Line("LEVEL", LevelController.Current.Color.ToString());
            if (boss)
            {
                var h = boss.Health;
                var state = boss.GetType().GetProperty("CurrentState")?.GetValue(boss);
                Line("BOSS", $"{boss.GetType().Name}  {(boss.IsFighting ? "fighting" : "idle")}  {state}");
                if (h) Line("", $"HP {h.Current}/{h.Max}  phase {boss.Phase + 1}/{boss.PhaseThresholds.Count + 1}{(h.Invulnerable ? "  invulnerable" : "")}");
            }
            var pc = PlayerController.Instance;
            if (pc)
            {
                var on = new StringBuilder();
                foreach (var a in pc.Loadout.All) if (a.enabled) on.Append(a.Id).Append(' ');
                Line("PLAYER", $"HP {pc.Health.Current}/{pc.Health.Max}  {(pc.IsGrounded ? "grounded" : "air")}");
                Line("", on.Length > 0 ? on.ToString() : "no abilities");
            }
            if (serenity != SerenityState.Unavailable) Line("SERENITY", $"{serenity} {serenityFraction:0.00}");
            if (Game.Time) Line("TIME", $"scale {Time.timeScale:0.00}{(Game.Time.IsPaused ? "  paused" : "")}");
            if (Game.Input) Line("INPUT", $"{(Game.Input.GameplayEnabled ? "on" : "BLOCKED")}  move {Game.Input.Intent.move}");
            if (LevelController.Current && LevelController.Current.Color == ColorId.Violet)
                Line("VIOLET", $"checkpoint {VioletCheckpoint.Index} / arena {VioletCheckpoint.ArenaIndex}");
            Line("KEYS", "F12 hide   F1-F7 colors   F9 win   F10 wipe save");

            var view = UiKit.Fill();
            view.Begin();
            string body = text.ToString();
            float height = CardGui.WrappedHeight(body, 20, 700f) + 72f;
            var panel = new Rect(28f, view.Rect.height - height - 28f, 740f, height);
            UiKit.Panel(panel, UiKit.Neutral, -1f, 14f, 0.94f);
            UiKit.Header(panel, "Dev", UiKit.Neutral, 36f, 20);
            CardGui.Text(new Rect(panel.x + 20f, panel.y + 48f, 700f, height - 56f), body, 20, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true);
            UiKit.End();
        }

        void Line(string label, string value) =>
            text.Append("<color=#9aa3b2><b>").Append(label.PadRight(9)).Append("</b></color> ").Append(value).Append('\n');
    }
}
#endif
