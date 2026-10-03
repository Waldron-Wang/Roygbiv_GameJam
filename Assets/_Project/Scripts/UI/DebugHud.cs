using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// PLACEHOLDER HUD (IMGUI). Shows the pattern every real UI must follow:
    /// it only LISTENS to GameEvents and READS state — it never drives gameplay.
    /// Replace with a styled HUD once the art style is decided; delete this file then.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        int playerHp, playerMax;
        BossBase boss;

        void OnEnable()
        {
            GameEvents.PlayerHealthChanged += OnPlayerHealth;
            GameEvents.BossFightStarted += OnBoss;
            GameEvents.BossHealthChanged += OnBoss;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.SceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.PlayerHealthChanged -= OnPlayerHealth;
            GameEvents.BossFightStarted -= OnBoss;
            GameEvents.BossHealthChanged -= OnBoss;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.SceneLoaded -= OnSceneLoaded;
        }

        void OnPlayerHealth(int hp, int max) { playerHp = hp; playerMax = max; }
        void OnBoss(BossBase b) => boss = b;
        void OnBossDefeated(BossBase _) => boss = null;
        void OnSceneLoaded(string _) { boss = null; playerMax = 0; }

        void OnGUI()
        {
            if (LevelController.Current == null && PlayerController.Instance == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 420, 200));
            if (playerMax > 0) GUILayout.Label($"HP  {new string('■', playerHp)}{new string('□', playerMax - playerHp)}");

            var colors = "";
            foreach (var c in Game.Config.colorOrder)
                colors += Game.Progress.IsRestored(c.id) ? $"<color=#{ColorUtility.ToHtmlStringRGB(c.tint)}>●</color> " : "○ ";
            GUILayout.Label("Colors  " + colors, new GUIStyle(GUI.skin.label) { richText = true });

            var abilities = "";
            if (PlayerController.Instance)
                foreach (var a in PlayerController.Instance.Loadout.All)
                    abilities += a.enabled ? $"[{a.Id}] " : "";
            GUILayout.Label("Abilities  " + (abilities == "" ? "-" : abilities));
            GUILayout.EndArea();

            if (boss && boss.Health)
            {
                var h = boss.Health;
                GUI.Label(new Rect(Screen.width / 2f - 150, 10, 300, 25),
                    $"{boss.DisplayName}  {h.Current}/{h.Max}  (phase {boss.Phase + 1})",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            }
        }
    }
}
