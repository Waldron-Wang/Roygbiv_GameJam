using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// One per level scene. The referee: knows which color this level is, starts the fight,
    /// and decides win / lose. It never applies rewards or loads scenes itself — it raises
    /// LevelCompleted / LevelFailed and GameManager handles the rest.
    ///
    /// StartBoss() plays the boss reveal first (BossIntro) when this color's ColorData.bossIntro is on.
    /// With ColorData.respawnAtBoss, starting the boss sets a BossCheckpoint: after a death the reloaded level puts
    /// the player back at the arena gate and restarts the fight, skipping the intro level.
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
        BossIntro intro;

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
            if (RespawnAtBoss()) yield break;
            if (introDialogue) yield return Game.Dialogue.Play(introDialogue);
            if (boss && startBossImmediately) boss.StartFight();
        }

        public void StartBoss()
        {
            if (!boss || boss.IsFighting || (intro && intro.IsRunning)) return;
            var data = Game.Config.Get(color);
            var pc = PlayerController.Instance;
            if (data && data.respawnAtBoss && pc) BossCheckpoint.Reach(pc.transform.position);
            if (data && data.bossIntro) StartCoroutine(IntroThenFight());
            else boss.StartFight();
        }

        // Back at the arena gate after a death: put the player where they stood when the fight started, then fire the
        // gate trigger again (it closes the door and calls StartBoss), or start the boss directly if there's none.
        bool RespawnAtBoss()
        {
            var data = Game.Config.Get(color);
            if (!boss || !data || !data.respawnAtBoss || !BossCheckpoint.Active) return false;

            var pc = PlayerController.Instance;
            if (pc)
            {
                pc.transform.position = BossCheckpoint.Position;
                if (pc.Body) { pc.Body.position = BossCheckpoint.Position; pc.Body.linearVelocity = Vector2.zero; }
                if (Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.SnapToPlayer();
            }

            foreach (var t in FindObjectsByType<LevelTrigger>(FindObjectsSortMode.None))
                if (t.TriggerAction == LevelTrigger.Action.StartBoss) { t.Fire(); return true; }
            StartBoss();
            return true;
        }

        IEnumerator IntroThenFight()
        {
            if (!intro) intro = gameObject.AddComponent<BossIntro>();
            yield return intro.Play(boss);
            if (!finished && boss) boss.StartFight();
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
