using System;
using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Shared boss plumbing: health, phases, the attack loop, and announcing itself to the game.
    /// A concrete boss only writes its attack patterns:
    ///
    ///   protected override IEnumerator RunPhase(int phase)
    ///   {
    ///       // ONE attack cycle for this phase. Called again and again until the boss dies,
    ///       // so a phase change takes effect at the end of the current cycle.
    ///   }
    ///
    /// The LevelController calls StartFight() and listens to Defeated. The boss never loads scenes
    /// or touches the save — it just dies, and the flow takes it from there.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public abstract class BossBase : MonoBehaviour, IActor
    {
        [SerializeField] ColorId color;
        [SerializeField] string displayName = "Boss";
        [Tooltip("Health fractions where the next phase starts, high to low. {0.66, 0.33} = 3 phases.")]
        [SerializeField] float[] phaseThresholds = { 0.66f, 0.33f };

        Health health;
        Rigidbody2D body;
        Coroutine brain;

        public ColorId Color => color;
        public string DisplayName => displayName;
        public int Phase { get; private set; }
        public bool IsFighting { get; private set; }

        /// <summary>Local event for the LevelController in the same scene.</summary>
        public event Action<BossBase> Defeated;

        protected Transform Player => PlayerController.Instance ? PlayerController.Instance.transform : null;

        // ---------- IActor ----------
        public Team Team => health.Team;
        public Transform Root => transform;
        public Rigidbody2D Body => body;
        public Health Health => health;
        public virtual int FacingSign => Player && Player.position.x < transform.position.x ? -1 : 1;
        public virtual Vector2 AimDirection => Player ? ((Vector2)(Player.position - transform.position)).normalized : Vector2.left;
        public virtual bool IsGrounded => true;
        public bool MovementLocked { get; set; }

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
        }

        protected virtual void OnEnable()
        {
            health.Changed += OnHealthChanged;
            health.Died += OnDied;
        }

        protected virtual void OnDisable()
        {
            health.Changed -= OnHealthChanged;
            health.Died -= OnDied;
        }

        public void StartFight()
        {
            if (IsFighting || health.IsDead) return;
            IsFighting = true;
            GameEvents.RaiseBossFightStarted(this);
            OnFightStarted();
            brain = StartCoroutine(BrainLoop());
        }

        IEnumerator BrainLoop()
        {
            while (!health.IsDead)
            {
                yield return StartCoroutine(RunPhase(Phase));
                yield return null; // guarantees progress even if RunPhase yields nothing
            }
        }

        /// <summary>One attack cycle for the given phase (0-based).</summary>
        protected abstract IEnumerator RunPhase(int phase);

        protected virtual void OnFightStarted() { }
        protected virtual void OnPhaseChanged(int newPhase) { }
        protected virtual void OnDefeated() { }

        void OnHealthChanged(int current, int max)
        {
            GameEvents.RaiseBossHealthChanged(this);

            int newPhase = 0;
            foreach (var t in phaseThresholds)
                if (health.Fraction <= t) newPhase++;

            if (newPhase != Phase && !health.IsDead)
            {
                Phase = newPhase;
                OnPhaseChanged(Phase);
                GameEvents.RaiseBossPhaseChanged(this);
            }
        }

        void OnDied()
        {
            IsFighting = false;
            StopAllCoroutines();
            brain = null;
            OnDefeated();
            Defeated?.Invoke(this);
            GameEvents.RaiseBossDefeated(this);
        }

        // ---------- Helpers for subclasses ----------

        protected Projectile Fire(Projectile prefab, Vector2 direction, Vector2? from = null)
        {
            var p = Instantiate(prefab, from ?? (Vector2)transform.position, Quaternion.identity);
            p.Launch(direction, Team);
            return p;
        }

        protected static WaitForSeconds Wait(float seconds) => new(seconds);
    }
}
