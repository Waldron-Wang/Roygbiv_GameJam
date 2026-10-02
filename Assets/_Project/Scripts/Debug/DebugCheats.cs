using UnityEngine;
using UnityEngine.InputSystem;

namespace Roygbiv
{
    /// <summary>
    /// Editor / development builds only (see Bootstrapper). Lets everyone test their level out of order.
    ///   F1..F7  restore the Nth color in play order (+ its ability)
    ///   F8      toggle god mode
    ///   F9      complete the current level
    ///   F10     wipe save + reset colors
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
                if (!godMode && PlayerController.Instance) PlayerController.Instance.Health.Invulnerable = false;
            }
            if (godMode && PlayerController.Instance) PlayerController.Instance.Health.Invulnerable = true;

            if (kb.f9Key.wasPressedThisFrame) LevelController.Current?.Complete();

            if (kb.f10Key.wasPressedThisFrame)
            {
                GameProgress.DeleteSave();
                Game.Manager.NewGame();
            }
        }

        void OnGUI()
        {
            if (godMode) GUI.Label(new Rect(Screen.width - 110, 10, 100, 25), "GOD MODE");
        }
    }
}
