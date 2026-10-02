using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The player's "brain". Each frame: read Game.Input.Intent -> feed motor, abilities, basic attack.
    /// Also the bridge from the player's local Health events to global GameEvents.
    /// Components on the same GameObject talk to each other DIRECTLY (GetComponent), not through events.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(Health), typeof(AbilityLoadout))]
    public class PlayerController : MonoBehaviour, IActor
    {
        PlayerMotor motor;
        Health health;
        PlayerCombat combat;
        AbilityLoadout loadout;

        public static PlayerController Instance { get; private set; }

        // ---------- IActor ----------
        public Team Team => Team.Player;
        public Transform Root => transform;
        public Rigidbody2D Body => motor.Body;
        public Health Health => health;
        public int FacingSign => motor.FacingSign;
        public bool IsGrounded => motor.IsGrounded;
        public bool MovementLocked { get => motor.Locked; set => motor.Locked = value; }
        public Vector2 AimDirection
        {
            get
            {
                var m = Game.Input.Intent.move;
                return m.y > 0.5f ? Vector2.up : new Vector2(FacingSign, 0f);
            }
        }

        public AbilityLoadout Loadout => loadout;

        void Awake()
        {
            Instance = this;
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            loadout = GetComponent<AbilityLoadout>();
        }

        void OnEnable()
        {
            health.Changed += OnHealthChanged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Changed -= OnHealthChanged;
            health.Died -= OnDied;
        }

        void Start() => GameEvents.RaisePlayerHealthChanged(health.Current, health.Max);

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (health.IsDead) { motor.SetInput(Vector2.zero, false, false); return; }

            var intent = Game.Input.Intent;
            motor.SetInput(intent.move, intent.jumpPressed, intent.jumpHeld);

            bool abilityUsedInput = loadout.HandleInput(intent);
            if (!abilityUsedInput && intent.attackPressed && combat != null)
                combat.TryAttack(FacingSign);
        }

        void OnHealthChanged(int current, int max) => GameEvents.RaisePlayerHealthChanged(current, max);

        void OnDied()
        {
            motor.Locked = true;
            GameEvents.RaisePlayerDied();
        }
    }
}
