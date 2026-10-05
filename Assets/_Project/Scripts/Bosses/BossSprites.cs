using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Swaps the boss's Visual sprite to match BossBase.Pose: one sprite per pose, or a looping cycle for Move.
    /// A hit shows the hurt sprite for hurtTime; a defeated boss stays on it.
    /// Runs in Update so anything copying the sprite in LateUpdate (Red's heat glow) sees this frame's.
    /// Wired by ROYGBIV > Build Boss Sprites.
    /// </summary>
    [RequireComponent(typeof(BossBase))]
    public class BossSprites : MonoBehaviour
    {
        [Tooltip("Defaults to the SpriteRenderer on the child named Visual (searched at any depth).")]
        [SerializeField] SpriteRenderer target;
        [SerializeField] Sprite idle;
        [Tooltip("Empty = idle.")]
        [SerializeField] Sprite attack;
        [Tooltip("Empty = idle.")]
        [SerializeField] Sprite hurt;
        [Tooltip("Looping cycle for the Move pose (Orange running). Empty = idle.")]
        [SerializeField] Sprite[] move = { };
        [SerializeField] float moveFps = 8f;
        [Tooltip("How long a hit shows the hurt sprite.")]
        [SerializeField] float hurtTime = 0.35f;

        BossBase boss;
        Health health; // not boss.Health: BossBase's Awake may not have run before our OnEnable
        float hurtUntil, moveClock;

        void Awake()
        {
            boss = GetComponent<BossBase>();
            health = GetComponent<Health>();
            if (!target)
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == "Visual" && t.TryGetComponent(out target)) break;
        }

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(DamageInfo _) => hurtUntil = Time.time + hurtTime;

        void Update()
        {
            if (!target) return;
            var pose = health.IsDead || Time.time < hurtUntil ? BossPose.Hurt : boss.Pose;
            moveClock = pose == BossPose.Move ? moveClock + Time.deltaTime : 0f;
            var sprite = pose switch
            {
                BossPose.Attack => attack,
                BossPose.Hurt => hurt,
                BossPose.Move when move.Length > 0 => move[(int)(moveClock * moveFps) % move.Length],
                _ => null,
            };
            target.sprite = sprite ? sprite : idle;
        }
    }
}
