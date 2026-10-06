using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// HP for anything. Raises plain C# events that its OWN GameObject listens to
    /// (PlayerController / BossBase translate them into GameEvents for the rest of the game).
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] Team team = Team.Enemy;
        [SerializeField] int maxHealth = 5;
        [Tooltip("Invulnerability after taking a hit.")]
        [SerializeField] float hitInvulnerability = 0.4f;

        float invulnerableUntil;

        public Team Team => team;
        public int Max => maxHealth;
        public int Current { get; private set; }
        public float Fraction => maxHealth > 0 ? (float)Current / maxHealth : 0f;
        public bool IsDead => Current <= 0;

        /// <summary>Set by abilities/bosses for i-frames or armored phases. A hit with pierceInvulnerability ignores it.</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Optional veto checked on every hit: return false to ignore it (Yellow: only reflected orbs hurt).</summary>
        public Func<DamageInfo, bool> DamageFilter { get; set; }

        public event Action<int, int> Changed;      // current, max
        public event Action<DamageInfo> Damaged;
        public event Action Died;

        void Awake() => Current = maxHealth;

        public bool TakeDamage(in DamageInfo info)
        {
            if (IsDead || Time.time < invulnerableUntil) return false;
            if (Invulnerable && !info.pierceInvulnerability) return false;
            if (!Combat.CanHurt(info.sourceTeam, team)) return false;
            if (DamageFilter != null && !DamageFilter(info)) return false;

            Current = Mathf.Max(0, Current - info.amount);
            invulnerableUntil = Time.time + hitInvulnerability;

            if (info.knockback != Vector2.zero && TryGetComponent<Rigidbody2D>(out var rb) && rb.bodyType == RigidbodyType2D.Dynamic)
                rb.linearVelocity = info.knockback;

            Damaged?.Invoke(info);
            Changed?.Invoke(Current, maxHealth);
            if (IsDead) Died?.Invoke();
            return true;
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }

        /// <summary>Sets HP directly, no hit (checkpoints: a boss resumed at its phase threshold). Raises Changed.</summary>
        /// <summary>Sets max HP and fills up, without raising anything (setup in Awake, before the fight).</summary>
        public void Configure(int max)
        {
            maxHealth = Mathf.Max(1, max);
            Current = maxHealth;
        }

        public void SetCurrent(int value)
        {
            if (IsDead) return;
            Current = Mathf.Clamp(value, 1, maxHealth);
            Changed?.Invoke(Current, maxHealth);
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0;
            Changed?.Invoke(Current, maxHealth);
            Died?.Invoke();
        }
    }
}
