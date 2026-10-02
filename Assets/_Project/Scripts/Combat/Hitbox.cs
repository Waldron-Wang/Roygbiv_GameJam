using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A trigger collider that deals damage while its GameObject is active.
    /// Turn it on for the attack's active frames with Open(seconds).
    /// Convention: hitboxes/projectiles are TRIGGERS, hurtboxes (bodies) are NON-trigger colliders.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        public Team team = Team.Player;
        public int damage = 1;
        public float knockback = 6f;
        [Tooltip("Hits IReflectable projectiles back instead of ignoring them (player melee).")]
        public bool reflectsProjectiles;

        readonly HashSet<IDamageable> alreadyHit = new();
        float closeAt;

        // Save this GameObject INACTIVE in prefabs; Open() switches it on.
        void Awake() => GetComponent<Collider2D>().isTrigger = true;

        public void Open(float seconds)
        {
            closeAt = Time.time + seconds;
            gameObject.SetActive(true);
        }

        void OnEnable() => alreadyHit.Clear();

        void Update()
        {
            if (Time.time >= closeAt) gameObject.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reflectsProjectiles && other.TryGetComponent<IReflectable>(out var reflectable))
            {
                if (reflectable.CanBeReflected)
                    reflectable.Reflect(team, (other.transform.position - transform.root.position).normalized);
                return;
            }

            if (other.isTrigger) return;
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || !alreadyHit.Add(target)) return;

            var dir = Mathf.Sign(other.transform.position.x - transform.position.x);
            target.TakeDamage(new DamageInfo(damage, team, new Vector2(dir * knockback, knockback * 0.5f), gameObject));
        }
    }
}
