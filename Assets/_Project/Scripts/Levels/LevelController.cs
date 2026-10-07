using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// One per level scene. The referee: knows which color this level is, starts the fight,
    /// and decides win / lose. It never applies rewards or loads scenes itself — it raises
    /// LevelCompleted / LevelFailed and GameManager handles the rest.
    ///
    /// Win conditions (combine as needed):
    ///   - assign `boss`: level completes when it is defeated
    ///   - LevelTrigger set to CompleteLevel (reach the top in Blue, end of a chase...)
    ///   - any custom script calling LevelController.Current.Complete()
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        [SerializeField] ColorId color;
        [SerializeField] BossBase boss;
        [Tooltip("Start the boss as soon as the level begins. Off = use a LevelTrigger (StartBoss) at the arena door.")]
        [SerializeField] bool startBossImmediately = true;
        [SerializeField] DialogueData introDialogue;

        bool finished;

        public static LevelController Current { get; private set; }
        public ColorId Color => color;
        public BossBase Boss => boss;
        /// <summary>A level that builds itself at runtime (Violet) turns this off in Awake, before Start reads it.</summary>
        public bool StartBossImmediately { get => startBossImmediately; set => startBossImmediately = value; }

        void Awake() => Current = this;

        void OnEnable()
        {
            GameEvents.PlayerDied += Fail;
            if (boss) boss.Defeated += OnBossDefeated;
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= Fail;
            if (boss) boss.Defeated -= OnBossDefeated;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        IEnumerator Start()
        {
            GameEvents.RaiseLevelStarted(color);
            if (introDialogue) yield return Game.Dialogue.Play(introDialogue);
            if (boss && startBossImmediately) boss.StartFight();
        }

        public void StartBoss()
        {
            if (boss) boss.StartFight();
        }

        public void Complete()
        {
            if (finished) return;
            finished = true;
            GameEvents.RaiseLevelCompleted(color);
        }

        public void Fail()
        {
            if (finished) return;
            finished = true;
            GameEvents.RaiseLevelFailed(color);
        }

        void OnBossDefeated(BossBase _) => Complete();
    }
}
