using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Hurts the player on touch (Orange: hot beams you must short-hop under). A trigger, so it never
    /// blocks movement; Health's i-frames stop it from hitting every frame.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        [SerializeField] int damage = 1;
        [Tooltip("Downward speed on hit. Horizontal speed is kept so an auto-run doesn't stall.")]
        [SerializeField] float pushDown = 6f;

        void Awake() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other) => Hurt(other);
        void OnTriggerStay2D(Collider2D other) => Hurt(other);

        void Hurt(Collider2D other)
        {
            if (other.isTrigger) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (!player) return;
            var knockback = new Vector2(player.Body.linearVelocity.x, -pushDown);
            player.Health.TakeDamage(new DamageInfo(damage, Team.Neutral, knockback, gameObject));
        }
    }
}
