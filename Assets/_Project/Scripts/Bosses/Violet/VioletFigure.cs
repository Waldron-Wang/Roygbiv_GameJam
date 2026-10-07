using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// The Violet king, procedural: a Pose child with its pivot at the feet, built from VioletShapes parts and
    /// animated in LateUpdate. The silhouette is the point: a pointed CROWN, a long segmented CAPE that sways and
    /// trails, a broad armored body, a one-handed GREATSWORD (long blade, wide crossguard) in the right hand and a
    /// free left hand that glows when it casts. A second greatsword can appear in the left hand (Twin Blades).
    ///
    /// The boss doesn't animate bones: it sets a few TARGETS (sword arm angle, off arm, crouch, lean, glows, spin...)
    /// and this eases toward them, adding breathing, cape sway and hit flashes on top. Angles: 0 = arm hanging
    /// straight down, + = swung forward and up, 180 = straight up, 200 = raised back over the head.
    /// Faces right at facing = +1; the whole Pose mirrors for -1. Runs on scaled time (Serenity slows it).
    /// He's drawn in front of the tilemap, but nothing of his shows below his GROUND (SetGround: a sprite mask whose
    /// bottom edge is the surface he stands on), so a blade resting in or stuck into the ground ends at the surface.
    /// Art can replace it: VioletBoss.CurrentState says what the king is doing.
    /// </summary>
    public class VioletFigure : MonoBehaviour
    {
        // ---------- Targets the boss sets ----------
        [HideInInspector] public float swordArm = 25f, swordTwist = -30f, offArm = 12f, offTwist = -20f;
        [HideInInspector] public float crouch, lean, kneel, headTilt;
        [HideInInspector] public float bladeGlow, handGlow, capeWind, aura;
        [HideInInspector] public float spinSpeed; // turns per second while whirling (0 = facing holds)
        [HideInInspector] public int facing = 1;
        [Tooltip("How fast the arms reach their targets (higher = snappier). Swings turn it up for a moment.")]
        [HideInInspector] public float armResponse = 10f;
        /// <summary>0..1: how solid the second greatsword in the left hand is.</summary>
        [HideInInspector] public float secondSword;

        const float HipHeight = 1.35f;
        /// <summary>The greatsword's blade (its glowing edge is one above) and hilt, above sortingBase: the blade under his
        /// sword arm, the hilt over his hand. The planted greatsword of the arrival uses them too.</summary>
        public const int BladeOrder = 12, HiltOrder = 15;
        // The ground clip: wide enough for a cape fallen at the other end of the arena, tall enough for any leap.
        const float ClipWidth = 120f, ClipHeight = 60f;

        Transform pose, hips, torso, head, armFront, armBack, sword, offSword, crown, capeRoot, legFront, legBack;
        SpriteRenderer blade, bladeEdge, offBlade, offBladeEdge, offHilt, handGlowSr, handRing, visor, auraRing, glint;
        readonly List<Transform> capeSegments = new();
        readonly List<SpriteRenderer> parts = new();
        readonly List<Color> partColors = new();

        float curSwordArm = 25f, curSwordTwist = -30f, curOffArm = 12f, curOffTwist = -20f;
        float curCrouch, curLean, curKneel, curHead, curBladeGlow, curHandGlow, curAura, curCapeWind;
        float animTime, flash, jolt, spinPhase, glintAt = 2f;
        Vector2 lastPos, smoothVel;
        bool hasCape = true, swordsShattered, crownFallen;
        int baseOrder;
        SpriteMask groundClip;
        float groundY;

        public Transform Pose => pose;
        public bool HasCape => hasCape;

        /// <summary>Builds the rig under `parent` with the feet at local `feet`. sortingBase = the lowest order it uses.</summary>
        public void Build(Transform parent, Vector2 feet, int sortingBase)
        {
            baseOrder = sortingBase;
            pose = new GameObject("Pose").transform;
            pose.SetParent(parent, false);
            pose.localPosition = feet;

            hips = Node("Hips", pose, new Vector2(0f, HipHeight));
            legBack = Part("LegBack", VioletShapes.Leg, hips, new Vector2(-0.16f, 0f), VioletShapes.Dark, 3).transform;
            torso = Node("Torso", hips, Vector2.zero);

            capeRoot = Node("Cape", torso, new Vector2(-0.3f, 1.02f));
            var parentSeg = capeRoot;
            float[] widths = { 0.95f, 1.05f, 1.15f, 1.25f, 1.35f, 1.45f };
            for (int i = 0; i < widths.Length - 1; i++)
            {
                var seg = Part("CapeSeg", VioletShapes.CapeSegment(widths[i], widths[i + 1], 0.5f, i == widths.Length - 2), parentSeg,
                    i == 0 ? Vector2.zero : new Vector2(0f, -0.48f), Color.Lerp(VioletShapes.Deep, VioletShapes.Dark, i * 0.08f), 0).transform;
                capeSegments.Add(seg);
                parentSeg = seg;
            }

            Part("PauldronBack", VioletShapes.Pauldron, torso, new Vector2(-0.44f, 1.0f), VioletShapes.Mid, 4);
            armBack = Part("ArmBack", VioletShapes.Arm, torso, new Vector2(-0.4f, 0.93f), VioletShapes.Deep, 2).transform;
            var offHand = Node("OffHand", armBack, new Vector2(0f, -1f));
            offSword = Node("OffSword", offHand, Vector2.zero);
            offBlade = Part("OffBlade", VioletShapes.Blade, offSword, Vector2.zero, VioletShapes.Steel, 1);
            offBladeEdge = Part("OffBladeEdge", VioletShapes.BladeEdge, offSword, Vector2.zero, VioletShapes.Glow, 2);
            offHilt = Part("OffHilt", VioletShapes.Hilt, offSword, Vector2.zero, VioletShapes.Bright, 3);
            handGlowSr = IndigoShapes.Create("HandGlow", IndigoShapes.Disc, offHand, Vector2.zero, 0.5f, VioletShapes.Glow, baseOrder + 18);
            handRing = IndigoShapes.Create("HandRing", IndigoShapes.ThinRing, offHand, Vector2.zero, 0.9f, VioletShapes.Glow, baseOrder + 18);

            Part("Torso", VioletShapes.Torso, torso, Vector2.zero, VioletShapes.Mid, 6);
            Part("Belt", VioletShapes.Gem, torso, new Vector2(0.02f, 0.02f), VioletShapes.Glow, 7).transform.localScale = new Vector3(0.28f, 0.22f, 1f);
            legFront = Part("LegFront", VioletShapes.Leg, hips, new Vector2(0.18f, 0f), VioletShapes.Mid, 7).transform;

            head = Node("Head", torso, new Vector2(0.04f, 1.12f));
            Part("Helm", VioletShapes.Helm, head, Vector2.zero, VioletShapes.Dark, 8);
            visor = Part("Visor", VioletShapes.Visor, head, new Vector2(0.02f, 0.3f), VioletShapes.Glow, 9);
            crown = Node("Crown", head, new Vector2(0f, 0.5f));
            Part("CrownBand", VioletShapes.Crown, crown, Vector2.zero, VioletShapes.Bright, 9);
            foreach (var gx in new[] { -0.16f, 0f, 0.16f })
                Part("CrownGem", VioletShapes.Gem, crown, new Vector2(gx, 0.08f), VioletShapes.Glow, 10).transform.localScale = new Vector3(0.12f, 0.14f, 1f);
            glint = VioletShapes.Create("Glint", VioletShapes.Glint, crown, new Vector2(0.06f, 0.62f), Color.clear, baseOrder + 19);

            Part("PauldronFront", VioletShapes.Pauldron, torso, new Vector2(0.46f, 0.98f), VioletShapes.Bright, 11);
            armFront = Part("ArmFront", VioletShapes.Arm, torso, new Vector2(0.5f, 0.93f), VioletShapes.Mid, 14).transform;
            var hand = Node("Hand", armFront, new Vector2(0f, -1f));
            sword = Node("Sword", hand, Vector2.zero);
            blade = Part("Blade", VioletShapes.Blade, sword, Vector2.zero, VioletShapes.Steel, BladeOrder);
            bladeEdge = Part("BladeEdge", VioletShapes.BladeEdge, sword, Vector2.zero, VioletShapes.Glow, BladeOrder + 1); // over the blade, under the arm
            Part("Hilt", VioletShapes.Hilt, sword, Vector2.zero, VioletShapes.Bright, HiltOrder);

            auraRing = IndigoShapes.Create("Aura", IndigoShapes.ThinRing, pose, new Vector2(0f, 1.7f), 4.4f, Color.clear, baseOrder + 20);
            lastPos = transform.position;

            // His ground starts under his feet; the boss moves it wherever he stands (SetGround).
            groundClip = new GameObject("Violet King Ground Clip").AddComponent<SpriteMask>();
            SceneManager.MoveGameObjectToScene(groundClip.gameObject, gameObject.scene); // lives and dies with his scene, wherever it's built
            groundClip.sprite = FlatSprite.Square;
            groundClip.transform.localScale = new Vector3(ClipWidth, ClipHeight, 1f);
            foreach (var sr in pose.GetComponentsInChildren<SpriteRenderer>(true)) ClipToGround(sr);
            SetGround(pose.position.y);
        }

        void OnDestroy()
        {
            if (groundClip) Destroy(groundClip.gameObject);
        }

        Transform Node(string name, Transform parent, Vector2 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        SpriteRenderer Part(string name, Sprite sprite, Transform parent, Vector2 local, Color color, int order)
        {
            var sr = VioletShapes.Create(name, sprite, parent, local, color, baseOrder + order);
            parts.Add(sr);
            partColors.Add(color);
            return sr;
        }

        // ---------- One-shots ----------

        /// <summary>A hit: white flash and a jolt.</summary>
        public void Flash()
        {
            flash = 1f;
            jolt = 1f;
        }

        /// <summary>The lowest sorting order of his parts (the sortingBase it was built with).</summary>
        public int SortingBase => baseOrder;

        /// <summary>
        /// The surface he stands on (the top of his hill in the run, the arena floor in the duel): nothing of his shows
        /// below it, so his blade resting in the ground or stuck into the floor ends at the surface.
        /// </summary>
        public void SetGround(float y)
        {
            groundY = y;
            PlaceGroundClip();
        }

        /// <summary>Hides the part of `sr` below his ground too (the planted greatsword).</summary>
        public void ClipToGround(SpriteRenderer sr)
        {
            if (sr) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        // The mask's bottom edge is the ground line; it follows him sideways, never up or down (leaps stay visible).
        void PlaceGroundClip()
        {
            if (groundClip) groundClip.transform.position = new Vector3(transform.position.x, groundY + ClipHeight * 0.5f, 0f);
        }

        /// <summary>Shows or hides the greatsword in his right hand (it's planted in the ground for the arrival).</summary>
        public void ShowHandSword(bool show)
        {
            if (sword) sword.gameObject.SetActive(show);
        }

        /// <summary>A small jolt with no flash (a cast going off, a ring fired).</summary>
        public void Jolt(float amount = 0.6f) => jolt = Mathf.Max(jolt, amount);

        /// <summary>The crown catches the light.</summary>
        public void Glint() => glintAt = animTime;

        /// <summary>Snap the arms to their targets now (cuts, restoring a pose).</summary>
        public void SnapArms()
        {
            curSwordArm = swordArm;
            curSwordTwist = swordTwist;
            curOffArm = offArm;
            curOffTwist = offTwist;
        }

        /// <summary>Tears the cape off: it falls away as a loose piece onto `floorY` and fades.</summary>
        public void DetachCape(float floorY)
        {
            if (!hasCape || !capeRoot) return;
            hasCape = false;
            var loose = new GameObject("FallenCape").transform;
            loose.position = capeRoot.position;
            capeRoot.SetParent(loose, true);
            loose.gameObject.AddComponent<VioletDebris>()
                .Launch(new Vector2(-facing * Random.Range(2f, 3.5f), 4f), 4f, -facing * 80f)
                .RestOn(floorY + 0.4f, 2.5f, 0.5f);
            foreach (var seg in capeSegments)
            {
                var sr = seg.GetComponent<SpriteRenderer>();
                int i = parts.IndexOf(sr);
                if (i >= 0) { parts.RemoveAt(i); partColors.RemoveAt(i); }
            }
            capeSegments.Clear();
        }

        /// <summary>No cape (resumed past the point where it was torn off).</summary>
        public void RemoveCape()
        {
            if (!hasCape) return;
            hasCape = false;
            foreach (var seg in capeSegments)
            {
                var sr = seg.GetComponent<SpriteRenderer>();
                int i = parts.IndexOf(sr);
                if (i >= 0) { parts.RemoveAt(i); partColors.RemoveAt(i); }
            }
            capeSegments.Clear();
            if (capeRoot) Destroy(capeRoot.gameObject);
        }

        /// <summary>Both greatswords burst into shards (defeat).</summary>
        public void ShatterSwords()
        {
            if (swordsShattered) return;
            swordsShattered = true;
            foreach (var b in new[] { blade, offBlade })
            {
                if (!b || !b.enabled || (b == offBlade && secondSword < 0.5f)) continue;
                for (int i = 0; i < 10; i++)
                {
                    var at = b.transform.TransformPoint(new Vector3(0f, -Random.Range(0.2f, 2.7f), 0f));
                    var shard = VioletShapes.Create("SwordShard", VioletShapes.Shard, null, at, VioletShapes.Steel, baseOrder + 20);
                    shard.transform.localScale = Vector3.one * Random.Range(0.35f, 0.7f);
                    shard.gameObject.AddComponent<VioletDebris>().Launch(Random.insideUnitCircle * 6f + Vector2.up * 3f, Random.Range(0.8f, 1.4f));
                }
                VioletHits.Burst(b.transform.TransformPoint(new Vector3(0f, -1.4f, 0f)), VioletShapes.Glow, 10, 6f, 0.4f);
            }
            blade.enabled = bladeEdge.enabled = false;
            offBlade.enabled = offBladeEdge.enabled = false;
        }

        /// <summary>The crown topples off and rolls to rest on the floor.</summary>
        public void DropCrown(float floorY)
        {
            if (crownFallen || !crown) return;
            crownFallen = true;
            var loose = new GameObject("FallenCrown").transform;
            loose.position = crown.position;
            crown.SetParent(loose, true);
            foreach (var sr in crown.GetComponentsInChildren<SpriteRenderer>())
            {
                int i = parts.IndexOf(sr);
                if (i >= 0) { parts.RemoveAt(i); partColors.RemoveAt(i); }
            }
            loose.gameObject.AddComponent<VioletDebris>()
                .Launch(new Vector2(facing * 1.5f, 3.5f), 30f, facing * -260f)
                .RestOn(floorY + 0.05f, 28f);
        }

        // ---------- Points for attacks ----------

        public Vector2 SwordTip => sword ? (Vector2)sword.TransformPoint(new Vector3(0f, -2.8f, 0f)) : (Vector2)transform.position;
        public Vector2 SwordHand => sword ? (Vector2)sword.position : (Vector2)transform.position;
        public Vector2 OffHand => armBack ? (Vector2)armBack.TransformPoint(new Vector3(0f, -1f, 0f)) : (Vector2)transform.position;
        public Vector2 OffSwordTip => offSword ? (Vector2)offSword.TransformPoint(new Vector3(0f, -2.8f, 0f)) : OffHand;
        public Vector2 Chest => torso ? (Vector2)torso.TransformPoint(new Vector3(0f, 0.7f, 0f)) : (Vector2)transform.position;
        public Vector2 CrownPoint => crown ? (Vector2)crown.position : (Vector2)transform.position;

        // ---------- Animation ----------

        void LateUpdate()
        {
            if (!pose) return;
            PlaceGroundClip();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            animTime += dt;

            float k = 1f - Mathf.Exp(-armResponse * dt);
            float soft = 1f - Mathf.Exp(-8f * dt);
            curSwordArm = Mathf.Lerp(curSwordArm, swordArm, k);
            curSwordTwist = Mathf.Lerp(curSwordTwist, swordTwist, k);
            curOffArm = Mathf.Lerp(curOffArm, offArm, k);
            curOffTwist = Mathf.Lerp(curOffTwist, offTwist, k);
            curCrouch = Mathf.Lerp(curCrouch, crouch, soft);
            curLean = Mathf.Lerp(curLean, lean, soft);
            curKneel = Mathf.Lerp(curKneel, kneel, 1f - Mathf.Exp(-3f * dt));
            curHead = Mathf.Lerp(curHead, headTilt, soft);
            curBladeGlow = Mathf.Lerp(curBladeGlow, bladeGlow, 1f - Mathf.Exp(-14f * dt));
            curHandGlow = Mathf.Lerp(curHandGlow, handGlow, 1f - Mathf.Exp(-14f * dt));
            curAura = Mathf.Lerp(curAura, aura, 1f - Mathf.Exp(-10f * dt));
            curCapeWind = Mathf.Lerp(curCapeWind, capeWind, soft);
            flash = Mathf.MoveTowards(flash, 0f, dt / 0.12f);
            jolt = Mathf.MoveTowards(jolt, 0f, dt * 4f);

            Vector2 pos = transform.position;
            smoothVel = Vector2.Lerp(smoothVel, (pos - lastPos) / dt, 1f - Mathf.Exp(-10f * dt));
            lastPos = pos;

            // Whirling: the body turns round and round (shown as the facing flipping smoothly).
            float facingScale = facing;
            if (spinSpeed > 0f)
            {
                spinPhase += spinSpeed * dt * Mathf.PI * 2f;
                facingScale = Mathf.Cos(spinPhase) * facing;
                if (Mathf.Abs(facingScale) < 0.15f) facingScale = 0.15f * Mathf.Sign(facingScale == 0f ? 1f : facingScale);
            }
            else spinPhase = 0f;

            float breath = Mathf.Sin(animTime * 1.9f);
            pose.localScale = new Vector3(facingScale, 1f + breath * 0.012f - curCrouch * 0.05f, 1f);

            float hipY = HipHeight - curCrouch * 0.38f - curKneel * 0.6f;
            hips.localPosition = new Vector3(Random.Range(-1f, 1f) * jolt * 0.06f, hipY + breath * 0.02f, 0f);
            legFront.localRotation = Quaternion.Euler(0f, 0f, 6f + curCrouch * 28f + curKneel * 70f);
            legBack.localRotation = Quaternion.Euler(0f, 0f, -8f - curCrouch * 22f - curKneel * 55f);
            torso.localRotation = Quaternion.Euler(0f, 0f, -(curLean + curKneel * 22f) + breath * 0.8f - jolt * 6f);
            head.localRotation = Quaternion.Euler(0f, 0f, -curHead - curKneel * 18f);

            armFront.localRotation = Quaternion.Euler(0f, 0f, curSwordArm + breath * 1.5f);
            sword.localRotation = Quaternion.Euler(0f, 0f, curSwordTwist);
            armBack.localRotation = Quaternion.Euler(0f, 0f, curOffArm - breath * 1.5f);
            offSword.localRotation = Quaternion.Euler(0f, 0f, curOffTwist);

            AnimateCape(dt);
            AnimateGlows();
            ApplyFlash();
        }

        void AnimateCape(float dt)
        {
            if (!hasCape || capeSegments.Count == 0) return;
            // Trails behind movement and billows with "wind" (the intro stance, casting, the roar).
            float trail = Mathf.Clamp(smoothVel.x * facing * 2.2f, -25f, 25f);
            float lift = Mathf.Clamp(-smoothVel.y * 1.5f, -20f, 20f);
            float wind = curCapeWind;
            for (int i = 0; i < capeSegments.Count; i++)
            {
                float sway = Mathf.Sin(animTime * (1.6f + wind * 3f) - i * 0.7f) * (2.5f + i * 1.2f) * (1f + wind * 2.5f);
                float angle = (i == 0 ? -10f - curLean * 0.6f : -5f) - trail * (0.4f + i * 0.15f) - lift * 0.3f - wind * (8f + i * 4f) + sway;
                capeSegments[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void AnimateGlows()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(animTime * 18f);
            if (bladeEdge && bladeEdge.enabled)
                bladeEdge.color = new Color(VioletShapes.Glow.r, VioletShapes.Glow.g, VioletShapes.Glow.b, 0.15f + 0.85f * curBladeGlow * (0.75f + 0.25f * pulse));

            bool second = secondSword > 0.01f && !swordsShattered;
            if (offBlade) { offBlade.enabled = second; offBlade.color = new Color(VioletShapes.Steel.r, VioletShapes.Steel.g, VioletShapes.Steel.b, secondSword); }
            if (offBladeEdge) { offBladeEdge.enabled = second; offBladeEdge.color = new Color(1f, 1f, 1f, secondSword * (0.2f + 0.8f * curBladeGlow * pulse)); }
            if (offHilt) { offHilt.enabled = secondSword > 0.01f; offHilt.color = new Color(VioletShapes.Bright.r, VioletShapes.Bright.g, VioletShapes.Bright.b, secondSword); }

            float hand = Mathf.Clamp01(curHandGlow);
            handGlowSr.transform.localScale = Vector3.one * (0.35f + 0.5f * hand + 0.08f * pulse * hand);
            handGlowSr.color = new Color(VioletShapes.Glow.r, VioletShapes.Glow.g, VioletShapes.Glow.b, 0.15f + 0.8f * hand);
            handRing.transform.localScale = Vector3.one * (0.5f + 1.4f * hand);
            handRing.transform.localRotation = Quaternion.Euler(0f, 0f, animTime * 200f);
            handRing.color = new Color(1f, 1f, 1f, 0.7f * hand);

            visor.color = Color.Lerp(VioletShapes.Glow, Color.white, 0.4f * Mathf.Max(curBladeGlow, curHandGlow) + 0.3f * pulse * curAura);

            float a = curAura * (0.45f + 0.35f * pulse);
            auraRing.color = new Color(VioletShapes.Glow.r, VioletShapes.Glow.g, VioletShapes.Glow.b, a);
            auraRing.transform.localScale = Vector3.one * (4.4f + 0.3f * Mathf.Sin(animTime * 5f));
            auraRing.transform.localRotation = Quaternion.Euler(0f, 0f, animTime * 40f);

            if (glint)
            {
                // A glint every few seconds, or when asked.
                if (animTime - glintAt > 4.5f && Random.value < 0.004f) glintAt = animTime;
                float g = Mathf.Clamp01(1f - (animTime - glintAt) / 0.45f);
                glint.color = new Color(1f, 1f, 1f, g);
                glint.transform.localScale = Vector3.one * (0.2f + 0.7f * g);
                glint.transform.localRotation = Quaternion.Euler(0f, 0f, animTime * 90f);
            }
        }

        void ApplyFlash()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var sr = parts[i];
                if (!sr || sr == bladeEdge || sr == offBladeEdge || sr == offBlade || sr == offHilt || sr == visor) continue;
                var c = Color.Lerp(partColors[i], Color.white, flash * 0.85f);
                c.a = partColors[i].a;
                sr.color = c;
            }
        }
    }
}
