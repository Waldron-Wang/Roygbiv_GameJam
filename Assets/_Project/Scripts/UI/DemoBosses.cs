using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The later bosses drawn in IMGUI for the how-to cards (and the hub's preview), built from the same parts,
    /// colors and motion as their real procedural looks, so the card shows the boss you're about to meet:
    ///   Green   GreenBoss: the 2x2 bramble block rooted at its feet, its diamond heart (dark, split open pink when it
    ///           wilts), the wilt's brown droop, falling leaves; GreenPod: stalk, husk diamond, core in the ability's color.
    ///   Blue    BlueBoss: the 1.4x1.8 weeping figure, sobbing, leaning into its throws, eyes cast down on the side it
    ///           faces; BlueTear drops; BlueWater's layered waves, light band and foam crest.
    ///   Indigo  IndigoBoss: the diamond body, thin halo in the curse's color, the eye (white, pupil, lid); IndigoSigil's
    ///           counter-turning rings, runes and motes.
    ///   Violet  VioletFigure: the king rebuilt from VioletShapes' sprites (cape segments, armor, helm, crown, greatsword)
    ///           with the same rig, angles and pose targets (KingPose).
    /// Coordinates are GUI pixels (y down); `unit` is GUI pixels per world unit. Every color goes through Tint, so a
    /// silhouette (Silhouette) or a fade (CardGui.Alpha) applies to all of it.
    /// If a boss's look changes, change it here too.
    /// </summary>
    static class DemoBosses
    {
        /// <summary>When set, every part is drawn in this color (keeping its alpha): a locked boss's silhouette.</summary>
        public static Color? Silhouette;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Silhouette = null;

        static readonly Color Outline = new(0.08f, 0.08f, 0.1f, 0.75f);

        static Color Tint(Color c)
        {
            if (Silhouette is { } s) return new Color(s.r, s.g, s.b, s.a * c.a);
            return c;
        }

        static Color Flashed(Color c, float flash)
        {
            var lit = Color.Lerp(c, Color.white, Mathf.Clamp01(flash) * 0.85f);
            lit.a = c.a;
            return Tint(lit);
        }

        // ---------- Green: the bramble ----------

        public static readonly Color GreenBody = new(0.2f, 0.75f, 0.3f);
        public static readonly Color HeartOpen = new(1f, 0.45f, 0.65f);
        public static readonly Color WiltColor = new(0.45f, 0.32f, 0.12f);
        public static readonly Color VineColor = new(0.12f, 0.4f, 0.12f);
        public static readonly Color LeafColor = new(0.35f, 0.8f, 0.3f, 0.8f);
        static readonly Color HeartClosed = new(0.1f, 0.25f, 0.08f);
        static readonly Color HuskColor = new(0.2f, 0.5f, 0.15f);
        static readonly Color RipeColor = new(0.75f, 0.7f, 0.15f);
        static readonly Color StalkColor = new(0.15f, 0.35f, 0.1f);

        /// <summary>
        /// The Green boss rooted with its feet at `feet`. lean: degrees, + tips the top toward +x (GreenBoss.LateUpdate's
        /// lean). squash: its pose scale. heartOpen 0..1 (wilted = open). wilt 0..1 browns it. glow: the colored overlay
        /// it gets while it covets or winds up a stolen ability.
        /// </summary>
        public static void Bramble(Vector2 feet, float unit, float time, float lean, Vector2 squash, float heartOpen = 0f,
                                   float wilt = 0f, Color glow = default, float glowAmount = 0f, float flash = 0f)
        {
            var prev = CardGui.Rotate(feet, lean);
            float w = 2f * unit * squash.x, h = 2f * unit * squash.y;
            var body = new Rect(feet.x - w * 0.5f, feet.y - h, w, h);
            CardGui.Box(Expand(body, 2.5f), Tint(Outline));
            CardGui.Box(body, Tint(GreenBody));
            if (glowAmount > 0f) CardGui.Box(body, Tint(new Color(glow.r, glow.g, glow.b, Mathf.Clamp01(glowAmount))));
            if (wilt > 0f) CardGui.Box(body, Tint(new Color(WiltColor.r, WiltColor.g, WiltColor.b, 0.55f * wilt)));
            if (flash > 0f) CardGui.Box(body, Tint(new Color(1f, 1f, 1f, 0.8f * flash)));

            // The heart sits a little over halfway up (GreenBoss.HeartPoint), and swells as it splits open.
            var heart = new Vector2(feet.x, feet.y - 1.1f * unit * squash.y);
            float size = (0.4f + 0.25f * heartOpen) * unit * squash.x;
            if (heartOpen > 0f) CardGui.Glow(heart, size * 1.6f, Tint(new Color(HeartOpen.r, HeartOpen.g, HeartOpen.b, 0.55f * heartOpen)));
            CardGui.Diamond(heart, size, Tint(Color.Lerp(HeartClosed, HeartOpen, heartOpen)));
            GUI.matrix = prev;

            // A leaf now and then, drifting down off its crown (not while it's wilted).
            for (int i = 0; i < 3 && wilt < 0.5f; i++)
            {
                float k = Mathf.Repeat(time * 0.45f + i * 0.37f, 1f);
                if (k > 0.6f) continue;
                float u = k / 0.6f;
                var at = new Vector2(feet.x + (Hash(i * 3.1f) - 0.5f) * 1.6f * unit + Mathf.Sin(time * 2f + i) * 0.15f * unit,
                                     feet.y - 1.9f * unit + u * 1.4f * unit);
                CardGui.Diamond(at, 0.15f * unit, Tint(new Color(LeafColor.r, LeafColor.g, LeafColor.b, LeafColor.a * (1f - u))), time * 120f + i * 40f);
            }
        }

        /// <summary>
        /// A stolen ability growing on a stalk (GreenPod): `spot` is the foot of the stalk. grown 0..1 sprouts it;
        /// ripeness 0..1 swells it toward bursting; core is the stolen ability's color.
        /// </summary>
        public static void Pod(Vector2 spot, float unit, float time, float grown, float ripeness, Color core, float jolt = 0f)
        {
            if (grown <= 0f) return;
            float stalk = 0.5f * unit * grown;
            CardGui.Box(new Rect(spot.x - 0.07f * unit, spot.y - stalk, 0.14f * unit, stalk), Tint(StalkColor));

            float rate = Mathf.Lerp(2f, 12f, ripeness * ripeness);
            float throb = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(time * rate * Mathf.PI), 3f) * Mathf.Lerp(0.04f, 0.14f, ripeness);
            float size = Mathf.Lerp(0.4f, 0.95f, Mathf.Sqrt(ripeness)) * grown * (1f + throb + 0.25f * jolt) * unit;
            var shake = new Vector2(Mathf.Sin(time * 71f), Mathf.Cos(time * 63f)) * 0.08f * jolt * unit;
            var center = new Vector2(spot.x, spot.y - stalk - 0.475f * unit * grown) + shake;
            float turn = Mathf.Sin(time * 2f) * 6f;
            CardGui.Diamond(center, size + 4f, Tint(Outline), turn);
            CardGui.Diamond(center, size, Tint(Color.Lerp(HuskColor, RipeColor, Mathf.Max(0f, (ripeness - 0.5f) * 2f))), turn);
            CardGui.Glow(center, size * 0.9f, Tint(new Color(core.r, core.g, core.b, 0.45f)));
            CardGui.Diamond(center, size * 0.5f, Tint(core), turn);
        }

        /// <summary>Where Pod draws its husk for these arguments (for sparks and flying keys).</summary>
        public static Vector2 PodCenter(Vector2 spot, float unit, float grown) =>
            new(spot.x, spot.y - 0.5f * unit * grown - 0.475f * unit * grown);

        /// <summary>The seed a stolen ability flies over as (spinning).</summary>
        public static void Seed(Vector2 at, float unit, Color core, float spin) =>
            CardGui.Diamond(at, 0.3f * unit, Tint(core), spin);

        // ---------- Blue: the weeping figure ----------

        public static readonly Color BlueBody = new(0.2f, 0.4f, 0.95f);
        public static readonly Color TearColor = new(0.55f, 0.75f, 1f, 0.95f);
        static readonly Color BlueEye = new(0.08f, 0.1f, 0.25f);
        static readonly Color Foam = new(0.85f, 0.93f, 1f, 0.9f);
        static readonly Color Surface = new(0.35f, 0.6f, 1f, 0.62f);
        static readonly Color Deep = new(0.05f, 0.12f, 0.45f, 0.82f);

        /// <summary>
        /// The Blue boss, its body centered on `center`. facing: the side its eyes are on. crying: sobbing shoulders and
        /// a trickle of tears. inhale 0..1 swells it (a sigh). lean 0..1 tips it toward `facing` (a throw). settled:
        /// calm, eyes shut (after the last catch).
        /// </summary>
        public static void Weeper(Vector2 center, float unit, int facing, float time, bool crying = true, float inhale = 0f,
                                  float lean = 0f, float jolt = 0f, bool settled = false, float flash = 0f)
        {
            float sob = crying ? Mathf.Sin(time * 9f) * 0.04f : Mathf.Sin(time * 2f) * 0.02f;
            float swell = 1f + inhale * 0.18f;
            var jitter = jolt > 0f ? new Vector2(Mathf.Sin(time * 83f), Mathf.Cos(time * 71f)) * jolt * 0.15f * unit : Vector2.zero;
            var c = center + jitter;
            float w = 1.4f * unit * swell * (1f + sob), h = 1.8f * unit * swell * (1f - sob);
            var body = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);

            var prev = CardGui.Rotate(c, facing * 18f * Mathf.Sin(Mathf.Clamp01(lean) * Mathf.PI * 0.5f));
            CardGui.Box(Expand(body, 2.5f), Tint(Outline));
            CardGui.Box(body, Flashed(BlueBody, flash));
            GUI.matrix = prev;

            // Eyes on the side it faces, cast down; shut once it has settled.
            for (int i = 0; i < 2; i++)
            {
                var eye = c + new Vector2(facing * 1.4f * unit * (0.12f + i * 0.2f) * swell, -1.8f * unit * 0.18f * swell);
                float d = 0.22f * unit;
                CardGui.Ellipse(eye, d, settled ? d * 0.25f : d, Tint(BlueEye));
            }

            // Tears run from its eyes while it cries.
            for (int i = 0; crying && i < 3; i++)
            {
                float k = Mathf.Repeat(time * 1.6f + i * 0.41f, 1f);
                var eye = c + new Vector2(facing * 1.4f * unit * (0.12f + (i % 2) * 0.2f) * swell, -1.8f * unit * 0.18f * swell + 0.15f * unit);
                var at = eye + new Vector2(facing * 0.4f * unit * k * 0.6f, 2.5f * unit * k * 0.6f);
                CardGui.Diamond(at, 0.12f * unit * (1f + k), Tint(new Color(TearColor.r, TearColor.g, TearColor.b, (1f - k) * (1f - k))), k * 200f);
            }
        }

        /// <summary>A Blue tear in flight: a drop with a tail along its path (scale: how much bigger than in the game).</summary>
        public static void Tear(Vector2 at, Vector2 velocity, float unit, float scale = 1f, float alpha = 1f)
        {
            float r = 0.125f * unit * scale;
            var back = velocity.sqrMagnitude > 1f ? -velocity.normalized : Vector2.up;
            var col = new Color(TearColor.r, TearColor.g, TearColor.b, TearColor.a * alpha);
            CardGui.Disc(at, r + 2f, Tint(new Color(Outline.r, Outline.g, Outline.b, Outline.a * alpha)));
            for (int i = 3; i >= 1; i--) CardGui.Disc(at + back * r * 0.9f * i, r * (1f - i * 0.22f), Tint(col));
            CardGui.Disc(at, r, Tint(col));
            CardGui.Disc(at + new Vector2(-0.3f, -0.3f) * r, r * 0.35f, Tint(new Color(1f, 1f, 1f, 0.8f * alpha)));
        }

        /// <summary>BlueWater over `area` with its surface at `surfaceY`: deep water, a lighter band, a foam crest, glints.</summary>
        public static void Water(Rect area, float surfaceY, float time, float unit)
        {
            if (surfaceY >= area.yMax) return;
            const float column = 6f;
            float band = 1.6f * unit * 0.6f, wave = 0.16f * unit * 1.5f;
            for (float x = area.x; x < area.xMax; x += column)
            {
                float cw = Mathf.Min(column, area.xMax - x);
                float y = surfaceY + Mathf.Sin(x / (5f * unit) * Mathf.PI * 2f + time * 1.6f) * wave
                                   + Mathf.Sin(x / (2.3f * unit) * Mathf.PI * 2f - time * 2.4f) * wave * 0.45f;
                y = Mathf.Max(y, area.y);
                CardGui.Box(new Rect(x, y, cw, area.yMax - y), Tint(Deep));
                CardGui.Box(new Rect(x, y, cw, Mathf.Min(band, area.yMax - y)), Tint(new Color(Surface.r, Surface.g, Surface.b, 0.5f)));
                CardGui.Box(new Rect(x, y - 1.5f, cw, 3.5f), Tint(Foam));
            }
            for (int i = 0; i < 4; i++) // glints sliding along the surface
            {
                float gx = area.x + Mathf.Repeat(time * (40f + i * 13f) + i * 211f, area.width);
                CardGui.Box(new Rect(gx, surfaceY + 8f + i * 5f, 18f + i * 4f, 2f), Tint(new Color(1f, 1f, 1f, 0.35f)));
            }
            for (int i = 0; i < 5; i++) // bubbles
            {
                float k = Mathf.Repeat(time * 0.6f + i * 0.23f, 1f);
                float by = area.yMax - k * (area.yMax - surfaceY - 6f);
                if (by <= surfaceY + 4f) continue;
                CardGui.Ring(new Vector2(area.x + area.width * Hash(i * 7.3f) + Mathf.Sin(time * 3f + i) * 5f, by), 3f + 2f * Hash(i), 1.5f,
                             Tint(new Color(Foam.r, Foam.g, Foam.b, 0.6f * (1f - k))));
            }
        }

        // ---------- Indigo: the seer ----------

        public static readonly Color SeerBody = new(0.3f, 0.2f, 0.65f);
        static readonly Color EyeWhite = new(0.95f, 0.93f, 1f);
        static readonly Color Pupil = new(0.08f, 0.03f, 0.2f);

        /// <summary>
        /// The Indigo boss centered on `center` (its bob is added here). eyeOpen 0..1 (shut while it casts). look: where
        /// the pupil points (x right, y up). charge 0..1 pumps the body and brightens the halo (casting, glowing).
        /// </summary>
        public static void Seer(Vector2 center, float unit, Color accent, float time, float eyeOpen, Vector2 look,
                                float charge = 0f, float jolt = 0f, float bob = 0.18f, float flash = 0f)
        {
            var c = center + new Vector2(0f, -Mathf.Sin(time * 2.1f) * bob * unit);
            float pump = 1f + 0.12f * charge + 0.25f * jolt;

            float halo = 1.7f * unit * (1f + 0.25f * charge + 0.15f * jolt);
            CardGui.Glow(c, halo * 1.15f, Tint(new Color(accent.r, accent.g, accent.b, 0.12f + 0.25f * charge)));
            CardGui.Ring(c, halo, Mathf.Max(2f, 0.07f * halo), Tint(new Color(accent.r, accent.g, accent.b, 0.3f + 0.6f * charge)));

            float side = 1.5f * unit * pump;
            CardGui.Diamond(c, side + 5f, Tint(Outline));
            CardGui.Diamond(c, side, Flashed(SeerBody, flash));

            float open = Mathf.Max(0.06f, eyeOpen);
            var eye = c + new Vector2(0f, -0.05f * unit);
            float d = 0.9f * unit;
            CardGui.Ellipse(eye, d, d * open, Tint(EyeWhite));
            if (eyeOpen > 0.2f)
            {
                var pupilAt = eye + new Vector2(look.x, -look.y) * d * 0.24f * eyeOpen;
                CardGui.Ellipse(pupilAt, d * 0.42f, d * 0.42f * open, Tint(Color.Lerp(Pupil, accent, charge * 0.8f)));
            }
        }

        /// <summary>The light its eye pools on the floor below it (only the real one casts it).</summary>
        public static void LightPool(Vector2 floor, float unit, Color accent, float near)
        {
            var tint = Color.Lerp(accent, Color.white, 0.3f);
            float w = 2.6f * unit * (0.7f + 0.5f * near), h = 0.45f * unit;
            CardGui.GlowRect(new Rect(floor.x - w * 0.5f, floor.y - h * 0.5f, w, h), Tint(new Color(tint.r, tint.g, tint.b, 0.3f + 0.5f * near)));
        }

        /// <summary>
        /// IndigoSigil around `center`: age seconds into a cast of `castTime` (charging), or `released` seconds after it
        /// went off (-1 = still charging): it flings outward (`releaseGrowth` x its size; 2.5 in the game) and fades.
        /// </summary>
        public static void Sigil(Vector2 center, float unit, Color accent, float age, float castTime, float released = -1f, float releaseGrowth = 2.5f)
        {
            float scale, alpha, charge;
            float a = Mathf.Min(age, castTime);
            float spin = 50f * a + 70f * a * a * a / Mathf.Max(0.01f, castTime * castTime);
            if (released >= 0f)
            {
                float r = Mathf.Clamp01(released / 0.5f);
                if (r >= 1f) return;
                scale = 1f + releaseGrowth * Mathf.Sqrt(r);
                alpha = (1f - r) * (1f - r);
                charge = 1f;
                spin += 260f * released;
            }
            else
            {
                float g = Mathf.Clamp01(age / 0.45f);
                scale = 1f - (1f - g) * (1f - g) * (1f - g);
                alpha = 1f;
                charge = Mathf.Clamp01(age / castTime);
            }
            var hot = Color.Lerp(accent, Color.white, charge * 0.5f);

            CardGui.Glow(center, Mathf.Lerp(2f, 5f, charge) * 0.5f * scale * unit * 1.4f, Tint(new Color(hot.r, hot.g, hot.b, alpha * Mathf.Lerp(0.12f, 0.4f, charge))));
            float outer = 2.6f * unit * scale;
            CardGui.Ring(center, outer, Mathf.Max(1.5f, 0.05f * outer), Tint(new Color(hot.r, hot.g, hot.b, 0.9f * alpha)));
            float throb = 1f + 0.06f * Mathf.Sin(age * Mathf.Lerp(6f, 22f, charge));
            float inner = 1.65f * unit * scale * throb;
            CardGui.Ring(center, inner, 0.22f * inner, Tint(new Color(hot.r, hot.g, hot.b, 0.7f * alpha)));
            // Tick marks on the inner ring turn with it, so the rings read as spinning.
            for (int i = 0; i < 12; i++)
            {
                float t = (-spin + i * 30f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(t), -Mathf.Sin(t));
                CardGui.Line(center + dir * inner * 0.8f, center + dir * inner * 1.02f, 2f, Tint(new Color(1f, 1f, 1f, 0.45f * alpha)));
            }

            for (int i = 0; i < 6; i++)
            {
                float t = (spin * 0.8f + i * 60f) * Mathf.Deg2Rad;
                var at = center + new Vector2(Mathf.Cos(t), -Mathf.Sin(t)) * 2.1f * unit * scale;
                var col = i % 2 == 0 ? hot : Color.white;
                CardGui.Diamond(at, 0.35f * unit, Tint(new Color(col.r, col.g, col.b, alpha)), spin * 2f);
            }

            // Motes of light drawn in from all around, faster and thicker as it charges.
            if (released >= 0f) return;
            int motes = 10 + Mathf.RoundToInt(10f * charge);
            for (int i = 0; i < motes; i++)
            {
                float k = Mathf.Repeat(age * Mathf.Lerp(1.4f, 2.6f, charge) + i / (float)motes, 1f);
                float seed = Mathf.Floor(age * Mathf.Lerp(1.4f, 2.6f, charge) + i / (float)motes) * 13.7f + i * 3.3f;
                float ang = Hash(seed) * Mathf.PI * 2f;
                var from = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(0.6f, 1f, Hash(seed + 1.7f)) * 7f * unit * 0.4f;
                var col = Color.Lerp(accent, Color.white, Hash(seed + 4.1f) * 0.6f);
                CardGui.Diamond(center + from * (1f - k * k), Mathf.Lerp(0.12f, 0.25f, Hash(seed + 2.9f)) * unit,
                                Tint(new Color(col.r, col.g, col.b, Mathf.Sin(k * Mathf.PI))));
            }
        }

        // ---------- Violet: the king ----------

        /// <summary>VioletFigure's targets (same names, same angles: 0 = arm hanging, + = swung forward and up).</summary>
        public struct KingPose
        {
            public float swordArm, swordTwist, offArm, offTwist, crouch, lean, headTilt, bladeGlow, handGlow, capeWind, aura;

            /// <summary>VioletBoss.ThronePose: standing on his hill, sword resting.</summary>
            public static KingPose Throne => new() { swordArm = 28f, swordTwist = -38f, offArm = 14f, offTwist = -20f, lean = -3f, capeWind = 0.25f };
            /// <summary>VioletBoss.IntroStance: sword raised, cape billowing.</summary>
            public static KingPose Intro => new() { swordArm = 172f, swordTwist = -8f, offArm = 35f, offTwist = -20f, lean = -6f, crouch = 0.12f, capeWind = 1.2f, bladeGlow = 0.6f };
            /// <summary>Far.Slash wind-up (crescent waves) and its release.</summary>
            public static KingPose SlashUp => new() { swordArm = 205f, swordTwist = 0f, offArm = 14f, offTwist = -20f, bladeGlow = 1f, lean = -8f, crouch = 0.25f, capeWind = 0.25f };
            public static KingPose SlashDown => new() { swordArm = -25f, swordTwist = 0f, offArm = 14f, offTwist = -20f, bladeGlow = 1f, lean = 22f, crouch = 0.25f, capeWind = 0.25f };
            /// <summary>Far.Slam wind-up (ripples) and its release.</summary>
            public static KingPose SlamUp => new() { swordArm = 190f, swordTwist = 0f, offArm = 14f, offTwist = -20f, crouch = 0.6f, bladeGlow = 1f, capeWind = 0.25f };
            public static KingPose SlamDown => new() { swordArm = 55f, swordTwist = 50f, offArm = 14f, offTwist = -20f, crouch = 0.75f, lean = 25f, bladeGlow = 1f, capeWind = 0.25f };
            /// <summary>Far.Cast (beams, rain, needles): the free hand raised and glowing.</summary>
            public static KingPose Cast => new() { swordArm = 28f, swordTwist = -38f, offArm = 115f, offTwist = -20f, handGlow = 1f, capeWind = 0.6f, lean = -4f };
            /// <summary>VioletBoss.PointPose: the greatsword pointed at the player.</summary>
            public static KingPose Point => new() { swordArm = 92f, swordTwist = 0f, offArm = 20f, offTwist = -20f, lean = 8f, crouch = 0.1f, capeWind = 1.5f, bladeGlow = 0.7f };

            public static KingPose Lerp(KingPose a, KingPose b, float t)
            {
                t = Mathf.Clamp01(t);
                return new KingPose
                {
                    swordArm = Mathf.Lerp(a.swordArm, b.swordArm, t), swordTwist = Mathf.Lerp(a.swordTwist, b.swordTwist, t),
                    offArm = Mathf.Lerp(a.offArm, b.offArm, t), offTwist = Mathf.Lerp(a.offTwist, b.offTwist, t),
                    crouch = Mathf.Lerp(a.crouch, b.crouch, t), lean = Mathf.Lerp(a.lean, b.lean, t),
                    headTilt = Mathf.Lerp(a.headTilt, b.headTilt, t), bladeGlow = Mathf.Lerp(a.bladeGlow, b.bladeGlow, t),
                    handGlow = Mathf.Lerp(a.handGlow, b.handGlow, t), capeWind = Mathf.Lerp(a.capeWind, b.capeWind, t),
                    aura = Mathf.Lerp(a.aura, b.aura, t),
                };
            }
        }

        struct Node
        {
            public Vector2 p; // figure space: x forward, y up, feet at the origin
            public float r;   // degrees, counterclockwise
        }

        static Node At(Node parent, Vector2 local, float rotation) =>
            new() { p = parent.p + Turn(local, parent.r), r = parent.r + rotation };

        static Vector2 Turn(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>Where a point of the king's figure space lands on screen (for bursts at his blade or hand).</summary>
        public static Vector2 KingPoint(Vector2 feet, float unit, int facing, Vector2 figurePoint) =>
            feet + new Vector2(facing * figurePoint.x * unit, -figurePoint.y * unit);

        /// <summary>
        /// The Violet king standing with his feet at `feet`, facing +1 (right) or -1. Same rig as VioletFigure: hips,
        /// legs, torso, a five-segment cape, pauldrons, both arms, helm with its glowing visor, the crown and its gems,
        /// the greatsword in his right hand. Returns his sword tip and off hand (screen) for effects.
        /// </summary>
        public static (Vector2 swordTip, Vector2 offHand) King(Vector2 feet, float unit, int facing, KingPose pose, float time,
                                                               float flash = 0f, float glint = 0f)
        {
            float breath = Mathf.Sin(time * 1.9f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 18f);
            var root = new Node();
            var hips = At(root, new Vector2(0f, 1.35f - pose.crouch * 0.38f + breath * 0.02f), 0f);
            var legBack = At(hips, new Vector2(-0.16f, 0f), -8f - pose.crouch * 22f);
            var legFront = At(hips, new Vector2(0.18f, 0f), 6f + pose.crouch * 28f);
            var torso = At(hips, Vector2.zero, -pose.lean + breath * 0.8f);
            var head = At(torso, new Vector2(0.04f, 1.12f), -pose.headTilt);
            var crown = At(head, new Vector2(0f, 0.5f), 0f);
            var armBack = At(torso, new Vector2(-0.4f, 0.93f), pose.offArm - breath * 1.5f);
            var offHand = At(armBack, new Vector2(0f, -1f), 0f);
            var armFront = At(torso, new Vector2(0.5f, 0.93f), pose.swordArm + breath * 1.5f);
            var sword = At(At(armFront, new Vector2(0f, -1f), 0f), Vector2.zero, pose.swordTwist);

            void Part(Sprite s, Node n, Color color, float sx = 1f, float sy = 1f)
            {
                var at = feet + new Vector2(facing * n.p.x * unit, -n.p.y * unit);
                CardGui.Sprite(s, at, new Vector2(unit * sx, unit * sy), Flashed(color, flash), facing < 0, -facing * n.r);
            }

            // Soft shadow on his ground.
            CardGui.GlowRect(new Rect(feet.x - 1.1f * unit, feet.y - 0.18f * unit, 2.2f * unit, 0.36f * unit), Tint(new Color(0.1f, 0.03f, 0.18f, 0.35f)));

            // The cape trails from his back, each segment swaying a little more than the last.
            float[] widths = { 0.95f, 1.05f, 1.15f, 1.25f, 1.35f, 1.45f };
            float wind = pose.capeWind;
            var seg = At(torso, new Vector2(-0.3f, 1.02f), 0f);
            for (int i = 0; i < widths.Length - 1; i++)
            {
                float sway = Mathf.Sin(time * (1.6f + wind * 3f) - i * 0.7f) * (2.5f + i * 1.2f) * (1f + wind * 2.5f);
                float angle = (i == 0 ? -10f - pose.lean * 0.6f : -5f) - wind * (8f + i * 4f) + sway;
                seg = At(seg, i == 0 ? Vector2.zero : new Vector2(0f, -0.48f), angle);
                Part(VioletShapes.CapeSegment(widths[i], widths[i + 1], 0.5f, i == widths.Length - 2), seg,
                     Color.Lerp(VioletShapes.Deep, VioletShapes.Dark, i * 0.08f));
            }

            Part(VioletShapes.Arm, armBack, VioletShapes.Deep);
            Part(VioletShapes.Leg, legBack, VioletShapes.Dark);
            Part(VioletShapes.Pauldron, At(torso, new Vector2(-0.44f, 1.0f), 0f), VioletShapes.Mid);
            Part(VioletShapes.Torso, torso, VioletShapes.Mid);
            Part(VioletShapes.Gem, At(torso, new Vector2(0.02f, 0.02f), 0f), VioletShapes.Glow, 0.28f, 0.22f);
            Part(VioletShapes.Leg, legFront, VioletShapes.Mid);
            Part(VioletShapes.Helm, head, VioletShapes.Dark);
            Part(VioletShapes.Visor, At(head, new Vector2(0.02f, 0.3f), 0f),
                 Color.Lerp(VioletShapes.Glow, Color.white, 0.4f * Mathf.Max(pose.bladeGlow, pose.handGlow) + 0.3f * pulse * pose.aura));
            Part(VioletShapes.Crown, crown, VioletShapes.Bright);
            foreach (var gx in new[] { -0.16f, 0f, 0.16f })
                Part(VioletShapes.Gem, At(crown, new Vector2(gx, 0.08f), 0f), VioletShapes.Glow, 0.12f, 0.14f);
            Part(VioletShapes.Pauldron, At(torso, new Vector2(0.46f, 0.98f), 0f), VioletShapes.Bright);
            Part(VioletShapes.Blade, sword, VioletShapes.Steel);
            var edge = VioletShapes.Glow;
            edge.a = 0.15f + 0.85f * pose.bladeGlow * (0.75f + 0.25f * pulse);
            Part(VioletShapes.BladeEdge, sword, edge);
            Part(VioletShapes.Arm, armFront, VioletShapes.Mid);
            Part(VioletShapes.Hilt, sword, VioletShapes.Bright);

            // The free hand's glow, brighter while it casts.
            var hand = KingPoint(feet, unit, facing, offHand.p);
            float h = Mathf.Clamp01(pose.handGlow);
            CardGui.Glow(hand, (0.35f + 0.5f * h) * unit, Tint(new Color(VioletShapes.Glow.r, VioletShapes.Glow.g, VioletShapes.Glow.b, 0.25f + 0.75f * h)));
            if (h > 0.05f) CardGui.Ring(hand, (0.25f + 0.7f * h) * unit, 2f, Tint(new Color(1f, 1f, 1f, 0.7f * h)));

            if (glint > 0f)
            {
                var g = KingPoint(feet, unit, facing, At(crown, new Vector2(0.06f, 0.62f), 0f).p);
                CardGui.Sprite(VioletShapes.Glint, g, Vector2.one * unit * (0.2f + 0.7f * glint), Tint(new Color(1f, 1f, 1f, glint)), false, time * 90f);
            }
            return (KingPoint(feet, unit, facing, At(sword, new Vector2(0f, -2.8f), 0f).p), hand);
        }

        // ---------- Helpers ----------

        static Rect Expand(Rect r, float by) => new(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        /// <summary>A stable pseudo-random 0..1 for a seed, so the drawings never flicker between frames.</summary>
        public static float Hash(float n)
        {
            float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
