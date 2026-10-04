using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// One frame of "what the player wants to do", independent of keys/gamepad.
    /// Gameplay reads THIS, never the keyboard — which is what lets Indigo scramble controls
    /// and lets dialogue/pause freeze the player without touching player code.
    /// </summary>
    public struct PlayerIntent
    {
        public Vector2 move;

        public bool jumpPressed, jumpHeld;
        public bool attackPressed, attackHeld, attackReleased;
        public bool shootPressed;
        public bool dashPressed;

        /// <summary>World-space point under the mouse. Only valid when hasAimPoint (keyboard + mouse);
        /// on gamepad the player aims with move + facing instead.</summary>
        public Vector2 aimPoint;
        public bool hasAimPoint;

        // UI-level (still delivered while gameplay is blocked)
        public bool confirmPressed;
        public bool pausePressed;

        public void ClearGameplay()
        {
            move = Vector2.zero;
            jumpPressed = jumpHeld = false;
            attackPressed = attackHeld = attackReleased = false;
            shootPressed = dashPressed = false;
        }
    }
}
