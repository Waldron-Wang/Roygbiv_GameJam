using UnityEngine;

namespace Roygbiv
{
    /// <summary>Red's reward. Hold Attack to charge, release for a heavy strike.</summary>
    public class BlazeStrikeAbility : AbilityBase
    {
        [SerializeField] Hitbox strikeHitbox;
        [SerializeField] float chargeTime = 0.6f;
        [SerializeField] float activeTime = 0.2f;

        float heldFor, strikeAt;
        bool charging;
        SpriteRenderer chargeGlow, strikeSlash;
        SpriteRenderer[] chargeParticles;
        Transform chargeLook;
        Sprite glowSprite;
        Texture2D glowTexture;

        public override AbilityId Id => AbilityId.BlazeStrike;
        public float ChargeFraction => Mathf.Clamp01(heldFor / chargeTime); // for UI / VFX
        /// <summary>The strike's hitbox: things only a full Blaze Strike can break (Violet's crystal gate) check for it.</summary>
        public Hitbox StrikeHitbox => strikeHitbox;

        public override bool HandleInput(in PlayerIntent intent)
        {
            // Defer this press's basic swing until release, so a hold never also swings.
            if (intent.attackPressed) { heldFor = 0f; charging = true; }
            if (!charging) return false;
            if (intent.attackHeld) heldFor += Time.deltaTime;
            if (intent.attackReleased)
            {
                bool charged = heldFor >= chargeTime;
                CancelCharge();
                if (charged) TryActivate();
                else if (Owner?.Root && Owner.Root.TryGetComponent<PlayerCombat>(out var combat))
                    combat.TryAttack(Owner.FacingSign);
                return true;
            }
            // Gameplay blocks clear both held and released: never retain a partial charge.
            if (!intent.attackHeld) CancelCharge();
            return intent.attackPressed;
        }

        void CancelCharge()
        {
            charging = false;
            heldFor = 0f;
            if (chargeLook) chargeLook.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            CancelCharge();
            if (strikeHitbox) strikeHitbox.gameObject.SetActive(false);
        }

        protected override void Activate()
        {
            if (strikeHitbox == null) { Debug.LogWarning("BlazeStrike has no hitbox."); return; }
            var p = strikeHitbox.transform.localPosition;
            p.x = Mathf.Abs(p.x) * Owner.FacingSign;
            strikeHitbox.transform.localPosition = p;
            strikeHitbox.team = Owner.Team; // stays correct when Green steals it
            strikeHitbox.Open(activeTime);
            strikeAt = Time.time;
            EnsureEffects();
            DrawStrike();
        }

        // Gauntlet's Blaze VFX, in world space: soft red glow, five rising diamonds, then its crescent.
        void EnsureEffects()
        {
            if (chargeGlow) return;
            chargeLook = new GameObject("BlazeCharge").transform;
            chargeLook.SetParent(transform, false);
            // Reproduce CardGui.Glow's soft falloff without drawing gameplay through IMGUI.
            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            glowTexture.SetPixels32(pixels);
            glowTexture.Apply();
            glowSprite = Sprite.Create(glowTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            chargeGlow = MakeSprite("BlazeGlow", glowSprite, chargeLook, -1);
            chargeParticles = new SpriteRenderer[5];
            for (int i = 0; i < chargeParticles.Length; i++)
            {
                chargeParticles[i] = MakeSprite("BlazeEmber", FlatSprite.Square, chargeLook, 2);
                chargeParticles[i].transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            strikeSlash = MakeSprite("BlazeSlash", VioletShapes.Crescent, strikeHitbox.transform, 2);
            chargeLook.gameObject.SetActive(false);
            strikeSlash.enabled = false;
        }

        SpriteRenderer MakeSprite(string name, Sprite shape, Transform parent, int orderOffset)
        {
            var sprite = VioletShapes.Create(name, shape, parent, Vector2.zero, UiKit.Accent(ColorId.Red), 10 + orderOffset);
            var visual = Owner.Root.GetComponentInChildren<SpriteRenderer>();
            if (visual) { sprite.sortingLayerID = visual.sortingLayerID; sprite.sortingOrder = visual.sortingOrder + orderOffset; }
            return sprite;
        }

        void LateUpdate()
        {
            if (Owner == null || !strikeHitbox) return;
            if (Owner.Health && Owner.Health.IsDead)
            {
                CancelCharge();
                strikeHitbox.gameObject.SetActive(false);
                return;
            }
            if (charging)
            {
                EnsureEffects();
                chargeLook.gameObject.SetActive(true);
                // The Tip draws at 90 pixels per player unit; convert only its VFX offsets/sizes.
                const float unit = 90f;
                var body = Owner.Root.GetComponent<Collider2D>();
                Vector3 feet = body ? transform.InverseTransformPoint(new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z)) : Vector3.down * 0.6f;
                float charge = ChargeFraction;
                var blaze = UiKit.Accent(ColorId.Red); // Gauntlet's BlazeColor (#FF4A3D).
                chargeGlow.transform.localPosition = feet + new Vector3(10f / unit * Owner.FacingSign, 60f / unit, 0f);
                chargeGlow.transform.localScale = Vector3.one * (50f + 70f * charge) * 2f / unit;
                chargeGlow.color = UiKit.WithAlpha(blaze, 0.25f + 0.45f * charge);
                for (int i = 0; i < chargeParticles.Length; i++)
                {
                    float k = Mathf.Repeat(Time.time * 2f + i * 0.2f, 1f);
                    var particle = chargeParticles[i];
                    particle.transform.localPosition = feet + new Vector3((-30f + i * 16f) / unit, (20f + 100f * k) / unit, 0f);
                    particle.transform.localScale = Vector3.one * 8f * (1f - k) / unit;
                    particle.color = UiKit.WithAlpha(blaze, charge * (1f - k));
                }
            }
            if (strikeHitbox.gameObject.activeInHierarchy && strikeSlash) DrawStrike();
        }

        void DrawStrike()
        {
            var box = strikeHitbox.GetComponent<BoxCollider2D>();
            if (!box) return;
            float direction = Mathf.Sign(strikeHitbox.transform.localPosition.x);
            float progress = Mathf.Clamp01((Time.time - strikeAt) / Mathf.Max(0.01f, activeTime));
            // Same crescent, proportions and tilt as Gauntlet's 110 x 190 scale; center it on the real hitbox.
            var scale = new Vector3(110f / 90f, 190f / 90f, 1f);
            strikeSlash.flipX = direction > 0f;
            strikeSlash.transform.localRotation = Quaternion.Euler(0f, 0f, 10f * direction);
            var center = strikeSlash.sprite.bounds.center;
            center.x *= direction > 0f ? -1f : 1f;
            strikeSlash.transform.localScale = scale;
            strikeSlash.transform.localPosition = (Vector3)box.offset - strikeSlash.transform.localRotation * Vector3.Scale(center, scale);
            strikeSlash.color = UiKit.WithAlpha(UiKit.Accent(ColorId.Red), 1f - progress);
            strikeSlash.enabled = true;
        }

        void OnDestroy()
        {
            if (chargeLook) Destroy(chargeLook.gameObject);
            if (strikeSlash) Destroy(strikeSlash.gameObject);
            if (glowSprite) Destroy(glowSprite);
            if (glowTexture) Destroy(glowTexture);
        }
    }
}
