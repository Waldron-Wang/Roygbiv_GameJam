using UnityEngine;

namespace Roygbiv
{
    /// <summary>Everything about a single hit.</summary>
    public struct DamageInfo
    {
        public int amount;
        public Team sourceTeam;
        public Vector2 knockback;
        public GameObject source;

        public DamageInfo(int amount, Team sourceTeam, Vector2 knockback = default, GameObject source = null)
        {
            this.amount = amount;
            this.sourceTeam = sourceTeam;
            this.knockback = knockback;
            this.source = source;
        }
    }

    /// <summary>
    /// Anything that reacts to being hit: player, bosses, enemies, shootable switches, breakable walls.
    /// Hitboxes and projectiles find it with GetComponentInParent&lt;IDamageable&gt;() on a NON-trigger collider.
    /// </summary>
    public interface IDamageable
    {
        Team Team { get; }
        /// <returns>true if the hit landed (not blocked / invulnerable / same team).</returns>
        bool TakeDamage(in DamageInfo info);
    }

    /// <summary>Projectiles that can be hit back (Yellow boss: punch its orbs back at it).</summary>
    public interface IReflectable
    {
        bool CanBeReflected { get; }
        void Reflect(Team newTeam, Vector2 direction);
    }

    /// <summary>
    /// Something that moves and fights: the player AND bosses. Abilities talk to their owner only
    /// through this, which is how the Green boss can use the player's stolen abilities.
    /// </summary>
    public interface IActor
    {
        Team Team { get; }
        Transform Root { get; }
        Rigidbody2D Body { get; }
        Health Health { get; }
        int FacingSign { get; }          // +1 right, -1 left
        Vector2 AimDirection { get; }
        bool IsGrounded { get; }
        /// <summary>When true the actor's own movement code must not touch velocity (dash, knockback...).</summary>
        bool MovementLocked { get; set; }
    }

    public static class Combat
    {
        /// <summary>Neutral hurts / is hurt by everyone; otherwise only opposing teams.</summary>
        public static bool CanHurt(Team attacker, Team target) =>
            attacker == Team.Neutral || target == Team.Neutral || attacker != target;
    }
}
