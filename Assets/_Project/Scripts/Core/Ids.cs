namespace Roygbiv
{
    // These enums are serialized as ints in assets and save files.
    // ONLY APPEND new values at the end — never reorder or delete.

    /// <summary>The seven colors, in spectrum order. Play order lives in GameConfig, not here.</summary>
    public enum ColorId { Red, Orange, Yellow, Green, Blue, Indigo, Violet }

    /// <summary>Every unlockable ability. Add Indigo / Violet abilities here once designed.</summary>
    public enum AbilityId { None, LightShot, Dash, BlazeStrike, HeavySlam }

    /// <summary>Who can hurt whom. Neutral = environment / switches, hit by everyone.</summary>
    public enum Team { Neutral, Player, Enemy }
}
