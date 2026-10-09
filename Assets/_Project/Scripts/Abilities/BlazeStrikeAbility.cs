using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Red's reward, worked like a Nail Art:
    ///   Press Attack       the normal swing fires at once (PlayerController), exactly as without Blaze.
    ///   Keep holding       after chargeDelay the blade starts to heat; at chargeTime it's READY and stays READY while held.
    ///   Release at READY   a heavy flaming strike: StrikeHitbox opens (3 damage on the player; the only thing that breaks
    ///                      Violet's crystal gate). Released any earlier, nothing more happens (the tap already swung).
    /// Getting hit, dying, any gameplay block (dialogue, pause, the Tip card) or losing the ability (Green steals it)
    /// cancels the charge, and it needs a fresh press. Nothing Blaze-related shows before chargeDelay, so taps stay clean.
    /// AI path (Green, once it has stolen it): TryActivate() strikes at once; Green does its own wind-up.
    /// The effects are BlazeStrikeVisuals, built at runtime and tuned through `look`.
    /// </summary>
    public class BlazeStrikeAbility : AbilityBase
    {
        public enum Phase { Idle, Grace, Charging, Ready }

        [SerializeField] Hitbox strikeHitbox;

        [Header("Timing")]
        [Tooltip("Seconds Attack must be held before the charge starts (and starts to show). Longer than a tap and past the swing, so a tap never shows Blaze.")]
        [SerializeField] float chargeDelay = 0.2f;
        [Tooltip("Seconds from the press until the charge is full (READY). The heat fills between chargeDelay and this.")]
        [SerializeField] float chargeTime = 0.6f;
        [Tooltip("Seconds the strike's hitbox stays open.")]
        [SerializeField] float activeTime = 0.2f;

        [Header("Impact")]
        [Tooltip("Real seconds the action freezes when the strike lands (0.05 = about 3 frames at 60 fps). 0 = no hitstop. " +
                 "Pausing doesn't count, and it multiplies with Serenity's slow motion instead of replacing it.")]
        [SerializeField] float hitstop = 0.05f;
        [Tooltip("Game speed during the hitstop.")]
        [SerializeField, Range(0.05f, 1f)] float hitstopSpeed = 0.05f;
        [Tooltip("Camera shake on the strike (world units). 0 = none.")]
        [SerializeField] float strikeShake = 0.1f;
        [Tooltip("Camera shake when the strike lands (world units). 0 = none.")]
        [SerializeField] float hitShake = 0.18f;
        [Tooltip("Seconds each camera shake lasts.")]
        [SerializeField] float shakeTime = 0.15f;

        [Header("Sound (optional: any can be empty)")]
        [Tooltip("Plays once when the charge starts (after chargeDelay).")]
        [SerializeField] AudioClip chargeStartSfx;
        [Tooltip("Loops while charging and READY, rising in pitch as it fills. Stops on release or cancel.")]
        [SerializeField] AudioClip chargeLoopSfx;
        [Tooltip("Plays once when the charge is full (READY).")]
        [SerializeField] AudioClip readySfx;
        [Tooltip("Plays on the strike (the release whoosh).")]
        [SerializeField] AudioClip releaseSfx;
        [Tooltip("Plays when the strike lands on something.")]
        [SerializeField] AudioClip hitSfx;
        [Tooltip("Volume of these sounds, on top of the game's SFX volume.")]
        [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;

        [Header("Blade anchors (where the heat sits on the sword)")]
        [Tooltip("The sword hand and blade tip on each player sprite, in that sprite's units from its pivot, as drawn (facing right). " +
                 "\"run\" covers every run_ and runAttack_ frame; a full name (\"jump_0004\") is one frame; the longest match wins; empty = any sprite. " +
                 "Select this component to see the blade it uses for the current frame (orange gizmo line, white circle = tip); pause Play mode on a pose to check it.")]
        [SerializeField] List<BladeAnchor> bladeAnchors = BladeAnchor.PlayerDefaults();

        [Tooltip("Sizes, colors and counts of every Blaze effect.")]
        [SerializeField] BlazeStrikeLook look = new();

        Phase phase;
        float heldFor;
        BlazeStrikeVisuals visuals;
        SpriteRenderer ownerSprite;
        Collider2D ownerBody;
        Sprite anchorSprite; // the frame `anchor` was looked up for
        BladeAnchor anchor;
        AudioSource loopSource;
        Coroutine hitstopRoutine;
        Health watchedHealth;

        public override AbilityId Id => AbilityId.BlazeStrike;
        /// <summary>Where the charge is. The visuals read it every frame.</summary>
        public Phase ChargePhase => phase;
        /// <summary>0 -> 1 over the visible charge (0 before chargeDelay), 1 at READY.</summary>
        public float ChargeFraction => phase == Phase.Ready ? 1f
            : phase == Phase.Charging ? Mathf.Clamp01((heldFor - chargeDelay) / Mathf.Max(0.01f, chargeTime - chargeDelay)) : 0f;
        /// <summary>The strike's hitbox: things only a full Blaze Strike can break (Violet's crystal gate) check for it.</summary>
        public Hitbox StrikeHitbox => strikeHitbox;

        BlazeStrikeVisuals Visuals => visuals ? visuals : visuals = BlazeStrikeVisuals.Create(this, look);

        void OnEnable()
        {
            watchedHealth = Owner?.Health;
            if (watchedHealth)
            {
                watchedHealth.Damaged += OnOwnerHurt;
                watchedHealth.Died += CancelCharge;
            }
            if (strikeHitbox) strikeHitbox.Landed += OnStrikeLanded;
        }

        void OnDisable()
        {
            if (watchedHealth)
            {
                watchedHealth.Damaged -= OnOwnerHurt;
                watchedHealth.Died -= CancelCharge;
            }
            watchedHealth = null;
            if (strikeHitbox)
            {
                strikeHitbox.Landed -= OnStrikeLanded;
                strikeHitbox.gameObject.SetActive(false);
            }
            CancelCharge();
            EndHitstop();
        }

        void OnValidate()
        {
            chargeDelay = Mathf.Max(0f, chargeDelay);
            chargeTime = Mathf.Max(chargeTime, chargeDelay);
            anchorSprite = null; // anchors may have changed
        }

        // ---------- Input (player) ----------

        public override bool HandleInput(in PlayerIntent intent)
        {
            // The press's own swing fires as usual (PlayerController sees this return false); a hold only arms the charge.
            if (intent.attackPressed)
            {
                CancelCharge();
                phase = Phase.Grace;
            }
            if (phase == Phase.Idle) return false;

            if (intent.attackReleased)
            {
                bool ready = phase == Phase.Ready;
                CancelCharge();
                return ready && TryActivate();
            }
            // Gameplay blocks (dialogue, pause, the Tip card) clear both held and released: never keep a partial charge.
            if (!intent.attackHeld)
            {
                CancelCharge();
                return false;
            }

            heldFor += Time.deltaTime;
            if (phase == Phase.Grace && heldFor >= chargeDelay) StartCharging();
            if (phase == Phase.Charging && heldFor >= chargeTime) BecomeReady();
            return false;
        }

        void StartCharging()
        {
            phase = Phase.Charging;
            _ = Visuals; // built now, so the first heated frame shows
            Sfx(chargeStartSfx);
            if (!chargeLoopSfx) return;
            if (!loopSource)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true;
                loopSource.spatialBlend = 0f;
            }
            loopSource.clip = chargeLoopSfx;
            UpdateLoop();
            loopSource.Play();
        }

        void BecomeReady()
        {
            phase = Phase.Ready;
            Visuals.Ready();
            Sfx(readySfx);
        }

        void OnOwnerHurt(DamageInfo _) => CancelCharge();

        void CancelCharge()
        {
            phase = Phase.Idle;
            heldFor = 0f;
            if (loopSource) loopSource.Stop();
        }

        // ---------- The strike (player release, or Green's TryActivate) ----------

        protected override void Activate()
        {
            if (strikeHitbox == null) { Debug.LogWarning("BlazeStrike has no hitbox."); return; }
            int facing = Owner.FacingSign;
            var p = strikeHitbox.transform.localPosition;
            p.x = Mathf.Abs(p.x) * facing;
            strikeHitbox.transform.localPosition = p;
            strikeHitbox.team = Owner.Team; // stays correct when Green steals it
            strikeHitbox.Open(activeTime);
            // The player swings (Attack / RunAttack / JumpAttack, like a basic attack); Green animates itself.
            if (Owner.Root.TryGetComponent<PlayerAnimator>(out var animator)) animator.PlayAttack();
            Visuals.PlayStrike(strikeHitbox, facing);
            Shake(strikeShake);
            Sfx(releaseSfx);
        }

        void OnStrikeLanded(Collider2D target, Vector2 point)
        {
            if (visuals) visuals.HitSparks(point);
            Shake(hitShake);
            Sfx(hitSfx);
            if (hitstop <= 0f || !Game.Time || !isActiveAndEnabled) return;
            EndHitstop();
            hitstopRoutine = StartCoroutine(Hitstop());
        }

        /// <summary>A brief freeze through Game.Time: it multiplies with any slow motion and lifts by itself.</summary>
        IEnumerator Hitstop()
        {
            Game.Time.SetScale(this, hitstopSpeed);
            float held = 0f;
            while (held < hitstop)
            {
                yield return null;
                if (!Game.Time) yield break;
                if (!Game.Time.IsPaused) held += Time.unscaledDeltaTime;
            }
            Game.Time.ClearScale(this);
            hitstopRoutine = null;
        }

        void EndHitstop()
        {
            if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
            hitstopRoutine = null;
            if (Game.Time) Game.Time.ClearScale(this);
        }

        void LateUpdate()
        {
            if (Owner == null) return;
            if (Owner.Health && Owner.Health.IsDead)
            {
                if (phase != Phase.Idle) CancelCharge();
                if (strikeHitbox && strikeHitbox.gameObject.activeSelf) strikeHitbox.gameObject.SetActive(false);
                return;
            }
            if (loopSource && loopSource.isPlaying) UpdateLoop();
        }

        void UpdateLoop()
        {
            float charge = ChargeFraction;
            loopSource.volume = sfxVolume * (Game.Audio ? Game.Audio.SfxVolume : 1f) * Mathf.Lerp(0.5f, 1f, charge);
            loopSource.pitch = Mathf.Lerp(0.9f, 1.15f, charge);
        }

        void Shake(float amount)
        {
            if (amount > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(amount, shakeTime);
        }

        void Sfx(AudioClip clip)
        {
            if (clip && Game.Audio) Game.Audio.PlaySfx(clip, sfxVolume);
        }

        // ---------- Where things are (for the visuals) ----------

        /// <summary>The owner's animated sprite (its "Visual" child). Its current frame picks the blade anchors.</summary>
        public SpriteRenderer OwnerSprite
        {
            get
            {
                if (ownerSprite) return ownerSprite;
                var root = Owner != null ? Owner.Root : (GetComponentInParent<IActor>() as Component)?.transform;
                if (!root) return null;
                var visual = root.Find("Visual");
                ownerSprite = visual ? visual.GetComponent<SpriteRenderer>() : root.GetComponentInChildren<SpriteRenderer>();
                return ownerSprite;
            }
        }

        /// <summary>The sword hand and blade tip in world space, for the owner's current sprite frame (see bladeAnchors).</summary>
        public bool TryGetBlade(out Vector2 hand, out Vector2 tip)
        {
            hand = tip = default;
            var sr = OwnerSprite;
            if (!sr || !sr.sprite) return false;
            if (sr.sprite != anchorSprite)
            {
                anchorSprite = sr.sprite;
                anchor = BladeAnchor.Find(bladeAnchors, anchorSprite.name);
            }
            float flip = sr.flipX ? -1f : 1f;
            var t = sr.transform;
            hand = t.TransformPoint(new Vector3(anchor.hand.x * flip, anchor.hand.y, 0f));
            tip = t.TransformPoint(new Vector3(anchor.tip.x * flip, anchor.tip.y, 0f));
            return true;
        }

        /// <summary>The bottom-center of the owner's body (the warm light under the feet sits here).</summary>
        public Vector2 Feet
        {
            get
            {
                if (Owner == null) return transform.position;
                if (!ownerBody) ownerBody = Owner.Root.GetComponent<Collider2D>();
                if (!ownerBody) return Owner.Root.position;
                var b = ownerBody.bounds;
                return new Vector2(b.center.x, b.min.y);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!TryGetBlade(out var hand, out var tip)) return;
            Gizmos.color = new Color(1f, 0.5f, 0.1f);
            Gizmos.DrawLine(hand, tip);
            Gizmos.DrawWireSphere(hand, 0.08f);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(tip, 0.05f);
        }
#endif
    }

    /// <summary>
    /// Where the sword's grip and tip are on a player sprite, in that sprite's own units (pixels / pixels-per-unit) from
    /// its pivot, as drawn (facing right, before any flip). `sprite` is a sprite name or the start of one, so one entry
    /// can cover a whole animation and another can fix a single frame of it: the longest match wins.
    /// </summary>
    [Serializable]
    public struct BladeAnchor
    {
        [Tooltip("A sprite name, or the start of one: \"run\" = every run_ and runAttack_ frame, \"jump_0004\" = that frame. The longest match wins; empty = any sprite.")]
        public string sprite;
        [Tooltip("The sword hand (the grip), in the sprite's units from its pivot, as drawn facing right.")]
        public Vector2 hand;
        [Tooltip("The blade's tip, same units.")]
        public Vector2 tip;

        public BladeAnchor(string sprite, Vector2 hand, Vector2 tip)
        {
            this.sprite = sprite;
            this.hand = hand;
            this.tip = tip;
        }

        static readonly BladeAnchor Fallback = new("", new Vector2(-0.33f, -0.03f), new Vector2(-1.09f, -0.28f));

        /// <summary>
        /// Measured from Art/Player (grip = the front glove, tip = the blade's end). The rest poses hold the sword low
        /// and trailing; idle and run barely move it, so they get one entry each. The jump and land frames, and the
        /// swing frames (the sword forward or up), each get their own.
        /// </summary>
        public static List<BladeAnchor> PlayerDefaults() => new()
        {
            Fallback,
            new("idle", new Vector2(-0.33f, -0.03f), new Vector2(-1.09f, -0.28f)),
            new("run", new Vector2(-0.39f, 0.02f), new Vector2(-1.20f, -0.22f)),
            new("runAttack_0001", new Vector2(0.53f, 0.71f), new Vector2(0.71f, 1.16f)),
            new("runAttack_0002", new Vector2(0.54f, 0.71f), new Vector2(0.71f, 1.10f)),
            new("jump", new Vector2(-0.28f, 0.19f), new Vector2(-1.06f, -0.23f)),
            new("jump_0001", new Vector2(-0.31f, 0.14f), new Vector2(-1.03f, -0.21f)),
            new("jump_0004", new Vector2(-0.25f, -0.16f), new Vector2(-0.99f, -0.53f)),
            new("jump_0005", new Vector2(-0.33f, -0.02f), new Vector2(-1.05f, -0.25f)),
            new("jumpAttack_0001", new Vector2(0.40f, 0.34f), new Vector2(-0.19f, 0.57f)),
            new("jumpAttack_0002", new Vector2(0.40f, 0.34f), new Vector2(-0.19f, 0.57f)),
            new("attack", new Vector2(-0.33f, -0.01f), new Vector2(-1.10f, -0.26f)),
            new("attack_0000", new Vector2(-0.03f, 0.06f), new Vector2(-1.05f, -0.24f)),
            new("attack_0001", new Vector2(0.41f, 0.29f), new Vector2(0.72f, 0.36f)),
            new("attack_0002", new Vector2(0.15f, 0.65f), new Vector2(0.84f, 1.13f)),
            new("attack_0003", new Vector2(0.15f, 0.65f), new Vector2(0.84f, 1.13f)),
            new("attack_0004", new Vector2(0.25f, 0.08f), new Vector2(0.77f, 0.03f)),
        };

        static List<BladeAnchor> defaults;
        /// <summary>The measured defaults, shared (read only): the Tip card's demo draws its blade with these.</summary>
        public static List<BladeAnchor> Defaults => defaults ??= PlayerDefaults();

        /// <summary>The entry for `spriteName`: the longest `sprite` it starts with (the built-in fallback if none does).</summary>
        public static BladeAnchor Find(List<BladeAnchor> anchors, string spriteName)
        {
            var best = Fallback;
            int bestLength = -1;
            if (anchors == null || spriteName == null) return best;
            foreach (var a in anchors)
            {
                string key = a.sprite ?? "";
                if (key.Length > bestLength && spriteName.StartsWith(key, StringComparison.Ordinal))
                {
                    best = a;
                    bestLength = key.Length;
                }
            }
            return best;
        }
    }
}
