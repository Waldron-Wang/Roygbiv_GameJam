using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The little looping animations on the instruction cards, one per InstructionDemo. Each draws into the
    /// card's light "stage" at time `time` (seconds since the card opened, unscaled) and fills Press with how
    /// far each key is held this frame, so the demo's keycaps and the caption's press together.
    /// Placeholders for the mechanic, not to scale: timings live at the top of each demo.
    /// This file: Yellow, Orange, Red and the shared pieces. InstructionDemosLate.cs: Green, Blue, Indigo, Violet.
    /// </summary>
    static partial class InstructionDemos
    {
        /// <summary>Key name -> 0..1, how far it's pressed this frame. Rebuilt every Draw.</summary>
        public static readonly Dictionary<string, float> Press = new();

        const float PlayerUnit = 90f; // GUI px per world unit: the player reads bigger than to scale
        const float BossUnit = 64f;
        const float KeySize = 54f;

        static readonly Color Ink = new(0.08f, 0.08f, 0.1f);
        static readonly Color ShotColor = new(1f, 0.85f, 0.23f); // Light Shot is always yellow
        static readonly Color[] Spectrum =
        {
            Hex(0xFF4A3D), Hex(0xFF9A2E), Hex(0xFFD93B), Hex(0x4CD964), Hex(0x3D8BFF), Hex(0x7B6CFF), Hex(0xC266FF),
        };

        public static void Draw(InstructionData d, Rect stage, float time)
        {
            Press.Clear();
            time = Mathf.Max(0f, time);
            float ground = stage.yMax - 54f;
            switch (d.demo)
            {
                case InstructionDemo.Reflect: Reflect(d, stage, ground, time); break;
                case InstructionDemo.ShootLatch: ShootLatch(d, stage, ground, time); break;
                case InstructionDemo.Overheat: Overheat(d, stage, ground, time); break;
                case InstructionDemo.Steal: Steal(d, stage, ground, time); break;
                case InstructionDemo.Climb: Climb(d, stage, time); break;
                case InstructionDemo.FlipControls: FlipControls(d, stage, ground, time); break;
                case InstructionDemo.Gauntlet: Gauntlet(d, stage, ground, time); break;
                default: Mystery(d, stage, time); break;
            }
        }

        // ---------- Yellow: punch the orb back ----------

        static void Reflect(InstructionData d, Rect stage, float ground, float time)
        {
            const float period = 2.8f, launch = 0.25f, contact = 1.2f, back = 1.6f;
            float t = time % period;
            float pressAt = contact - 0.08f; // the swing's active frame lands on the orb
            var feet = new Vector2(stage.x + stage.width * 0.26f, ground);
            var bossAt = new Vector2(stage.x + stage.width * 0.74f, stage.y + 140f + Mathf.Sin(time * 2.2f) * 6f);
            var hit = feet + new Vector2(64f, -80f);

            Floor(stage, ground);
            float sinceHit = t - back;
            bool hurt = sinceHit >= 0f && sinceHit < 0.45f;
            Boss(d, bossAt + Shake(sinceHit, 0.35f, 7f), hurt, false, sinceHit >= 0f && sinceHit < 0.07f);
            Spark(bossAt, Seg(t, back, back + 0.35f), d.accent, 90f);

            Player(d, feet, t, t >= pressAt ? pressAt : -1f);
            SetPress(d.keys, Tap(t, pressAt));
            CardGui.KeyRow(d.keys, new Vector2(feet.x, feet.y - 200f), KeySize, d.accent, Press);

            if (t >= launch && t < contact)
            {
                // Falls, bounces once off the floor, and floats up into the swing: slow and bouncy.
                float k = Seg(t, launch, contact);
                var from = bossAt + new Vector2(-70f, 30f);
                var bounce = new Vector2(Mathf.Lerp(from.x, hit.x, 0.55f), ground - 16f);
                Vector2 pos;
                if (k < 0.55f)
                {
                    float u = k / 0.55f;
                    pos = new Vector2(Mathf.Lerp(from.x, bounce.x, u), Mathf.Lerp(from.y, bounce.y, u * u));
                }
                else
                {
                    float u = (k - 0.55f) / 0.45f;
                    pos = new Vector2(Mathf.Lerp(bounce.x, hit.x, u), Mathf.Lerp(bounce.y, hit.y, 1f - (1f - u) * (1f - u)));
                }
                Orb(d, pos, 15f, d.accent);
            }
            else if (t >= contact && t < back)
            {
                // Reflected: faster, brighter, homing straight back onto it.
                float k = Seg(t, contact, back);
                for (int i = 3; i >= 1; i--)
                {
                    float trail = Mathf.Max(0f, k - i * 0.07f);
                    CardGui.Disc(Vector2.Lerp(hit, bossAt, trail * trail), 14f - i * 3f, WithAlpha(d.accent, 0.35f - i * 0.08f));
                }
                Orb(d, Vector2.Lerp(hit, bossAt, k * k), 16f, Color.Lerp(d.accent, Color.white, 0.4f));
            }
            Spark(hit, Seg(t, contact, contact + 0.3f), d.accent);
        }

        // ---------- Orange: shoot the latch, the cage drops, run into it ----------

        static void ShootLatch(InstructionData d, Rect stage, float ground, float time)
        {
            const float period = 3.6f, pressAt = 0.7f, shotHits = 1.0f, landed = 1.25f;
            const float scrollSpeed = 260f, catchUpSpeed = 80f, catchGap = 95f;
            float t = time % period;

            float homeX = stage.x + stage.width * 0.17f, spotX = stage.x + stage.width * 0.7f;
            var latch = new Vector2(stage.x + stage.width * 0.42f, stage.y + 76f);
            var cageSize = new Vector2(140f, 128f);
            float hangBottom = stage.y + 26f + cageSize.y;

            // After the drop the trapped boss stays put in the world while the screen scrolls on,
            // and the player runs into it: that's the catch.
            float catchAt = landed + (spotX - homeX - catchGap) / (scrollSpeed + catchUpSpeed);
            float bossX = spotX, playerX = homeX;
            if (t >= landed && t < catchAt)
            {
                bossX = spotX - (t - landed) * scrollSpeed;
                playerX = homeX + (t - landed) * catchUpSpeed;
            }
            else if (t >= catchAt)
            {
                float back = Smooth(Seg(t, catchAt + 0.15f, catchAt + 0.75f)); // dashes back to its spot
                float atCatch = spotX - (catchAt - landed) * scrollSpeed;
                bossX = Mathf.Lerp(atCatch, spotX, back);
                playerX = Mathf.Lerp(homeX + (catchAt - landed) * catchUpSpeed, homeX, back);
            }
            bool trapped = t >= landed && t < catchAt;

            Floor(stage, ground, time * scrollSpeed);

            // Rope + latch.
            bool latchHit = t >= shotHits;
            var cageTop = new Vector2(spotX + 20f, hangBottom - cageSize.y);
            if (!latchHit) CardGui.Line(latch, cageTop, 2.5f, WithAlpha(Ink, 0.6f));
            CardGui.Box(new Rect(latch.x - 14f, latch.y - 14f, 28f, 28f), latchHit ? new Color(0.55f, 0.55f, 0.55f) : Ink);
            if (!latchHit) CardGui.Outline(new Rect(latch.x - 17f, latch.y - 17f, 34f, 34f), d.accent, 3f);
            Spark(latch, Seg(t, shotHits, shotHits + 0.3f), ShotColor, 40f);

            // Boss: runs, or sits stunned in the cage.
            var boss = trapped || (t >= catchAt && t < catchAt + 0.15f) ? d.bossHurt : Loop(d.bossMove, time, 8f);
            float sinceCatch = t - catchAt;
            Boss(d, new Vector2(bossX, ground) + Shake(t - landed, 0.3f, 5f) + Shake(sinceCatch, 0.3f, 6f), false, true,
                 sinceCatch >= 0f && sinceCatch < 0.07f, boss);
            Spark(new Vector2(bossX + 10f, ground - 60f), Seg(t, catchAt, catchAt + 0.35f), d.accent, 80f);

            // Cage: hangs, drops, holds, then bursts on the catch.
            if (t < catchAt)
            {
                float fall = Seg(t, shotHits, landed);
                float bottom = Mathf.Lerp(hangBottom, ground, fall * fall);
                float x = (trapped ? bossX : spotX) + 20f;
                Cage(new Rect(x - cageSize.x * 0.5f, bottom - cageSize.y, cageSize.x, cageSize.y), 1f);
            }
            else if (sinceCatch < 0.3f)
            {
                float k = sinceCatch / 0.3f;
                var size = cageSize * (1f + 0.4f * k);
                Cage(new Rect(bossX + 20f - size.x * 0.5f, ground - size.y, size.x, size.y), 1f - k);
            }

            // Player: always running; the shot leaves its hand.
            var feet = new Vector2(playerX, ground);
            Player(d, feet, time);
            SetPress(d.keys, Tap(t, pressAt));
            CardGui.KeyRow(d.keys, new Vector2(feet.x, feet.y - 190f), KeySize, d.accent, Press);
            if (t >= pressAt + 0.05f && t < shotHits)
            {
                var from = feet + new Vector2(40f, -70f);
                Orb(d, Vector2.Lerp(from, latch, Seg(t, pressAt + 0.05f, shotHits)), 9f, ShotColor);
            }
        }

        static void Cage(Rect r, float alpha)
        {
            const float bar = 7f;
            var c = WithAlpha(Ink, 0.9f * alpha);
            CardGui.Box(new Rect(r.x, r.y, r.width, bar), c);
            CardGui.Box(new Rect(r.x, r.yMax - bar, r.width, bar), c);
            for (int i = 0; i < 4; i++)
                CardGui.Box(new Rect(Mathf.Lerp(r.x, r.xMax - bar, i / 3f), r.y, bar, r.height), c);
        }

        // ---------- Red: hits feed the rage, it overheats, then hits land ----------

        static void Overheat(InstructionData d, Rect stage, float ground, float time)
        {
            const float period = 3.9f, tellAt = 1.5f, dropAt = 2.1f, dropTime = 0.25f;
            const float swing = 0.12f; // press -> the attack's active frame
            float[] armoredPresses = { 0.3f, 0.8f, 1.3f };
            float[] damagePresses = { 2.6f, 3.05f };
            const int maxHp = 4;
            float t = time % period;

            var bossFeet = new Vector2(stage.x + stage.width * 0.64f, ground);
            var feet = new Vector2(bossFeet.x - 200f, ground);
            var contact = new Vector2(bossFeet.x - 92f, ground - 82f);

            Floor(stage, ground);

            // Rage: each armored hit adds a third; full = overheat. Damage: each exposed hit takes a pip.
            int armoredHits = 0, damageHits = 0;
            float lastArmored = float.NegativeInfinity, lastDamage = float.NegativeInfinity;
            foreach (var p in armoredPresses) if (t >= p + swing) { armoredHits++; lastArmored = p + swing; }
            foreach (var p in damagePresses) if (t >= p + swing) { damageHits++; lastDamage = p + swing; }
            float rage = armoredHits == 0 ? 0f : (armoredHits - 1 + Seg(t, lastArmored, lastArmored + 0.15f)) / armoredPresses.Length;
            bool overheated = t >= tellAt;
            float drop = Smooth(Seg(t, dropAt, dropAt + dropTime));

            // Boss: armored and jolting, then trembling hot, then down and exposed.
            var at = bossFeet + Shake(t - lastArmored, 0.2f, 4f) + Shake(t - lastDamage, 0.3f, 7f);
            if (overheated && t < dropAt) at += new Vector2(Mathf.Sin(t * 70f), Mathf.Cos(t * 53f)) * 3f;
            if (overheated)
                CardGui.Glow(bossFeet + new Vector2(0f, -70f), 150f + 10f * Mathf.Sin(t * 30f), WithAlpha(Color.Lerp(d.accent, ShotColor, 0.5f), 0.5f));
            Boss(d, at + Vector2.up * 10f * drop, drop > 0f, true, t - lastDamage < 0.07f, null, -16f * drop);
            for (int i = 0; overheated && i < 4; i++) // smoke
            {
                float k = Mathf.Repeat(t * 0.8f + i * 0.25f, 1f);
                var puff = bossFeet + new Vector2(-50f + i * 32f, -140f - k * 70f);
                CardGui.Disc(puff, 8f + 14f * k, new Color(0.35f, 0.33f, 0.33f, 0.45f * (1f - k)));
            }

            foreach (var p in armoredPresses) Spark(contact, Seg(t, p + swing, p + swing + 0.25f), new Color(0.5f, 0.5f, 0.55f), 36f); // clang
            foreach (var p in damagePresses) Spark(contact, Seg(t, p + swing, p + swing + 0.3f), d.accent, 64f);

            // Rage bar, overheat label, and HP pips.
            var bar = new Rect(bossFeet.x - 100f, stage.y + 34f, 200f, 16f);
            bool flash = overheated && Mathf.Repeat(t * 8f, 1f) < 0.5f;
            CardGui.Text(new Rect(bar.x - 90f, bar.y - 6f, 80f, 28f), "RAGE", 16, Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            CardGui.Box(bar, WithAlpha(Ink, 0.85f));
            CardGui.Box(new Rect(bar.x + 3f, bar.y + 3f, (bar.width - 6f) * (overheated ? 1f : rage), bar.height - 6f), flash ? ShotColor : d.accent);
            if (overheated)
                CardGui.Text(new Rect(bar.x - 50f, bar.yMax + 2f, bar.width + 100f, 36f), "OVERHEAT!", 26, flash ? d.accent : Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            for (int i = 0; i < maxHp; i++)
            {
                var pip = new Rect(bossFeet.x - 50f + i * 26f, ground + 16f, 20f, 12f);
                CardGui.Box(pip, i < maxHp - damageHits ? d.accent : WithAlpha(Ink, 0.2f));
            }

            float lastPress = Last(t, armoredPresses, damagePresses);
            Player(d, feet, t, lastPress);
            float press = 0f;
            foreach (var p in armoredPresses) press = Mathf.Max(press, Tap(t, p));
            foreach (var p in damagePresses) press = Mathf.Max(press, Tap(t, p));
            SetPress(d.keys, press);
            CardGui.KeyRow(d.keys, new Vector2(feet.x - 20f, feet.y - 210f), KeySize, d.accent, Press);
        }

        // ---------- Violet / None: no mechanic yet ----------

        static void Mystery(InstructionData d, Rect stage, float time)
        {
            var c = stage.center;
            CardGui.Glow(c, 150f + 12f * Mathf.Sin(time * 2f), WithAlpha(d.accent, 0.55f));
            for (int i = 0; i < Spectrum.Length; i++)
            {
                float a = time * 0.8f + i * Mathf.PI * 2f / Spectrum.Length;
                var at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * 120f;
                CardGui.Disc(at, 15f, Ink);
                CardGui.Disc(at, 12f, Spectrum[i]);
            }
            CardGui.Text(new Rect(c.x - 60f, c.y - 50f, 120f, 100f), "?", 72, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        // ---------- Pieces ----------

        static void Floor(Rect stage, float ground, float scroll = 0f)
        {
            CardGui.Box(new Rect(stage.x, ground, stage.width, stage.yMax - ground), new Color(0.82f, 0.82f, 0.8f));
            CardGui.Box(new Rect(stage.x, ground - 1.5f, stage.width, 3f), Ink);
            // Dashes that slide by while the chase scrolls.
            for (float x = stage.x - Mathf.Repeat(scroll, 70f) + 70f; x < stage.xMax; x += 70f)
                CardGui.Box(new Rect(x, ground + 16f, Mathf.Min(30f, stage.xMax - x), 3f), new Color(0f, 0f, 0f, 0.18f));
        }

        /// <summary>The player at `feet`: looping frames, or the action frames from `actionAt` on (-1 = none).</summary>
        static void Player(InstructionData d, Vector2 feet, float t, float actionAt = -1f, Color? tint = null, bool flip = false, float degrees = 0f)
        {
            var c = tint ?? Color.white;
            Sprite s = null;
            if (actionAt >= 0f && t >= actionAt && d.playerAction != null && d.playerAction.Length > 0)
            {
                int i = (int)((t - actionAt) * d.playerActionFps);
                if (i < d.playerAction.Length) s = d.playerAction[i];
            }
            if (!s) s = Loop(d.playerLoop, t, d.playerLoopFps);

            if (s)
            {
                CardGui.SpriteOnGround(s, feet, PlayerUnit, c, flip, degrees);
                return;
            }
            var ink = new Color(Ink.r, Ink.g, Ink.b, c.a); // no art: a stick figure
            CardGui.Round(new Rect(feet.x - 15f, feet.y - 92f, 30f, 66f), ink, 8f);
            CardGui.Disc(feet + new Vector2(0f, -110f), 15f, ink);
            CardGui.Box(new Rect(feet.x - 12f, feet.y - 30f, 5f, 30f), ink);
            CardGui.Box(new Rect(feet.x + 7f, feet.y - 30f, 5f, 30f), ink);
        }

        /// <summary>
        /// The boss with a thin dark outline, so saturated art still reads on the light stage.
        /// onGround: `at` is its feet; otherwise its pivot (body center). silhouette: a one-frame dark hit flash.
        /// </summary>
        static void Boss(InstructionData d, Vector2 at, bool hurt, bool onGround, bool silhouette = false, Sprite sprite = null, float degrees = 0f)
        {
            var s = sprite ? sprite : hurt && d.bossHurt ? d.bossHurt : d.boss;
            if (!s)
            {
                Blob(onGround ? at + Vector2.up * -70f : at, d.accent, Time.unscaledTime, Vector2.zero);
                return;
            }
            var outline = WithAlpha(Ink, 0.75f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                DrawAt(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f, outline);
            }
            DrawAt(at, silhouette ? Ink : Color.white);

            void DrawAt(Vector2 p, Color tint)
            {
                if (onGround) CardGui.SpriteOnGround(s, p, BossUnit, tint, false, degrees);
                else CardGui.Sprite(s, p, BossUnit, tint, false, degrees);
            }
        }

        /// <summary>A thorny placeholder boss for colors with no art yet.</summary>
        static void Blob(Vector2 center, Color color, float time, Vector2 shake)
        {
            center += shake;
            var thorn = Color.Lerp(color, Ink, 0.5f);
            CardGui.Glow(center, 140f, WithAlpha(color, 0.4f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f + time * 0.3f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                CardGui.Line(center + dir * 50f, center + dir * (80f + 8f * Mathf.Sin(time * 3f + i)), 7f, thorn);
            }
            CardGui.Disc(center, 60f, Ink);
            CardGui.Disc(center, 56f, color);
            CardGui.Disc(center + new Vector2(-18f, -8f), 9f, Ink);
            CardGui.Disc(center + new Vector2(18f, -8f), 9f, Ink);
        }

        static void Orb(InstructionData d, Vector2 at, float radius, Color color)
        {
            CardGui.Glow(at, radius * 3f, WithAlpha(color, 0.6f));
            CardGui.Disc(at, radius + 2.5f, WithAlpha(Ink, 0.85f));
            if (d.prop) CardGui.DrawSprite(d.prop, new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f), color);
            else CardGui.Disc(at, radius, color);
            CardGui.Disc(at + new Vector2(-0.3f, -0.3f) * radius, radius * 0.35f, new Color(1f, 1f, 1f, 0.85f));
        }

        /// <summary>A burst of rays and a ring; k goes 0 -> 1 over its life (outside that, nothing).</summary>
        static void Spark(Vector2 at, float k, Color color, float size = 46f)
        {
            if (k <= 0f || k >= 1f) return;
            var c = WithAlpha(color, 1f - k);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f + 0.3f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                CardGui.Line(at + dir * size * (0.25f + 0.6f * k), at + dir * size * (0.55f + 0.6f * k), 2f + 5f * (1f - k), c);
            }
            CardGui.Ring(at, size * (0.3f + 0.7f * k), 3f, c);
        }

        // ---------- Timing ----------

        static Sprite Loop(Sprite[] frames, float t, float fps) =>
            frames == null || frames.Length == 0 ? null : frames[(int)(t * Mathf.Max(0.01f, fps)) % frames.Length];

        static void SetPress(IEnumerable<string> keys, float amount)
        {
            if (keys == null) return;
            foreach (var k in keys) Press[k] = Mathf.Max(amount, Press.TryGetValue(k, out var p) ? p : 0f);
        }

        /// <summary>A key going down at `at`, held for `hold`, with a quick press and release.</summary>
        static float Tap(float t, float at, float hold = 0.18f)
        {
            float x = t - at;
            if (x < 0f || x > hold + 0.08f) return 0f;
            return x < 0.04f ? x / 0.04f : x < hold ? 1f : 1f - (x - hold) / 0.08f;
        }

        /// <summary>0 -> 1 -> 0 over [at, at + hold + 0.25]: a step out and back.</summary>
        static float Bump(float t, float at, float hold) =>
            Smooth(Seg(t, at, at + 0.2f)) * (1f - Smooth(Seg(t, at + hold, at + hold + 0.25f)));

        /// <summary>The latest of the given times that's already passed, or -1.</summary>
        static float Last(float t, params float[][] lists)
        {
            float last = -1f;
            foreach (var list in lists)
                foreach (var x in list)
                    if (x <= t && x > last) last = x;
            return last;
        }

        static Vector2 Shake(float since, float duration, float amount)
        {
            if (since < 0f || since > duration) return Vector2.zero;
            float f = amount * (1f - since / duration);
            return new Vector2(Mathf.Sin(since * 95f), Mathf.Cos(since * 77f)) * f;
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) =>
            Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);

        /// <summary>0 -> 1 as t goes a -> b (a step at a when b &lt;= a).</summary>
        static float Seg(float t, float a, float b) => b > a ? Mathf.Clamp01((t - a) / (b - a)) : t >= a ? 1f : 0f;

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
