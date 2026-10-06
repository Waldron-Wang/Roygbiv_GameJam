using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Transforms the player's intent after it is read from the device.
    /// Push one with Game.Input.AddModifier(...), remove it with RemoveModifier(...).
    /// Main client: the Indigo boss (disorientation).
    /// </summary>
    public interface IInputModifier
    {
        PlayerIntent Modify(PlayerIntent intent);
    }

    public sealed class InvertHorizontalModifier : IInputModifier
    {
        public PlayerIntent Modify(PlayerIntent i)
        {
            i.move.x = -i.move.x;
            return i;
        }
    }

    public sealed class SwapJumpAndAttackModifier : IInputModifier
    {
        bool attackWasHeld;

        public PlayerIntent Modify(PlayerIntent i)
        {
            (i.jumpPressed, i.attackPressed) = (i.attackPressed, i.jumpPressed);
            (i.jumpHeld, i.attackHeld) = (i.attackHeld, i.jumpHeld);
            // The release has to follow the swap too, or Blaze Strike would charge on one key and fire on the other.
            i.attackReleased = attackWasHeld && !i.attackHeld;
            attackWasHeld = i.attackHeld;
            return i;
        }
    }

    public sealed class SwapShootAndDashModifier : IInputModifier
    {
        public PlayerIntent Modify(PlayerIntent i)
        {
            (i.shootPressed, i.dashPressed) = (i.dashPressed, i.shootPressed);
            return i;
        }
    }

    /// <summary>
    /// The body acts `delay` seconds after the player does (Indigo's Echo). Every press still happens exactly once,
    /// just late. Aim, confirm and pause stay live, so menus and the cursor never lag.
    /// </summary>
    public sealed class DelayedInputModifier : IInputModifier
    {
        readonly float delay;
        readonly Queue<(float time, PlayerIntent intent)> pending = new();
        PlayerIntent shown;

        public DelayedInputModifier(float delay) => this.delay = delay;

        public PlayerIntent Modify(PlayerIntent i)
        {
            float now = Time.time;
            pending.Enqueue((now, i));

            var o = shown;
            o.jumpPressed = o.attackPressed = o.attackReleased = o.shootPressed = o.dashPressed = o.serenityPressed = false;
            while (pending.Count > 0 && pending.Peek().time <= now - delay)
            {
                var e = pending.Dequeue().intent;
                o.move = e.move;
                o.jumpHeld = e.jumpHeld;
                o.attackHeld = e.attackHeld;
                // Presses that come due on the same frame are merged, never dropped.
                o.jumpPressed |= e.jumpPressed;
                o.attackPressed |= e.attackPressed;
                o.attackReleased |= e.attackReleased;
                o.shootPressed |= e.shootPressed;
                o.dashPressed |= e.dashPressed;
                o.serenityPressed |= e.serenityPressed;
            }
            shown = o;

            o.aimPoint = i.aimPoint;
            o.hasAimPoint = i.hasAimPoint;
            o.confirmPressed = i.confirmPressed;
            o.pausePressed = i.pausePressed;
            return o;
        }
    }
}
