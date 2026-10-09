using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// Every size, color and count of Blaze Strike's effects. Shown on BlazeStrikeAbility as "Look"; the defaults are the
    /// intended look, so a prefab that has never touched these gets them as they are here.
    /// </summary>
    [Serializable]
    public class BlazeStrikeLook
    {
        [Header("Colors")]
        [Tooltip("The fire as it heats, left to right: barely warm (dark red) -> orange -> white-hot. The hand's flame, the blade, the embers and the light at the feet pick from it by how charged it is.")]
        public Gradient heat = NewHeatGradient();
        [Tooltip("Multiplied into every Blaze color. White = Blaze's own colors.")]
        public Color tint = Color.white;
        [Tooltip("The strike's crescent across its width: the thin white-hot leading edge...")]
        public Color strikeCore = new(1f, 0.96f, 0.82f);
        [Tooltip("...its orange body...")]
        public Color strikeBody = new(1f, 0.5f, 0.08f);
        [Tooltip("...and its red outer edge with the ragged flame licks.")]
        public Color strikeEdge = new(0.78f, 0.07f, 0.02f);

        [Header("Charge: the hand's flame")]
        [Tooltip("Height of the flame at the sword hand (world units) when the charge starts and when it's full.")]
        public Vector2 auraHeight = new(0.25f, 0.65f);
        [Tooltip("How much the flame's size flickers. 0 = steady.")]
        [Range(0f, 0.5f)] public float auraFlicker = 0.15f;
        [Tooltip("How fast every flame flickers.")]
        public float flickerSpeed = 12f;
        [Tooltip("Seconds the charge effects take to fade out on a release or a cancel.")]
        public float fadeOut = 0.1f;

        [Header("Charge: the blade (the gauge)")]
        [Tooltip("Glowing segments along the blade. The heat creeps from the hilt to the tip as the charge fills; when it reaches the tip, it's READY. Read when the effects are first built.")]
        [Range(4, 24)] public int bladeBeads = 12;
        [Tooltip("Thickness of the glowing blade (world units). Its soft halo is 2.4x this; the bright point at the heat's front is 3.2x.")]
        public float beadSize = 0.12f;
        [Tooltip("Flame licks per second off the lit part of the blade, at full charge.")]
        public float bladeFlameRate = 22f;
        [Tooltip("Flame licks per second off the whole blade while READY is held.")]
        public float readyFlameRate = 48f;
        [Tooltip("Size of a flame lick (world units).")]
        public float flameSize = 0.16f;
        [Tooltip("How fast the licks rise (world units per second).")]
        public float flameRise = 1.2f;

        [Header("Charge: embers pulled in")]
        [Tooltip("Embers per second when the charge starts and just before it's full. They stop at READY.")]
        public Vector2 emberRate = new(14f, 50f);
        [Tooltip("Embers start this far from the sword hand (min, max world units).")]
        public Vector2 emberRadius = new(0.8f, 1.5f);
        [Tooltip("Seconds an ember takes to reach the hand (min, max).")]
        public Vector2 emberTime = new(0.22f, 0.38f);
        [Tooltip("Ember thickness (world units). They're drawn as short streaks along their path.")]
        public float emberSize = 0.06f;
        [Tooltip("How much of the player's own movement the embers follow: 1 = they always land on the hand, lower = they trail behind while moving.")]
        [Range(0f, 1f)] public float emberFollow = 0.75f;

        [Header("Charge: feet and heat haze")]
        [Tooltip("Width and height of the warm light under the feet (world units).")]
        public Vector2 underLightSize = new(1.6f, 0.45f);
        [Tooltip("Opacity of that light at full charge.")]
        [Range(0f, 1f)] public float underLightAlpha = 0.35f;
        [Tooltip("Size of the heat-haze patch over the hand (world units).")]
        public float shimmerSize = 0.9f;
        [Tooltip("How much the haze bends the picture at full charge (fraction of the screen). 0 = off.")]
        [Range(0f, 0.02f)] public float shimmerStrength = 0.004f;

        [Header("READY")]
        [Tooltip("Size of the glint that flashes at the blade tip when the charge is full (world units).")]
        public float glintSize = 0.95f;
        [Tooltip("Size of the tip's twinkle while READY is held.")]
        public float twinkleSize = 0.32f;
        [Tooltip("Final radius of the ring of flame that bursts from the hand (world units).")]
        public float readyRingRadius = 0.9f;
        [Tooltip("Seconds the READY glint and ring take.")]
        public float readyBurstTime = 0.28f;
        [Tooltip("Embers thrown out by the READY burst.")]
        [Range(0, 60)] public int readyBurstEmbers = 18;

        [Header("Strike: the flaming crescent")]
        [Tooltip("Seconds the crescent takes to sweep from the top of the swing to the bottom.")]
        public float sweepTime = 0.09f;
        [Tooltip("Seconds it then takes to burn out.")]
        public float burnTime = 0.17f;
        [Tooltip("How far the swing reaches above and below straight ahead (degrees).")]
        [Range(30f, 90f)] public float arcAngle = 78f;
        [Tooltip("How round the arc is: its horizontal radius as a fraction of the hitbox's width (bigger = flatter, wider crescent).")]
        [Range(0.5f, 1.5f)] public float arcDepth = 0.85f;
        [Tooltip("The crescent's rim sits at this fraction of the strike hitbox and its flame licks reach about to the hitbox's edge, so what you see is what hits.")]
        [Range(0.6f, 1f)] public float rimFit = 0.88f;
        [Tooltip("Thickness of the crescent at its middle, as a fraction of its radius.")]
        [Range(0.1f, 0.8f)] public float thickness = 0.42f;
        [Tooltip("Length of the ragged flame licks on its outer edge, as a fraction of its radius.")]
        [Range(0f, 0.3f)] public float lickLength = 0.12f;
        [Tooltip("How many flame licks along the arc.")]
        [Range(2f, 20f)] public float lickCount = 9f;
        [Tooltip("Opacity of the motion smear trailing inside the arc.")]
        [Range(0f, 1f)] public float smear = 0.3f;
        [Tooltip("Embers sprayed along the swing.")]
        [Range(0, 80)] public int strikeEmbers = 30;
        [Tooltip("Their speed (min, max world units per second).")]
        public Vector2 strikeEmberSpeed = new(4f, 9f);
        [Tooltip("The heat ripple's radius (world units).")]
        public float rippleRadius = 1.6f;
        [Tooltip("How much the ripple bends the picture (fraction of the screen). 0 = off.")]
        [Range(0f, 0.05f)] public float rippleStrength = 0.012f;
        [Tooltip("Seconds the ripple takes to spread.")]
        public float rippleTime = 0.28f;

        [Header("Hit")]
        [Tooltip("Sparks that burst off the target when the strike lands.")]
        [Range(0, 80)] public int hitSparks = 22;
        [Tooltip("Their speed (min, max world units per second).")]
        public Vector2 hitSparkSpeed = new(5f, 12f);
        [Tooltip("Size of the flash on the target (world units).")]
        public float hitFlashSize = 1.1f;
        [Tooltip("Seconds the flash takes to fade.")]
        public float hitFlashTime = 0.12f;

        [Header("Sorting")]
        [Tooltip("The effects draw this many sorting orders above the owner's top sprite (the light at the feet draws just under its sprite). Read when the effects are first built.")]
        public int sortingOffset = 2;

        /// <summary>The heat color at `k` (0 = barely warm, 1 = white-hot), tinted, fully opaque.</summary>
        public Color Heat(float k)
        {
            var c = (heat ?? DefaultHeatGradient).Evaluate(Mathf.Clamp01(k)) * tint;
            c.a = 1f;
            return c;
        }

        static Gradient defaultHeat;
        static Gradient DefaultHeatGradient => defaultHeat ??= NewHeatGradient();

        /// <summary>The default heat colors (the Tip card's demo draws with these).</summary>
        public static Color DefaultHeat(float k)
        {
            var c = DefaultHeatGradient.Evaluate(Mathf.Clamp01(k));
            c.a = 1f;
            return c;
        }

        static Gradient NewHeatGradient()
        {
            var g = new Gradient();
            g.SetKeys(new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.02f, 0.01f), 0f),
                    new GradientColorKey(new Color(0.85f, 0.1f, 0.02f), 0.3f),
                    new GradientColorKey(new Color(1f, 0.42f, 0.05f), 0.6f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.3f), 0.85f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.88f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }

    /// <summary>
    /// Blaze Strike's effects, built at runtime under the ability (no prefab art, no packages):
    ///   Charging  embers streak in to the sword hand; a flame at the hand grows and heats (dark red -> orange ->
    ///             white-hot); heat creeps up the blade from the hilt to the tip (that's the gauge: no bar); a warm
    ///             light at the feet and a little heat haze.
    ///   READY     a glint at the blade tip and a ring of flame, then flames keep licking up the whole blade while held.
    ///   Strike    a flaming crescent fitted to the strike's hitbox (white-hot core, orange body, red ragged edge,
    ///             motion smear), embers sprayed along the swing, a heat ripple. On a hit: sparks and a flash.
    /// It reads BlazeStrikeAbility's phase and blade every frame; the ability calls Ready / PlayStrike / HitSparks.
    /// Everything is placed in world space (embers trail when moving) and sorted around the owner's sprites.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerAnimator (facing) and the Animator (sprite frame)
    public class BlazeStrikeVisuals : MonoBehaviour
    {
        const float Tau = Mathf.PI * 2f;
        const int SlashSteps = 96;
        const int AlphaRings = 7, AddRings = 3, RingsPerStep = AlphaRings + AddRings;

        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int ShimmerId = Shader.PropertyToID("_Shimmer");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
        static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");

        static Shader fxShader, heatShader;
        static Material alphaMat, addMat, dotAddMat, flameAlphaMat;
        static Sprite dot, flame, star, ring;
        static bool warned;

        // Domain reload is off: drop the cached assets each Play session (they're rebuilt lazily).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            fxShader = heatShader = null;
            alphaMat = addMat = dotAddMat = flameAlphaMat = null;
            dot = flame = star = ring = null;
            warned = false;
        }

        BlazeStrikeAbility ability;
        BlazeStrikeLook look;
        int layer, front, back;
        float z;

        SpriteRenderer auraOuter, auraMid, auraCore, heatFront, underLight, glint, readyRing, shimmer, ripple, hitFlash;
        SpriteRenderer[] beadHalos, beadCores;
        ParticleSystem inEmbers, outEmbers, flames, sparks;
        Material shimmerMat, rippleMat;
        MeshRenderer slash;
        Mesh slashMesh;
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> alphaTris = new(), addTris = new();

        float fade, shownCharge, readyAt = float.NegativeInfinity, emberDebt, flameDebt;
        bool shownReady, chargeShown, linearSpace;
        Hitbox strikeBox;
        BoxCollider2D strikeCollider;
        int strikeFacing = 1, sprayed;
        float strikeAt = float.NegativeInfinity, rippleAt = float.NegativeInfinity, hitAt = float.NegativeInfinity, seed;
        Vector2 ripplePoint, hitPoint;

        /// <summary>Builds the effects under `ability` (they live and die with it).</summary>
        public static BlazeStrikeVisuals Create(BlazeStrikeAbility ability, BlazeStrikeLook look)
        {
            var go = new GameObject("BlazeVFX");
            go.transform.SetParent(ability.transform, false);
            var visuals = go.AddComponent<BlazeStrikeVisuals>();
            visuals.Build(ability, look ?? new BlazeStrikeLook());
            return visuals;
        }

        /// <summary>The charge just filled: the glint at the tip, the ring of flame and a burst of embers.</summary>
        public void Ready()
        {
            readyAt = Time.time;
            if (!ability.TryGetBlade(out var hand, out var tip)) return;
            int n = look.readyBurstEmbers;
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.Range(-0.3f, 0.3f)) / Mathf.Max(1, n) * Tau;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var from = i % 3 == 0 ? tip : hand;
                Emit(outEmbers, from + dir * 0.08f, dir * Random.Range(2.5f, 5f), look.emberSize * Random.Range(1f, 1.6f),
                     Random.Range(0.25f, 0.4f), look.Heat(Random.Range(0.75f, 1f)));
            }
        }

        /// <summary>The strike: the crescent sweeps through `box` (it follows it), with its embers and heat ripple.</summary>
        public void PlayStrike(Hitbox box, int facing)
        {
            strikeBox = box;
            strikeCollider = box ? box.GetComponent<BoxCollider2D>() : null;
            strikeFacing = facing >= 0 ? 1 : -1;
            strikeAt = Time.time;
            sprayed = 0;
            seed = Random.value * 10f;
            if (StrikeFrame(out var e, out float rx, out _))
            {
                rippleAt = Time.time;
                ripplePoint = e + new Vector2(strikeFacing * rx * 0.55f, 0f);
            }
        }

        /// <summary>The strike landed at `at`: sparks fly off it and it flashes.</summary>
        public void HitSparks(Vector2 at)
        {
            hitAt = Time.time;
            hitPoint = at;
            for (int i = 0; i < look.hitSparks; i++)
            {
                float a = Random.Range(-75f, 75f) * Mathf.Deg2Rad;
                var dir = new Vector2(strikeFacing * Mathf.Cos(a), Mathf.Sin(a));
                if (Random.value < 0.2f) dir.x *= -0.6f; // a few kick back along the swing
                Emit(sparks, at, dir * Random.Range(look.hitSparkSpeed.x, look.hitSparkSpeed.y), Random.Range(0.035f, 0.07f),
                     Random.Range(0.12f, 0.32f), look.Heat(Random.Range(0.8f, 1f)));
            }
            for (int i = 0; i < 6; i++)
                Emit(outEmbers, at, Random.insideUnitCircle * 3f + new Vector2(strikeFacing * 1.5f, 1f), look.emberSize * 1.5f,
                     Random.Range(0.3f, 0.5f), look.Heat(Random.Range(0.6f, 0.9f)));
        }

        // ---------- Building ----------

        void Build(BlazeStrikeAbility owner, BlazeStrikeLook settings)
        {
            ability = owner;
            look = settings;
            EnsureAssets();

            // Sort around the owner's sprites: in front of all of them, the light at the feet just under the body.
            var root = owner.Owner != null ? owner.Owner.Root : owner.transform.root;
            int top = int.MinValue;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.transform.IsChildOf(transform) || sr.sortingOrder <= top) continue;
                top = sr.sortingOrder;
                layer = sr.sortingLayerID;
            }
            if (top == int.MinValue) top = 10;
            var body = owner.OwnerSprite;
            if (body) layer = body.sortingLayerID;
            // The player is one sprite: sort on it, not on whatever else hangs under it. A boss sorts above all its parts.
            if (body && root.GetComponent<PlayerAnimator>()) top = body.sortingOrder;
            front = top + look.sortingOffset;
            back = (body ? body.sortingOrder : top) - 1;

            auraOuter = MakeSprite("AuraOuter", flame, alphaMat, front + 1);
            auraMid = MakeSprite("AuraMid", flame, addMat, front + 2);
            auraCore = MakeSprite("AuraCore", flame, addMat, front + 3);
            int beads = Mathf.Clamp(look.bladeBeads, 1, 64);
            beadHalos = new SpriteRenderer[beads];
            beadCores = new SpriteRenderer[beads];
            for (int i = 0; i < beads; i++)
            {
                beadHalos[i] = MakeSprite("BeadHalo", dot, addMat, front);
                beadCores[i] = MakeSprite("Bead", dot, alphaMat, front + 1);
            }
            heatFront = MakeSprite("HeatFront", star, addMat, front + 3);
            underLight = MakeSprite("UnderLight", dot, addMat, back);
            readyRing = MakeSprite("ReadyRing", ring, addMat, front + 4);
            glint = MakeSprite("Glint", star, addMat, front + 5);
            hitFlash = MakeSprite("HitFlash", star, addMat, front + 6);
            if (heatShader)
            {
                shimmerMat = new Material(heatShader) { name = "BlazeShimmer", hideFlags = HideFlags.DontSave };
                rippleMat = new Material(heatShader) { name = "BlazeRipple", hideFlags = HideFlags.DontSave };
                shimmer = MakeSprite("HeatShimmer", dot, shimmerMat, front + 7);
                ripple = MakeSprite("HeatRipple", dot, rippleMat, front + 7);
            }

            flames = MakeParticles("Flames", flameAlphaMat, false, front + 1, 0f, 0f,
                Fade(0f, 0.95f, new Color(1f, 0.75f, 0.55f), new Color(0.55f, 0.12f, 0.06f)), Curve(0.5f, 1f, 0.15f));
            inEmbers = MakeParticles("EmbersIn", dotAddMat, true, front + 2, 0f, 0f,
                Fade(0f, 1f, new Color(0.7f, 0.7f, 0.7f), Color.white, brightenIn: true), Curve(0.6f, 1f, 0.8f));
            outEmbers = MakeParticles("EmbersOut", dotAddMat, true, front + 4, -0.15f, 2f,
                Fade(1f, 1f, new Color(1f, 0.6f, 0.35f), new Color(0.55f, 0.12f, 0.05f)), Curve(1f, 0.7f, 0.2f));
            sparks = MakeParticles("Sparks", dotAddMat, true, front + 6, 1.2f, 1f,
                Fade(1f, 1f, new Color(1f, 0.8f, 0.45f), new Color(0.9f, 0.3f, 0.05f)), Curve(1f, 0.8f, 0.3f));

            var slashGo = new GameObject("FlameCrescent");
            slashGo.transform.SetParent(transform, false);
            slashMesh = new Mesh { name = "BlazeCrescent", hideFlags = HideFlags.DontSave };
            slashMesh.MarkDynamic();
            slashGo.AddComponent<MeshFilter>().sharedMesh = slashMesh;
            slash = slashGo.AddComponent<MeshRenderer>();
            slash.sharedMaterials = new[] { alphaMat, addMat };
            slash.sortingLayerID = layer;
            slash.sortingOrder = front + 3;
            slash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            slash.receiveShadows = false;
            slash.enabled = false;

            HideCharge();
            if (ripple) ripple.enabled = false;
            hitFlash.enabled = false;
            PinRoot();
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        ParticleSystem MakeParticles(string name, Material material, bool streaks, int order, float gravity, float drag, Gradient overLife, AnimationCurve sizeOverLife)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // they stay where they were made: trails when moving
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.enabled = false; // emitted by hand, with Emit()
            var shape = ps.shape;
            shape.enabled = false;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(overLife);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLife);
            if (drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1000f;
                limit.drag = drag;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (streaks)
            {
                r.velocityScale = 0.035f;
                r.lengthScale = 1.2f;
            }
            r.maxParticleSize = 2f;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>
        /// Color over a particle's life (it multiplies the particle's own heat color): white -> `mid` -> `to` as it cools,
        /// or `mid` -> `to` when `brightenIn` (embers heating up as they arrive). Alpha goes from `startAlpha` to `peak`, then out.
        /// </summary>
        static Gradient Fade(float startAlpha, float peak, Color mid, Color to, bool brightenIn = false)
        {
            var g = new Gradient();
            g.SetKeys(brightenIn
                    ? new[] { new GradientColorKey(mid, 0f), new GradientColorKey(to, 0.8f) }
                    : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(mid, 0.45f), new GradientColorKey(to, 1f) },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(peak, 0.18f),
                    new GradientAlphaKey(peak * 0.85f, 0.75f), new GradientAlphaKey(0f, 1f),
                });
            return g;
        }

        static AnimationCurve Curve(float start, float peak, float end) =>
            new(new Keyframe(0f, start), new Keyframe(0.25f, peak), new Keyframe(1f, end));

        // ---------- Every frame ----------

        void LateUpdate()
        {
            PinRoot();
            if (ability && ability.Owner != null && ability.Owner.Root) z = ability.Owner.Root.position.z;
            float now = Time.time;
            UpdateCharge(now, Time.deltaTime);
            UpdateStrike(now);
            UpdateOneShots(now);
        }

        /// <summary>Keeps this root at the world origin with no scale, so every child is placed in plain world units.</summary>
        void PinRoot()
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var parent = transform.parent;
            if (!parent) return;
            var s = parent.lossyScale;
            transform.localScale = new Vector3(Inverse(s.x), Inverse(s.y), Inverse(s.z));
        }

        static float Inverse(float v) => Mathf.Abs(v) > 0.00001f ? 1f / v : 1f;

        void UpdateCharge(float now, float dt)
        {
            var phase = ability ? ability.ChargePhase : BlazeStrikeAbility.Phase.Idle;
            bool on = phase is BlazeStrikeAbility.Phase.Charging or BlazeStrikeAbility.Phase.Ready;
            if (on)
            {
                shownCharge = ability.ChargeFraction;
                shownReady = phase == BlazeStrikeAbility.Phase.Ready;
                fade = 1f;
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 0f, dt / Mathf.Max(0.01f, look.fadeOut));
                emberDebt = flameDebt = 0f;
            }

            Vector2 hand = default, tip = default;
            if (fade <= 0.001f || !ability || !ability.TryGetBlade(out hand, out tip))
            {
                if (chargeShown) HideCharge();
                return;
            }
            chargeShown = true;
            float c = shownCharge, a = fade;
            bool ready = shownReady;
            float flick = Mathf.PerlinNoise(now * look.flickerSpeed, 0.37f);
            float sway = (Mathf.PerlinNoise(0.61f, now * look.flickerSpeed * 1.3f) - 0.5f) * 16f;

            // The hand's flame: grows and heats with the charge (dark red -> orange -> white-hot core).
            float height = Mathf.Lerp(look.auraHeight.x, look.auraHeight.y, Smooth(c)) * (1f + look.auraFlicker * (flick - 0.5f) * 2f);
            if (ready) height *= 1f + 0.07f * Mathf.Sin(now * 22f);
            Place(auraOuter, hand, height, sway, look.Heat(0.15f + 0.6f * c), a * 0.9f * Mathf.Clamp01(0.4f + 2.4f * c));
            Place(auraMid, hand, height * 0.72f, sway * 0.6f, look.Heat(0.3f + 0.65f * c), a * 0.8f * Mathf.Clamp01(0.3f + 2f * c));
            Place(auraCore, hand, height * 0.42f, sway * 0.3f, look.Heat(1f), a * Mathf.InverseLerp(0.45f, 1f, c));

            // The blade is the gauge: each bead lights as the heat front passes it, hilt first, hotter near the hilt.
            // Each bead is stretched along the blade, so neighbours overlap into one hot streak rather than dots.
            int n = beadCores.Length;
            var blade = tip - hand;
            float along = Mathf.Atan2(blade.y, blade.x) * Mathf.Rad2Deg, step = blade.magnitude / n;
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n;
                float lit = ready ? 1f : Mathf.Clamp01((c - u) * n);
                float h = ready ? 0.75f + 0.25f * (1f - u) : Mathf.Clamp01(0.2f + c * 0.9f - u * 0.4f);
                float wobble = 0.85f + 0.3f * Mathf.PerlinNoise(now * 9f, i * 1.31f);
                var p = Vector2.Lerp(hand, tip, u);
                Place(beadHalos[i], p, new Vector2(Mathf.Max(step * 2.2f, look.beadSize * 2.4f), look.beadSize * 2.4f * wobble), along, look.Heat(h * 0.85f), a * lit * 0.5f);
                Place(beadCores[i], p, new Vector2(Mathf.Max(step * 1.8f, look.beadSize), look.beadSize * 0.7f * wobble), along, look.Heat(h + 0.2f), a * lit * 0.9f);
            }
            // A bright point where the heat has got to, until it reaches the tip (then the glint takes over).
            Place(heatFront, Vector2.Lerp(hand, tip, c), look.beadSize * 3.2f, now * 200f, look.Heat(1f), ready ? 0f : a * Mathf.Clamp01(c * 6f));

            if (on) EmitCharge(hand, tip, c, ready, dt);

            // A warm light at the feet and a little heat haze over the hand, both growing with the charge.
            float glow = a * look.underLightAlpha * Smooth(c) * (0.85f + 0.3f * flick);
            Place(underLight, ability.Feet, look.underLightSize, 0f, look.Heat(0.55f + 0.2f * c), glow);
            if (shimmer)
            {
                float strength = look.shimmerStrength * c * a;
                Place(shimmer, hand + Vector2.up * look.shimmerSize * 0.3f, Vector2.one * look.shimmerSize * (0.7f + 0.3f * c), 0f, Color.white, strength > 0f ? 1f : 0f);
                shimmerMat.SetFloat(StrengthId, strength);
                shimmerMat.SetFloat(ShimmerId, 1f);
                shimmerMat.SetFloat(RingId, 0f);
            }

            // READY: a ring of flame bursts from the hand, the tip glints, then keeps twinkling while it's held.
            float k = (now - readyAt) / Mathf.Max(0.01f, look.readyBurstTime);
            bool burst = ready && k >= 0f && k < 1f;
            float ringRadius = Mathf.Lerp(0.15f, look.readyRingRadius, 1f - (1f - k) * (1f - k));
            Place(readyRing, hand, ringRadius / 0.4f, 0f, look.Heat(0.7f), burst ? a * Mathf.Pow(1f - k, 1.5f) : 0f); // the ring is drawn at 0.4 of its sprite
            float glintSize = !ready ? 0f
                : k < 1f ? Mathf.Lerp(look.glintSize, look.twinkleSize, Smooth((k - 0.15f) / 0.85f)) * Mathf.Clamp01(k / 0.1f)
                : look.twinkleSize * (0.8f + 0.25f * Mathf.Sin(now * 17f) * Mathf.Sin(now * 5.3f));
            Place(glint, tip, glintSize, 45f * Mathf.Clamp01(k) + now * 40f, look.Heat(1f), a);
        }

        /// <summary>Flame licks off the lit part of the blade, and (until READY) embers pulled in to the hand.</summary>
        void EmitCharge(Vector2 hand, Vector2 tip, float c, bool ready, float dt)
        {
            float reach = ready ? 1f : c;
            flameDebt = Mathf.Min(flameDebt + (ready ? look.readyFlameRate : look.bladeFlameRate * c) * dt, 8f);
            for (; flameDebt >= 1f; flameDebt -= 1f)
            {
                float u = Random.Range(0f, reach);
                var p = Vector2.Lerp(hand, tip, u) + Random.insideUnitCircle * 0.04f;
                float heat = (ready ? Random.Range(0.55f, 0.95f) : Random.Range(0.35f, 0.75f) * (0.5f + 0.5f * c)) + (1f - u) * 0.1f;
                float size = look.flameSize * Random.Range(0.7f, 1.3f) * (ready ? 1.2f : 0.6f + 0.5f * c);
                Emit(flames, p, new Vector2(Random.Range(-0.25f, 0.25f), look.flameRise * Random.Range(0.7f, 1.3f)), size,
                     Random.Range(0.22f, 0.4f), look.Heat(heat));
            }

            if (ready) { emberDebt = 0f; return; }
            var body = ability.Owner?.Body;
            var follow = body ? body.linearVelocity * look.emberFollow : Vector2.zero;
            emberDebt = Mathf.Min(emberDebt + Mathf.Lerp(look.emberRate.x, look.emberRate.y, c) * dt, 8f);
            for (; emberDebt >= 1f; emberDebt -= 1f)
            {
                float angle = Random.value * Tau;
                var from = hand + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(look.emberRadius.x, look.emberRadius.y);
                float life = Mathf.Max(0.05f, Random.Range(look.emberTime.x, look.emberTime.y));
                Emit(inEmbers, from, (hand - from) / life + follow, look.emberSize * Random.Range(0.7f, 1.3f), life, look.Heat(Random.Range(0.55f, 0.95f)));
            }
        }

        void HideCharge()
        {
            chargeShown = false;
            auraOuter.enabled = auraMid.enabled = auraCore.enabled = heatFront.enabled = false;
            underLight.enabled = readyRing.enabled = glint.enabled = false;
            foreach (var b in beadHalos) b.enabled = false;
            foreach (var b in beadCores) b.enabled = false;
            if (shimmer) shimmer.enabled = false;
        }

        void UpdateStrike(float now)
        {
            float age = now - strikeAt;
            float sweep = Mathf.Max(0.01f, look.sweepTime), burn = Mathf.Max(0.01f, look.burnTime);
            if (age < 0f || age > sweep + burn || !StrikeFrame(out var e, out float rx, out float ry))
            {
                if (slash.enabled) slash.enabled = false;
                return;
            }
            float head = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / sweep), 3f); // the blade leads fast, then eases
            float burnK = Mathf.Clamp01((age - sweep) / burn);
            BuildCrescent(e, rx, ry, head, burnK, now);
            Spray(e, rx, ry, head);
            slash.enabled = true;
        }

        /// <summary>
        /// The crescent's ellipse, fitted to the strike hitbox: its far edge at the box's far side, its top and bottom
        /// inside it, all scaled by rimFit (the flame licks then reach about to the box's edge).
        /// </summary>
        bool StrikeFrame(out Vector2 center, out float rx, out float ry)
        {
            center = default;
            rx = ry = 0f;
            if (!strikeBox) return false;
            var t = strikeBox.transform;
            var s = t.lossyScale;
            var size = strikeCollider ? new Vector2(strikeCollider.size.x * Mathf.Abs(s.x), strikeCollider.size.y * Mathf.Abs(s.y)) : Vector2.one;
            Vector2 boxCenter = t.TransformPoint(strikeCollider ? strikeCollider.offset : Vector2.zero);
            rx = size.x * look.arcDepth;
            ry = size.y * 0.5f;
            center = boxCenter + new Vector2(strikeFacing * (size.x * 0.5f - rx), 0f);
            rx *= look.rimFit;
            ry *= look.rimFit;
            return true;
        }

        /// <summary>
        /// One frame of the crescent, as a mesh: per step along the arc, rings across its width from the inside out
        /// (smear, orange body, hot band, rim, red edge, ragged flame lick), then an additive white-hot core at the rim.
        /// `head` 0 -> 1 wipes it on from the top; `burnK` 0 -> 1 thins it and burns it away from the top.
        /// </summary>
        void BuildCrescent(Vector2 e, float rx, float ry, float head, float burnK, float now)
        {
            verts.Clear();
            colors.Clear();
            alphaTris.Clear();
            addTris.Clear();
            linearSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
            float f = strikeFacing;
            float top = look.arcAngle * Mathf.Deg2Rad;
            float thin = 1f - 0.55f * burnK, dim = 1f - burnK * burnK;
            Color core = look.strikeCore * look.tint, body = look.strikeBody * look.tint, edge = look.strikeEdge * look.tint;
            Color hot = Color.Lerp(body, core, 0.65f), rim = Color.Lerp(edge, body, 0.5f), ember = edge * 0.8f;

            for (int i = 0; i <= SlashSteps; i++)
            {
                float u = i / (float)SlashSteps;
                float theta = Mathf.Lerp(top, -top, u);
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.65f); // sin(PI) is a hair below 0 in float: Pow would NaN
                float reveal = Mathf.Clamp01((head * 1.15f - u) / 0.15f);
                float vanish = Mathf.Clamp01((u - (burnK * 1.3f - 0.3f)) / 0.3f);
                float vis = reveal * vanish * dim;
                float th = look.thickness * taper * thin;
                float lick = look.lickLength * Mathf.Sqrt(taper) * Lick(u, now);

                Vert(e, rx, ry, f, theta, 1f - th * 1.9f, body, 0f);
                Vert(e, rx, ry, f, theta, 1f - th, body, look.smear * vis);
                Vert(e, rx, ry, f, theta, 1f - th * 0.55f, body, 0.92f * vis);
                Vert(e, rx, ry, f, theta, 1f - th * 0.15f, hot, vis);
                Vert(e, rx, ry, f, theta, 1f, rim, vis);
                Vert(e, rx, ry, f, theta, 1.04f, edge, 0.9f * vis);
                Vert(e, rx, ry, f, theta + lick * 0.9f, 1.04f + lick, ember, 0.12f * vis); // leans back, away from the swing
                Vert(e, rx, ry, f, theta, 1f - th * 0.35f, core, 0f);
                Vert(e, rx, ry, f, theta, 1f - th * 0.07f, core, 0.9f * vis);
                Vert(e, rx, ry, f, theta, 1.02f, core, 0f);
                if (i == 0) continue;
                int a0 = (i - 1) * RingsPerStep, b0 = i * RingsPerStep;
                for (int k = 0; k < AlphaRings - 1; k++) Quad(alphaTris, a0 + k, b0 + k);
                for (int k = AlphaRings; k < RingsPerStep - 1; k++) Quad(addTris, a0 + k, b0 + k);
            }

            slashMesh.Clear();
            slashMesh.SetVertices(verts);
            slashMesh.SetColors(colors);
            slashMesh.subMeshCount = 2;
            slashMesh.SetTriangles(alphaTris, 0, false);
            slashMesh.SetTriangles(addTris, 1, false);
            slashMesh.RecalculateBounds();
        }

        /// <summary>0..1: tall where a flame lick stands. A lick every 1/lickCount of the arc, drifting and flickering.</summary>
        float Lick(float u, float now)
        {
            float x = u * look.lickCount + seed;
            float peak = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(Tau * (x + now * 1.7f)), 3f);
            return peak * (0.45f + 0.55f * Mathf.PerlinNoise(x * 1.7f, now * 9f));
        }

        void Vert(Vector2 e, float rx, float ry, float f, float theta, float s, Color c, float alpha)
        {
            verts.Add(new Vector3(e.x + f * Mathf.Cos(theta) * rx * s, e.y + Mathf.Sin(theta) * ry * s, z));
            c.a = Mathf.Clamp01(alpha);
            // Mesh vertex colors reach the shader as they are; the picture is linear in a Linear project.
            colors.Add(linearSpace ? c.linear : c);
        }

        static void Quad(List<int> tris, int a, int b)
        {
            tris.Add(a); tris.Add(b); tris.Add(a + 1);
            tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
        }

        /// <summary>Embers thrown off the blade's path as the crescent sweeps: along the swing and outward.</summary>
        void Spray(Vector2 e, float rx, float ry, float head)
        {
            int due = Mathf.RoundToInt(look.strikeEmbers * head);
            float top = look.arcAngle * Mathf.Deg2Rad, f = strikeFacing;
            for (; sprayed < due; sprayed++)
            {
                float u = Mathf.Clamp01(head - Random.Range(0f, 0.12f));
                float theta = Mathf.Lerp(top, -top, u), cos = Mathf.Cos(theta), sin = Mathf.Sin(theta);
                var p = e + new Vector2(f * cos * rx, sin * ry);
                var along = new Vector2(f * sin * rx, -cos * ry).normalized;
                var outward = new Vector2(f * cos * ry, sin * rx).normalized;
                float speed = Random.Range(look.strikeEmberSpeed.x, look.strikeEmberSpeed.y);
                Emit(outEmbers, p, (along * 0.7f + outward * 0.5f + Random.insideUnitCircle * 0.3f) * speed,
                     look.emberSize * Random.Range(0.8f, 1.6f), Random.Range(0.2f, 0.45f), look.Heat(Random.Range(0.65f, 1f)));
            }
        }

        void UpdateOneShots(float now)
        {
            if (ripple)
            {
                float k = (now - rippleAt) / Mathf.Max(0.01f, look.rippleTime);
                bool on = k >= 0f && k < 1f && look.rippleStrength > 0f;
                Place(ripple, ripplePoint, Vector2.one * look.rippleRadius * 2f, 0f, Color.white, on ? 1f : 0f);
                if (on)
                {
                    rippleMat.SetFloat(RingId, 1f);
                    rippleMat.SetFloat(ShimmerId, 0f);
                    rippleMat.SetFloat(RingRadiusId, Mathf.Lerp(0.1f, 0.85f, 1f - (1f - k) * (1f - k)));
                    rippleMat.SetFloat(RingWidthId, 0.12f);
                    rippleMat.SetFloat(StrengthId, look.rippleStrength * (1f - k));
                }
            }
            float hk = (now - hitAt) / Mathf.Max(0.01f, look.hitFlashTime);
            bool flash = hk >= 0f && hk < 1f;
            Place(hitFlash, hitPoint, look.hitFlashSize * (1f - 0.5f * hk), 20f * hk, look.Heat(1f), flash ? 1f - hk * hk : 0f);
        }

        // ---------- Helpers ----------

        void Place(SpriteRenderer sr, Vector2 at, float size, float degrees, Color color, float alpha) =>
            Place(sr, at, new Vector2(size, size), degrees, color, alpha);

        void Place(SpriteRenderer sr, Vector2 at, Vector2 size, float degrees, Color color, float alpha)
        {
            if (!sr) return;
            bool on = alpha > 0.003f && size.x > 0.0001f && size.y > 0.0001f;
            if (sr.enabled != on) sr.enabled = on;
            if (!on) return;
            sr.transform.SetPositionAndRotation(new Vector3(at.x, at.y, z), Quaternion.Euler(0f, 0f, degrees));
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            color.a = Mathf.Clamp01(alpha);
            sr.color = color;
        }

        void Emit(ParticleSystem ps, Vector2 at, Vector2 velocity, float size, float life, Color color)
        {
            if (!ps) return;
            var p = new ParticleSystem.EmitParams
            {
                position = new Vector3(at.x, at.y, z),
                velocity = velocity,
                startSize = size,
                startLifetime = life,
                startColor = color,
                applyShapeToPosition = false,
            };
            ps.Emit(p, 1);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void OnDestroy()
        {
            if (shimmerMat) Destroy(shimmerMat);
            if (rippleMat) Destroy(rippleMat);
            if (slashMesh) Destroy(slashMesh);
        }

        // ---------- Shared assets (made once, in code) ----------

        static void EnsureAssets()
        {
            if (!fxShader)
            {
                fxShader = Resources.Load<Shader>("BlazeFx");
                if (!fxShader || !fxShader.isSupported)
                {
                    if (!warned) Debug.LogWarning("BlazeStrikeVisuals: shader Resources/BlazeFx is missing or unsupported; using Sprites/Default (no additive glow).");
                    warned = true;
                    fxShader = Shader.Find("Sprites/Default");
                }
            }
            if (!heatShader)
            {
                var heat = Resources.Load<Shader>("BlazeHeat");
                heatShader = heat && heat.isSupported ? heat : null; // no heat haze without it
            }
            if (!dot) dot = MakeTexture("Dot", 64, 64, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                return Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
            });
            if (!flame) flame = MakeTexture("Flame", 64, 128, new Vector2(0.5f, 0.3f), (x, y) =>
            {
                // A teardrop: a round belly low down (the pivot sits in it) and a tongue rising to a point.
                float v = (y + 1f) * 0.5f;
                float half = 0.95f * Mathf.Sqrt(v) * Mathf.Pow(1f - v, 1.1f) / 0.371f;
                return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x) / Mathf.Max(half, 0.001f)), 0.9f) * Mathf.Clamp01(v * 6f);
            });
            if (!star) star = MakeTexture("Star", 64, 64, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                // A four-point glint: two thin streaks and a soft center.
                float streaks = Mathf.Max(Mathf.Exp(-Mathf.Abs(y) * 26f) * Mathf.Pow(1f - Mathf.Abs(x), 2f),
                                          Mathf.Exp(-Mathf.Abs(x) * 26f) * Mathf.Pow(1f - Mathf.Abs(y), 2f));
                return streaks + 0.9f * Mathf.Exp(-(x * x + y * y) * 18f);
            });
            if (!ring) ring = MakeTexture("Ring", 128, 128, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                // A soft ring at 0.8 of the radius, a little ragged like flame.
                float d = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Atan2(y, x);
                return Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.1f, 2f)) * (0.65f + 0.35f * Mathf.Pow(Mathf.Sin(a * 7f), 2f));
            });
            if (!alphaMat) alphaMat = NewMaterial("BlazeAlpha", false, null);
            if (!addMat) addMat = NewMaterial("BlazeAdditive", true, null);
            if (!dotAddMat) dotAddMat = NewMaterial("BlazeEmbers", true, dot.texture);
            if (!flameAlphaMat) flameAlphaMat = NewMaterial("BlazeFlames", false, flame.texture);
        }

        static Material NewMaterial(string name, bool additive, Texture texture)
        {
            var m = new Material(fxShader) { name = name, hideFlags = HideFlags.DontSave };
            m.SetFloat(DstBlendId, (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (texture) m.mainTexture = texture;
            return m;
        }

        /// <summary>A white sprite whose alpha is `alpha(x, y)`, x and y from -1 to 1 across it. Its long side is 1 world unit.</summary>
        static Sprite MakeTexture(string name, int w, int h, Vector2 pivot, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { name = "Blaze_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = alpha((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Mathf.Max(w, h), 0, SpriteMeshType.FullRect);
            sprite.name = "Blaze_" + name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
