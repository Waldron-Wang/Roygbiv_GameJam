using UnityEngine;
using UnityEngine.InputSystem;

namespace Roygbiv
{
    /// <summary>
    /// Editor / development builds only (see Bootstrapper). Lets everyone test their level out of order.
    ///   F1..F7  restore the Nth color in play order (+ its ability)
    ///   F8      toggle god mode (blocks every hit, Violet's piercing needles included)
    ///   F9      complete the current level
    ///   F10     wipe save + reset colors
    /// Level_Violet only (they reload the level at that checkpoint, intro skipped):
    ///   PageDown / PageUp   next / previous checkpoint
    ///   Home                skip to the duel (arena entrance)
    ///   End                 skip to Twin Blades (phase 3)
    /// </summary>
    public class DebugCheats : MonoBehaviour
    {
        bool godMode;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var fKeys = new[] { kb.f1Key, kb.f2Key, kb.f3Key, kb.f4Key, kb.f5Key, kb.f6Key, kb.f7Key };
            var order = Game.Config.colorOrder;
            for (int i = 0; i < fKeys.Length && i < order.Count; i++)
                if (fKeys[i].wasPressedThisFrame) Game.Manager.RestoreColor(order[i].id);

            if (kb.f8Key.wasPressedThisFrame)
            {
                godMode = !godMode;
                if (!godMode && PlayerController.Instance)
                {
                    PlayerController.Instance.Health.Invulnerable = false;
                    PlayerController.Instance.Health.GodMode = false;
                }
            }
            if (godMode && PlayerController.Instance)
            {
                PlayerController.Instance.Health.Invulnerable = true;
                PlayerController.Instance.Health.GodMode = true;
            }

            if (kb.f9Key.wasPressedThisFrame) LevelController.Current?.Complete();

            if (kb.f10Key.wasPressedThisFrame)
            {
                GameProgress.DeleteSave();
                Game.Manager.NewGame();
            }

            VioletKeys(kb);
        }

        static void VioletKeys(Keyboard kb)
        {
            var level = LevelController.Current;
            if (!level || level.Color != ColorId.Violet || Game.Scenes.IsLoading) return;

            int target = int.MinValue;
            if (kb.pageDownKey.wasPressedThisFrame) target = VioletCheckpoint.Index + 1;
            else if (kb.pageUpKey.wasPressedThisFrame) target = VioletCheckpoint.Index - 1;
            else if (kb.homeKey.wasPressedThisFrame) target = VioletCheckpoint.ArenaIndex;
            else if (kb.endKey.wasPressedThisFrame) target = VioletCheckpoint.TwinBladesIndex;
            if (target == int.MinValue) return;

            VioletCheckpoint.JumpTo(Mathf.Max(0, target));
            Debug.Log($"[DebugCheats] Violet: checkpoint {VioletCheckpoint.Index} (arena = {VioletCheckpoint.ArenaIndex}, twin blades = {VioletCheckpoint.TwinBladesIndex}).");
            Game.Scenes.Reload();
        }

        void OnGUI()
        {
            if (godMode) GUI.Label(new Rect(Screen.width - 110, 10, 100, 25), "GOD MODE");
        }
    }
}
