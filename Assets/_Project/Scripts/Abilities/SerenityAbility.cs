using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Indigo's reward: calm focus. Press Serenity (Q / LB) and EVERYTHING slows down: the boss, its attacks and
    /// animations, every projectile, beam, trap and timer. The player slows down too, on purpose: the whole scene
    /// becomes slow and readable, so every gap can be seen and threaded precisely.
    ///
    ///   It lasts `duration` seconds; pressing again ends it early. When it ends (either way) it RECHARGES for
    ///   `rechargeTime` seconds and can't be used until that's over: a press then only gives a "not ready" blip.
    ///   Both are REAL seconds (unscaled time), so the slow motion doesn't stretch them, and neither counts down
    ///   while time is paused (pause menu, Tip card).
    ///   It ends on its own on death, when the level is won or lost, when dialogue or a cinematic starts and when
    ///   the scene unloads. It can't start during dialogue or while paused. A reload builds a fresh player, so it's always
    ///   ready after a respawn.
    ///
    /// It only asks Game.Time for a scale (TimeController multiplies it with any others) and announces its state with
    /// GameEvents.SerenityChanged / SerenityDenied: the screen look and the HUD meter are UI listening to those.
    /// </summary>
    public class SerenityAbility : AbilityBase
    {
        [Tooltip("How long it lasts, in real seconds (pausing doesn't count).")]
        [SerializeField] float duration = 4f;
        [Tooltip("Seconds it needs to recharge after it ends, in real seconds (pausing doesn't count). Can't be used until then.")]
        [SerializeField] float rechargeTime = 10f;
        [Tooltip("Speed of the whole game while it's on. 0.35 = everything (the player too) at 35% speed.")]
        [SerializeField, Range(0.05f, 1f)] float timeScale = 0.35f;

        SerenityState state = SerenityState.Ready;
        float left; // real seconds left of the current active / recharging stretch

        public override AbilityId Id => AbilityId.Serenity;
        public SerenityState State => enabled ? state : SerenityState.Unavailable;
        public bool IsActive => state == SerenityState.Active;
        /// <summary>Real seconds it lasts.</summary>
        public float Duration => duration;
        /// <summary>Real seconds it recharges after it ends.</summary>
        public float RechargeTime => rechargeTime;

        // The press is handled here, not by AbilityBase's scaled-time cooldown. A constructor so a copy added at
        // runtime gets it too; serialized prefab values still win.
        public SerenityAbility() => cooldown = 0f;

        void OnEnable()
        {
            GameEvents.LevelCompleted += OnLevelEnded;
            GameEvents.LevelFailed += OnLevelEnded;
            GameEvents.PlayerDied += End;
            GameEvents.DialogueStarted += OnDialogueStarted;
            GameEvents.CinematicChanged += OnCinematic;
            Announce();
        }

        void OnDisable()
        {
            GameEvents.LevelCompleted -= OnLevelEnded;
            GameEvents.LevelFailed -= OnLevelEnded;
            GameEvents.PlayerDied -= End;
            GameEvents.DialogueStarted -= OnDialogueStarted;
            GameEvents.CinematicChanged -= OnCinematic;
            End(); // revoked or unloaded mid-use: never leave the world slowed down
            GameEvents.RaiseSerenityChanged(SerenityState.Unavailable, 0f);
        }

        void OnDestroy()
        {
            if (Game.Time != null) Game.Time.ClearScale(this);
        }

        public override bool HandleInput(in PlayerIntent intent)
        {
            if (!intent.serenityPressed) return false;
            switch (state)
            {
                case SerenityState.Active:
                    End();
                    return true;
                case SerenityState.Ready when CanStart:
                    return TryActivate();
                default:
                    GameEvents.RaiseSerenityDenied();
                    return true;
            }
        }

        protected override void Activate()
        {
            if (state != SerenityState.Ready || !CanStart) return;
            state = SerenityState.Active;
            left = duration;
            Game.Time.SetScale(this, timeScale);
            Announce();
        }

        /// <summary>Ends it now (if it's on) and starts the recharge.</summary>
        public void End()
        {
            if (state != SerenityState.Active) return;
            if (Game.Time != null) Game.Time.ClearScale(this);
            state = SerenityState.Recharging;
            left = rechargeTime;
            Announce();
        }

        bool CanStart => Game.Time != null && !Game.Time.IsPaused && !Game.Dialogue.IsPlaying && !(Owner?.Health && Owner.Health.IsDead);

        void OnLevelEnded(ColorId _) => End();
        void OnDialogueStarted(DialogueData _) => End();
        void OnCinematic(bool playing) { if (playing) End(); }

        void Update()
        {
            if (state == SerenityState.Active && Owner?.Health && Owner.Health.IsDead) End();
            if (state == SerenityState.Ready || Game.Time == null || Game.Time.IsPaused) return;

            left -= Time.unscaledDeltaTime;
            if (left > 0f) { Announce(); return; }

            if (state == SerenityState.Active) End();
            else
            {
                state = SerenityState.Ready;
                Announce();
            }
        }

        void Announce()
        {
            float fraction = state switch
            {
                SerenityState.Active => Mathf.Clamp01(left / Mathf.Max(0.01f, duration)),
                SerenityState.Recharging => 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, rechargeTime)),
                _ => 1f,
            };
            GameEvents.RaiseSerenityChanged(State, fraction);
        }
    }
}
