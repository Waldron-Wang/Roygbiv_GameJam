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
        public PlayerIntent Modify(PlayerIntent i)
        {
            (i.jumpPressed, i.attackPressed) = (i.attackPressed, i.jumpPressed);
            (i.jumpHeld, i.attackHeld) = (i.attackHeld, i.jumpHeld);
            return i;
        }
    }
}
