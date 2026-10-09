using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The how-to demos for the later bosses: Green (Steal), Blue (Climb), Indigo (FlipControls) and Violet (Gauntlet).
    /// Same rules as the rest of InstructionDemos: they draw into the card's light stage at `time` (unscaled seconds
    /// since it opened), loop, and fill Press so keycaps press in sync. The bosses are drawn by DemoBosses with their
    /// real parts and motion. Every timing is a constant at the top of its demo.
    /// </summary>
    static partial class InstructionDemos
    {
        static readonly Color DashColor = Hex(0xFF9A2E);   // Orange gives Dash: the color its thread and pod glow in
        static readonly Color SurfColor = new(0.35f, 0.6f, 1f);
        static readonly Color SerenityCore = new(0.294f, 0.169f, 1f);  // #4B2BFF
        static readonly Color SerenityEdge = new(0.482f, 0.424f, 1f);  // #7B6CFF
        static readonly Color Rock = new(0.47f, 0.51f, 0.6f);
        static readonly Color Stone = new(0.5f, 0.45f, 0.58f);

        // ---------- Green: it takes what you used last; break the pod up close ----------

        static void Steal(InstructionData d, Rect stage, float ground, float time)
        {
            const float period = 6.6f, unit = 58f, swing = 0.08f;
            const float dashAt = 0.35f, dashTime = 0.22f, covetFrom = 0.7f, yankAt = 2.05f, seedTime = 0.55f, sproutTime = 0.35f;
            const float toPodAt = 2.9f, toPodTime = 0.5f, toBossAt = 4.75f, toBossTime = 0.45f, keyBackTime = 0.45f;
            float[] podPresses = { 3.5f, 3.85f, 4.2f };
            float[] bossPresses = { 5.35f, 5.7f };
            float t = time % period;
            float breakAt = podPresses[podPresses.Length - 1] + swing, wiltAt = breakAt + 0.05f;
            float seedEnd = yankAt + seedTime, backAt = breakAt + keyBackTime;
            string abilityKey = KeyAt(d, 0, "Shift"), attackKey = KeyAt(d, 1, "LMB");

            float x0 = stage.x + stage.width * 0.1f, x1 = x0 + 140f;
            var podSpot = new Vector2(stage.x + stage.width * 0.48f, ground);
            var bossFeet = new Vector2(stage.x + stage.width * 0.8f, ground);
            float xPod = podSpot.x - 80f, xBoss = bossFeet.x - unit - 75f;
            float px = x0;
            if (t >= dashAt) px = Mathf.Lerp(x0, x1, Smooth(Seg(t, dashAt, dashAt + dashTime)));
            if (t >= toPodAt) px = Mathf.Lerp(x1, xPod, Seg(t, toPodAt, toPodAt + toPodTime));
            if (t >= toBossAt) px = Mathf.Lerp(xPod, xBoss, Seg(t, toBossAt, toBossAt + toBossTime));
            var feet = new Vector2(px, ground);
            var keyHome = feet + new Vector2(0f, -182f);

            float alpha = CardGui.Alpha;
            CardGui.Alpha *= LoopFade(t, period);
            Floor(stage, ground);

            // The boss: sways rooted; COVETS (leans out, trembling, glowing in the color of what it's about to take);
            // yanks; WILTS when the pod breaks (droops brown, heart split open and blinking); flinches when hit.
            float covet = t >= covetFrom && t < yankAt ? Seg(t, covetFrom, yankAt) : 0f;
            float sinceYank = t - yankAt;
            float punch = sinceYank >= 0f ? Mathf.Clamp01(1f - sinceYank / 0.25f) : 0f;
            float wilt = t >= wiltAt ? Smooth(Seg(t, wiltAt, wiltAt + 0.3f)) : 0f;
            float lastBossPress = Last(t - swing, bossPresses);
            float sinceBossHit = lastBossPress >= 0f ? t - (lastBossPress + swing) : -1f;
            float hitJolt = sinceBossHit >= 0f && sinceBossHit < 0.2f ? 1f - sinceBossHit / 0.2f : 0f;

            float lean = (Mathf.Sin(time * 1.3f) + 0.4f * Mathf.Sin(time * 2.9f)) * 4f, tremble = 0f;
            var squash = new Vector2(1f - 0.03f * Mathf.Sin(time * 2.2f), 1f + 0.04f * Mathf.Sin(time * 2.2f));
            if (covet > 0f)
            {
                lean = -18f * covet; // facing the player, on its left
                squash = new Vector2(1f - 0.12f * covet, 1f + 0.18f * covet);
                tremble = 2f + 8f * covet;
            }
            if (wilt > 0f)
            {
                squash = Vector2.Lerp(Vector2.one, new Vector2(1.15f, 0.72f), wilt);
                lean = 24f * wilt + Mathf.Sin(t * 3f) * 2f; // droops away from the player
            }
            squash.x *= 1f + 0.2f * punch;
            squash.y *= 1f - 0.12f * punch;
            lean += -10f * punch + (Mathf.PerlinNoise(time * 16f, 0.71f) - 0.5f) * 2f * (tremble + 10f * hitJolt);
            float heartOpen = wilt > 0f ? (Mathf.Repeat(t - wiltAt, 0.5f) < 0.1f ? 1f : 0.75f) * wilt : 0f;
            float flash = sinceBossHit >= 0f && sinceBossHit < 0.1f ? 1f - sinceBossHit / 0.1f : 0f;
            DemoBosses.Bramble(bossFeet, unit, time, lean, squash, heartOpen, wilt, DashColor, covet > 0f ? 0.15f + 0.5f * covet : 0f, flash);
            var heart = new Vector2(bossFeet.x, bossFeet.y - 1.1f * unit * squash.y);
            foreach (var p in bossPresses) Spark(heart, Seg(t, p + swing, p + swing + 0.3f), DemoBosses.HeartOpen, 64f);

            // The thread: from its heart to the player, in the color of the ability it's about to take.
            if (covet > 0f)
            {
                var chest = feet + new Vector2(0f, -66f);
                float pulse = 0.75f + 0.25f * Mathf.Sin((t - covetFrom) * Mathf.Lerp(10f, 40f, covet));
                CardGui.Line(heart, chest, Mathf.Lerp(2.5f, 8f, covet), WithAlpha(DashColor, Mathf.Lerp(0.35f, 1f, covet) * pulse));
                CardGui.Glow(chest, 40f + 24f * covet, WithAlpha(DashColor, 0.35f * covet));
                for (int i = 0; i < 4; i++)
                {
                    float k = Mathf.Repeat(t * 1.8f + i * 0.25f, 1f);
                    var at = chest + new Vector2(Mathf.Cos(i * 1.7f + t), Mathf.Sin(i * 2.3f + t)) * (26f + 20f * k);
                    CardGui.Diamond(at, 8f * (1f - k), WithAlpha(DashColor, covet * (1f - k)), t * 90f);
                }
            }

            // The pod: the stolen ability grows on a stalk; three hits up close break it.
            float grown = Seg(t, seedEnd, seedEnd + sproutTime);
            bool podAlive = t >= seedEnd && t < breakAt;
            float podJolt = 0f;
            foreach (var p in podPresses)
            {
                float since = t - (p + swing);
                if (since >= 0f && since < 0.2f) podJolt = Mathf.Max(podJolt, 1f - since / 0.2f);
            }
            var podCenter = DemoBosses.PodCenter(podSpot, unit, 1f);
            if (podAlive) DemoBosses.Pod(podSpot, unit, time, grown, 0.15f + 0.35f * Seg(t, seedEnd, breakAt), DashColor, podJolt);
            foreach (var p in podPresses) Spark(podCenter, Seg(t, p + swing, p + swing + 0.28f), d.accent, 48f);
            Burst(podCenter, Seg(t, breakAt, breakAt + 0.45f), DashColor, 90f);

            // The player: dashes (that's what it takes), staggers at the yank, runs to the pod, breaks it, runs in, hits.
            float lastPress = Last(t, podPresses, bossPresses);
            var attack = lastPress >= 0f ? AttackFrame(d, t - lastPress) : null;
            bool dashing = t >= dashAt && t < dashAt + dashTime;
            bool running = (t >= toPodAt && t < toPodAt + toPodTime) || (t >= toBossAt && t < toBossAt + toBossTime);
            Sprite body = attack ? attack : dashing || running ? RunFrame(d, time)
                        : sinceYank >= 0f && sinceYank < 0.3f ? HurtFrame(d) : IdleFrame(d, time);
            if (dashing) Ghosts(d, body, feet, new Vector2(-36f, 0f), 3);
            Runner(d, body, feet + Shake(sinceYank, 0.3f, 6f), false, dashing ? 7f : 0f);

            // Its key: over the player's head, marked while it covets, yanked away into the pod, home again when it breaks.
            float keyPress = Tap(t, dashAt);
            if (t < yankAt)
            {
                if (covet > 0f) CardGui.Ring(keyHome, 42f + 5f * Mathf.Sin(t * 20f), 3f, WithAlpha(DashColor, covet));
                FloatKey(abilityKey, keyHome, KeySize, keyPress, d.accent);
            }
            else if (t < seedEnd)
            {
                float f = (t - yankAt) / seedTime;
                var at = Vector2.Lerp(keyHome, podCenter, f) + new Vector2(0f, -Mathf.Sin(f * Mathf.PI) * 90f);
                DemoBosses.Seed(at, unit, DashColor, f * 720f);
                FloatKey(abilityKey, at, KeySize * Mathf.Lerp(1f, 0.5f, f), 0f, DashColor, 1f - 0.4f * f);
            }
            if (t >= yankAt && t < backAt) TakenKey(abilityKey, keyHome, t - yankAt);
            if (podAlive && grown > 0.4f) FloatKey(abilityKey, podCenter + new Vector2(0f, -unit * 0.95f), KeySize * 0.5f, 0f, DashColor, Seg(t, seedEnd, seedEnd + sproutTime));
            if (t >= breakAt && t < backAt)
            {
                float f = (t - breakAt) / keyBackTime;
                var at = Vector2.Lerp(podCenter, keyHome, Smooth(f)) + new Vector2(0f, -Mathf.Sin(f * Mathf.PI) * 70f);
                FloatKey(abilityKey, at, KeySize * Mathf.Lerp(0.5f, 1f, f), 0f, DashColor);
            }
            else if (t >= backAt)
            {
                FloatKey(abilityKey, keyHome, KeySize, 0f, d.accent);
                float k = Seg(t, backAt, backAt + 0.45f);
                if (k > 0f && k < 1f) CardGui.Ring(keyHome, 30f + 46f * k, 4f * (1f - k) + 1f, WithAlpha(d.accent, 1f - k));
            }

            // The attack key appears beside it once it's time to break things.
            float attackPress = 0f;
            foreach (var p in podPresses) attackPress = Mathf.Max(attackPress, Tap(t, p));
            foreach (var p in bossPresses) attackPress = Mathf.Max(attackPress, Tap(t, p));
            float appear = Seg(t, toPodAt - 0.3f, toPodAt);
            if (appear > 0f)
                FloatKey(attackKey, keyHome + new Vector2(CardGui.KeyWidth(abilityKey, KeySize) * 0.5f + 46f, 0f), KeySize, attackPress, d.accent, appear);
            Press[abilityKey] = keyPress;
            Press[attackKey] = attackPress;
            CardGui.Alpha = alpha;
        }

        /// <summary>The slot an ability was taken from: an empty key, struck through, flashing red at first.</summary>
        static void TakenKey(string key, Vector2 center, float since)
        {
            float w = CardGui.KeyWidth(key, KeySize);
            var r = new Rect(center.x - w * 0.5f, center.y - KeySize * 0.5f, w, KeySize);
            float red = Mathf.Clamp01(1f - since / 0.5f);
            float a = CardGui.Alpha;
            CardGui.Alpha *= 0.4f;
            CardGui.Key(r, key, 0f, new Color(0.5f, 0.5f, 0.55f));
            CardGui.Alpha = a;
            var hot = Color.Lerp(new Color(0.55f, 0.15f, 0.15f), new Color(1f, 0.25f, 0.25f), red);
            if (red > 0f) CardGui.Glow(r.center, KeySize * 1.2f, WithAlpha(hot, 0.5f * red));
            CardGui.Line(new Vector2(r.x - 4f, r.yMax + 2f), new Vector2(r.xMax + 4f, r.y - 2f), 4f, hot);
        }

        // ---------- Blue: climb the shaft while it floods; a tear knocks you down; catch it at the top ----------

        static void Climb(InstructionData d, Rect stage, float time)
        {
            const float period = 8f, step = 100f, unit = 44f, arc = 70f;
            const float warnAt = 0.9f, lockAt = 1.55f, throwAt = 1.7f, hitAt = 2.15f, knockEnd = 2.65f;
            const float summitAt = 4.3f, hopAt = 4.45f, hopTime = 0.6f, runAt = 4.6f, runEnd = 5.05f, leapAt = 5.2f, catchAt = 5.45f, leapEnd = 5.8f;
            const float fleeAt = 5.7f, fleeTime = 0.6f;
            float t = time % period;
            float baseY = stage.yMax - 66f; // the screen line the camera keeps the player's ledge on
            string jumpKey = KeyAt(d, 0, "Space");

            float sx0 = stage.x + stage.width * 0.3f, sx1 = stage.x + stage.width * 0.7f, mid = (sx0 + sx1) * 0.5f;
            // World: x as on screen, y up from the first ledge. Ledge 4 is the summit floor across the shaft.
            var ledges = new[]
            {
                new Vector2(sx0 + 80f, 0f), new Vector2(mid + 30f, step), new Vector2(sx1 - 85f, 2f * step),
                new Vector2(mid - 70f, 3f * step), new Vector2(mid - 60f, 4f * step),
            };
            var perchA = new Vector2(sx1 - 64f, 4f * step + 92f);
            var perchB = new Vector2(sx0 + 70f, 4f * step + 120f);
            float[] jumpStarts = { 0.25f, 1.05f, 1.85f, 3.0f, 3.75f, leapAt };

            Vector2 JumpPos(float tt, float a, float b, Vector2 from, Vector2 to)
            {
                float k = Seg(tt, a, b);
                return new Vector2(Mathf.Lerp(from.x, to.x, k), Mathf.Lerp(from.y, to.y, k) + arc * 4f * k * (1f - k));
            }

            // Where the player is (world), and what it's doing.
            Vector2 PlayerAt(float tt)
            {
                if (tt < 0.25f) return ledges[0];
                if (tt < 1.05f) return JumpPos(tt, 0.25f, 0.75f, ledges[0], ledges[1]);
                if (tt < 1.85f) return JumpPos(tt, 1.05f, 1.55f, ledges[1], ledges[2]);
                if (tt < hitAt) return JumpPos(tt, 1.85f, 2.35f, ledges[2], ledges[3]);
                if (tt < 3.0f)
                {
                    // Knocked down and away (the way the tear flew) back onto the ledge below.
                    var hit = JumpPos(hitAt, 1.85f, 2.35f, ledges[2], ledges[3]);
                    float k = Seg(tt, hitAt, knockEnd);
                    return new Vector2(Mathf.Lerp(hit.x, ledges[2].x, Smooth(k)), Mathf.Lerp(hit.y, ledges[2].y, k * k) + 30f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                }
                if (tt < 3.75f) return JumpPos(tt, 3.0f, 3.5f, ledges[2], ledges[3]);
                if (tt < runAt) return JumpPos(tt, 3.75f, summitAt, ledges[3], ledges[4]);
                float runTo = perchA.x - 92f;
                if (tt < leapAt) return new Vector2(Mathf.Lerp(ledges[4].x, runTo, Seg(tt, runAt, runEnd)), ledges[4].y);
                float l = Seg(tt, leapAt, leapEnd);
                return new Vector2(Mathf.Lerp(runTo, runTo + 60f, l), ledges[4].y + 110f * 4f * l * (1f - l));
            }

            float Cam(float tt)
            {
                float c = 0f;
                c = Mathf.Lerp(c, step, Smooth(Seg(tt, 0.25f, 1.0f)));
                c = Mathf.Lerp(c, 2f * step, Smooth(Seg(tt, 1.05f, 1.8f)));
                c = Mathf.Lerp(c, 3f * step, Smooth(Seg(tt, 3.0f, 3.75f)));
                c = Mathf.Lerp(c, 4f * step, Smooth(Seg(tt, 3.75f, 4.55f)));
                return Mathf.Lerp(c, 4f * step + 40f, Smooth(Seg(tt, 4.4f, 5.0f)));
            }

            float cam = Cam(t);
            Vector2 ToScreen(Vector2 w) => new(w.x, baseY - (w.y - cam));
            Vector2 ToWorld(Vector2 s) => new(s.x, cam - (s.y - baseY));

            float alpha = CardGui.Alpha;
            CardGui.Alpha *= LoopFade(t, period);

            // The shaft: rock walls with tile seams that slide down as you climb.
            CardGui.Box(new Rect(stage.x, stage.y, sx0 - 14f - stage.x, stage.height), Rock);
            CardGui.Box(new Rect(sx1 + 14f, stage.y, stage.xMax - sx1 - 14f, stage.height), Rock);
            CardGui.Box(new Rect(sx0 - 18f, stage.y, 4f, stage.height), WithAlpha(Ink, 0.5f));
            CardGui.Box(new Rect(sx1 + 14f, stage.y, 4f, stage.height), WithAlpha(Ink, 0.5f));
            for (float wy = Mathf.Floor((cam - 120f) / 46f) * 46f; ToScreen(new Vector2(0f, wy)).y > stage.y; wy += 46f)
            {
                float y = ToScreen(new Vector2(0f, wy)).y;
                if (y > stage.yMax) continue;
                CardGui.Box(new Rect(stage.x, y, sx0 - 18f - stage.x, 2f), WithAlpha(Ink, 0.12f));
                CardGui.Box(new Rect(sx1 + 18f, y, stage.xMax - sx1 - 18f, 2f), WithAlpha(Ink, 0.12f));
            }

            // Ledges (one-way, like the level's), the summit floor and its perches.
            for (int i = 0; i < ledges.Length; i++)
            {
                var s = ToScreen(ledges[i]);
                if (s.y < stage.y - 20f || s.y > stage.yMax + 20f) continue;
                bool summit = i == ledges.Length - 1;
                var r = summit ? new Rect(sx0 - 14f, s.y, sx1 - sx0 + 28f, 18f) : new Rect(s.x - 75f, s.y, 150f, 16f);
                CardGui.Round(r, Color.Lerp(Rock, Ink, 0.3f), 3f);
                CardGui.Box(new Rect(r.x, r.y, r.width, 3f), WithAlpha(Color.white, 0.35f));
            }
            float caughtShrink = Seg(t, fleeAt, fleeAt + fleeTime);
            foreach (var perch in new[] { perchA, perchB })
            {
                var s = ToScreen(perch);
                if (s.y < stage.y - 20f || s.y > stage.yMax) continue;
                float w = Mathf.Lerp(96f, 76f, caughtShrink); // they shrink after each catch
                CardGui.Round(new Rect(s.x - w * 0.5f, s.y, w, 12f), Color.Lerp(DemoBosses.BlueBody, Rock, 0.45f), 3f);
            }

            // The figure: in the top-left corner while you climb (always out of reach), then on a perch at the summit.
            var corner = new Vector2(sx0 + 44f, stage.y + 64f);
            var seatA = perchA + new Vector2(0f, 0.9f * unit);
            var seatB = perchB + new Vector2(0f, 0.9f * unit);
            Vector2 bossWorld;
            if (t < hopAt) bossWorld = ToWorld(corner);
            else if (t < fleeAt)
            {
                float k = Seg(t, hopAt, hopAt + hopTime);
                bossWorld = Vector2.Lerp(new Vector2(corner.x, Cam(hopAt) - (corner.y - baseY)), seatA, Smooth(k)) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 60f);
            }
            else
            {
                float k = Seg(t, fleeAt, fleeAt + fleeTime);
                bossWorld = Vector2.Lerp(seatA, seatB, Smooth(k)) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 70f);
            }
            var bossScreen = ToScreen(bossWorld);
            var player = PlayerAt(t);
            var playerScreen = ToScreen(player);
            int toPlayer = playerScreen.x >= bossScreen.x ? 1 : -1;
            int facing = t < hopAt ? toPlayer
                       : t < hopAt + hopTime ? 1
                       : t < fleeAt ? -toPlayer // perched: it turns its back on you
                       : t < fleeAt + fleeTime ? -1 : -toPlayer;
            float lean = Mathf.Clamp01(1f - Mathf.Abs(t - throwAt) / 0.25f) * (t >= throwAt - 0.05f ? 1f : 0f);
            float sinceCatch = t - catchAt;
            DemoBosses.Weeper(bossScreen, unit, facing, time, true, 0f, lean, sinceCatch >= 0f && sinceCatch < 0.4f ? 1f - sinceCatch / 0.4f : 0f,
                              false, sinceCatch >= 0f && sinceCatch < 0.1f ? 1f : 0f);
            if (t >= catchAt) // catches so far: one of three
                for (int i = 0; i < 3; i++)
                {
                    var pip = bossScreen + new Vector2((i - 1) * 22f, -1.25f * unit);
                    CardGui.Diamond(pip, 13f, WithAlpha(Ink, 0.7f));
                    CardGui.Diamond(pip, 9f, i == 0 ? d.accent : new Color(0.85f, 0.87f, 0.92f));
                }
            Burst(bossScreen, Seg(t, catchAt, catchAt + 0.5f), DemoBosses.TearColor, 110f);
            Spark(bossScreen, Seg(t, catchAt, catchAt + 0.35f), d.accent, 70f);

            // The tear: forms beside it while a dotted arc and a landing glow follow you, locks (white flash), is thrown.
            Vector2 TearOrigin(float tt) // beside it, on the side facing the player (screen: it hangs in the view's corner)
            {
                var to = (ToScreen(PlayerAt(tt)) + new Vector2(0f, -50f) - corner).normalized;
                return corner + to * 1.1f * unit;
            }
            var target = PlayerAt(hitAt) + new Vector2(0f, 50f);
            if (t >= warnAt && t < throwAt)
            {
                var o = TearOrigin(t);
                var aim = t < lockAt ? ToScreen(PlayerAt(t) + new Vector2(0f, 50f)) : ToScreen(target);
                bool locked = t >= lockAt;
                float form = Seg(t, warnAt, lockAt);
                var ctrl = (o + aim) * 0.5f + new Vector2(0f, -110f);
                var dot = locked ? Color.white : WithAlpha(DemoBosses.TearColor, 0.85f);
                for (int i = 1; i < 10; i++)
                {
                    float k = i / 10f;
                    CardGui.Disc(Bezier(o, ctrl, aim, k), 3f, WithAlpha(dot, (locked ? 1f : 0.5f + 0.5f * form) * (1f - k * 0.4f)));
                }
                CardGui.GlowRect(new Rect(aim.x - 34f, aim.y - 12f, 68f, 24f), WithAlpha(locked ? Color.white : DemoBosses.TearColor, 0.6f + 0.3f * Mathf.Sin(t * 30f)));
                DemoBosses.Tear(o, Vector2.down, unit, 1.6f * Mathf.Max(0.2f, form));
            }
            else if (t >= throwAt && t < hitAt)
            {
                var o = TearOrigin(throwAt);
                var aim = ToScreen(target);
                var ctrl = (o + aim) * 0.5f + new Vector2(0f, -110f);
                float k = Seg(t, throwAt, hitAt);
                var at = Bezier(o, ctrl, aim, k);
                var vel = Bezier(o, ctrl, aim, Mathf.Min(1f, k + 0.05f)) - at;
                DemoBosses.Tear(at, vel * 60f, unit, 1.6f);
            }
            Burst(ToScreen(target), Seg(t, hitAt, hitAt + 0.4f), DemoBosses.TearColor, 70f);

            // The player.
            Sprite body;
            float lastJump = Last(t, jumpStarts);
            bool knocked = t >= hitAt && t < knockEnd;
            bool airborne = (t >= 0.25f && t < 0.75f) || (t >= 1.05f && t < 1.55f) || (t >= 1.85f && t < hitAt)
                            || (t >= 3.0f && t < 3.5f) || (t >= 3.75f && t < summitAt) || (t >= leapAt && t < leapEnd);
            if (knocked) body = HurtFrame(d);
            else if (airborne) body = JumpFrame(d, t - lastJump);
            else if (t >= runAt && t < runEnd) body = RunFrame(d, time);
            else body = IdleFrame(d, time);
            float vx = PlayerAt(t + 0.02f).x - player.x;
            Runner(d, body, playerScreen, knocked ? false : vx < -0.5f);

            // The water: there from the start, rising after you, stopping just under the summit floor.
            float water = Mathf.Min(-40f + 85f * t, 4f * step - 18f);
            DemoBosses.Water(stage, ToScreen(new Vector2(0f, water)).y, time, unit);

            float press = 0f;
            foreach (var j in jumpStarts) press = Mathf.Max(press, Tap(t, j));
            Press[jumpKey] = press;
            FloatKey(jumpKey, new Vector2(stage.xMax - 110f, stage.y + 54f), KeySize, press, d.accent);
            CardGui.Alpha = alpha;
        }

        // ---------- Indigo: each curse scrambles the controls, and the screen tells you which ----------

        static readonly string[] CurseNames = { "MIRROR", "SWAP", "ECHO", "INVERSION" };
        // IndigoBoss.HintFor, word for word: what the sigil spells out under the name.
        static readonly string[] CurseHints = { "LEFT <-> RIGHT", "JUMP <-> ATTACK    SHOOT <-> DASH", "YOUR BODY LAGS BEHIND", "UPSIDE DOWN    JUMP <-> ATTACK" };
        static readonly Color[] CurseColors = { new(0.55f, 0.45f, 1f), new(1f, 0.35f, 0.75f), new(0.3f, 0.9f, 1f), new(1f, 0.85f, 0.4f) };
        // Slot pairs that trade keys (slots: left, right, jump, attack, shoot, dash). INVERSION mirrors too, but upside down
        // left and right feel right again, so on screen only jump and attack trade.
        static readonly int[][] CurseSwaps = { new[] { 0, 1 }, new[] { 2, 3, 4, 5 }, new int[0], new[] { 2, 3 } };
        static readonly string[] SlotLabels = { "LEFT", "RIGHT", "JUMP", "ATTACK", "SHOOT", "DASH" };
        static readonly string[] SlotDefaults = { "Left", "Right", "Space", "LMB", "RMB", "Shift" };
        static readonly int[] CursePressedKey = { 1, 2, 1, 2 }; // the key the player presses to show it: Right, Space, Right, Space

        static void FlipControls(InstructionData d, Rect stage, float ground, float time)
        {
            const float cycle = 3.4f, unit = 38f, castFrom = 0.35f, castTime = 1.4f, pressAt = 2.55f, slotWidth = 84f, keyHeight = 44f;
            float releaseAt = castFrom + castTime;
            int curse = (int)(time / cycle) % CurseNames.Length;
            int prev = (curse + CurseNames.Length - 1) % CurseNames.Length;
            float u = time % cycle;
            bool first = time < cycle;
            float lift = first ? 0f : 1f - Smooth(Seg(u, 0f, 0.25f));
            float land = Smooth(Seg(u, releaseAt + 0.05f, releaseAt + 0.65f));
            var accent = CurseColors[curse];

            // How strongly each curse's look is on: the new one landing, the old one lifting.
            float Look(int c) => c == curse ? land : c == prev ? lift : 0f;
            float roll = 180f * Look(3);

            var hover = new Vector2(stage.x + stage.width * 0.76f, ground - 3.7f * unit);
            var castSpot = new Vector2(hover.x, stage.y + 112f);
            var seer = Vector2.Lerp(hover, castSpot, Smooth(Seg(u, 0f, 0.5f)));
            seer = Vector2.Lerp(seer, hover, Smooth(Seg(u, releaseAt + 0.25f, releaseAt + 0.85f)));
            float eyeOpen = Mathf.Clamp01(1f - Seg(u, 0f, 0.15f) + Seg(u, releaseAt + 0.3f, releaseAt + 0.5f));

            // The player: presses one key once the curse is on, and does the wrong thing.
            var home = new Vector2(stage.x + stage.width * 0.28f, ground);
            float StepOffset(int c, float uu) => c switch
            {
                0 => -80f * Smooth(Seg(uu, pressAt + 0.05f, pressAt + 0.45f)),        // pressed right, went left
                2 => 80f * Smooth(Seg(uu, pressAt + 0.25f, pressAt + 0.65f)),         // went right, 0.2 s late
                _ => 0f,
            };
            float offset = StepOffset(curse, u) + (first ? 0f : StepOffset(prev, cycle) * lift);
            var feet = home + new Vector2(offset, 0f);
            bool attacking = (curse == 1 || curse == 3) && u >= pressAt + 0.05f;
            bool walking = (curse == 0 && u >= pressAt + 0.05f && u < pressAt + 0.45f) || (curse == 2 && u >= pressAt + 0.25f && u < pressAt + 0.65f);
            var attack = attacking ? AttackFrame(d, u - pressAt - 0.05f) : null;
            Sprite body = attack ? attack : walking ? RunFrame(d, time) : IdleFrame(d, time);

            // ---- The scene (rolled upside down by INVERSION) ----
            var look = (Vector2)(feet + new Vector2(0f, -60f) - seer);
            look = new Vector2(look.x, -look.y).normalized;
            var sway = Look(0) * Mathf.Sin(time * 1.4f) * 2.5f + Look(2) * Mathf.Sin(time * 1.1f) * 2f;
            var jitter = Look(1) > 0f ? new Vector2(DemoBosses.Hash(Mathf.Floor(time * 20f)) - 0.5f, 0f) * 4f * Look(1) : Vector2.zero;
            var prevMatrix = CardGui.Rotate(stage.center, roll + sway);
            GUI.matrix = GUI.matrix * Matrix4x4.Translate(jitter);

            Floor(stage, ground);
            float near = Mathf.Clamp01(1f - (ground - seer.y) / (12f * unit));
            DemoBosses.LightPool(new Vector2(seer.x, ground + 2f), unit, accent, near);

            // MIRROR: a ghost of the world, mirrored. ECHO: the body leaves trails.
            if (Look(0) > 0.01f)
            {
                float a = CardGui.Alpha;
                CardGui.Alpha *= 0.28f * Look(0);
                Runner(d, body, new Vector2(stage.center.x * 2f - feet.x, feet.y), !walking);
                DemoBosses.Seer(new Vector2(stage.center.x * 2f - seer.x, seer.y), unit, accent, time, eyeOpen, new Vector2(-look.x, look.y));
                CardGui.Alpha = a;
            }
            if (Look(2) > 0.01f)
                for (int i = 3; i >= 1; i--)
                {
                    float a = CardGui.Alpha;
                    CardGui.Alpha *= 0.22f * Look(2) * (1f - i * 0.2f);
                    Runner(d, body, home + new Vector2(StepOffset(2, u - i * 0.07f), 0f), false);
                    CardGui.Alpha = a;
                }

            float charge = u >= castFrom && u < releaseAt ? Seg(u, castFrom, releaseAt) : 0f;
            float sinceRelease = u - releaseAt;
            DemoBosses.Seer(seer, unit, accent, time, eyeOpen, look, charge, sinceRelease >= 0f && sinceRelease < 0.4f ? 1f - sinceRelease / 0.4f : 0f);
            if (u >= castFrom && sinceRelease < 0.5f)
                DemoBosses.Sigil(castSpot, unit, accent, u - castFrom, castTime, sinceRelease >= 0f ? sinceRelease : -1f, 0.5f);
            Runner(d, body, feet, curse == 0 && walking);
            Spark(feet + new Vector2(60f, -70f), attacking ? Seg(u, pressAt + 0.12f, pressAt + 0.4f) : 0f, accent, 40f);
            float wave = Seg(u, releaseAt, releaseAt + 0.6f);
            if (wave > 0f && wave < 1f) CardGui.Ring(castSpot, 40f + 190f * wave, 7f * (1f - wave) + 1f, WithAlpha(accent, 1f - wave));
            GUI.matrix = prevMatrix;

            // ---- The curse's look, over the whole stage ----
            DrawCurseLook(stage, 0, Look(0), time);
            DrawCurseLook(stage, 1, Look(1), time);
            DrawCurseLook(stage, 2, Look(2), time);
            DrawCurseLook(stage, 3, Look(3), time);
            float flash = 1f - Seg(u, releaseAt, releaseAt + 0.35f);
            if (u >= releaseAt && flash > 0f) CardGui.Box(stage, WithAlpha(Color.Lerp(accent, Color.white, 0.5f), 0.55f * flash));

            // The sigil's words: the curse's name and what it does, on a dark plate under it.
            float words = Smooth(Seg(u, castFrom + 0.1f, castFrom + 0.35f)) * (1f - Seg(u, releaseAt + 0.05f, releaseAt + 0.3f));
            if (words > 0f)
            {
                float a = CardGui.Alpha;
                CardGui.Alpha *= words;
                var plate = new Rect(castSpot.x - 190f, castSpot.y + 3.6f * unit - 30f, 380f, 74f);
                plate.x = Mathf.Min(plate.x, stage.xMax - plate.width - 8f);
                CardGui.Box(plate, new Color(0f, 0f, 0f, 0.6f));
                CardGui.Text(new Rect(plate.x, plate.y + 2f, plate.width, 42f), Spaced(CurseNames[curse]), 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                CardGui.Text(new Rect(plate.x, plate.y + 42f, plate.width, 26f), CurseHints[curse], 15, WithAlpha(Color.white, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
                CardGui.Alpha = a;
            }

            // ---- The controls: six slots with fixed actions; the curse trades the keys around ----
            var keys = new string[SlotDefaults.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = KeyAt(d, i, SlotDefaults[i]);
            var panel = new Rect(stage.x + 18f, stage.y + 14f, slotWidth * keys.Length + 16f, 104f);
            CardGui.Round(panel, new Color(0f, 0f, 0f, 0.06f), 8f);
            CardGui.Outline(panel, WithAlpha(Ink, 0.15f), 1.5f, 8f);
            Vector2 Slot(int i) => new(panel.x + 8f + slotWidth * (i + 0.5f), panel.y + 66f);
            for (int i = 0; i < SlotLabels.Length; i++)
                CardGui.Text(new Rect(Slot(i).x - slotWidth * 0.5f, panel.y + 6f, slotWidth, 22f), SlotLabels[i], 15, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);

            int pressed = CursePressedKey[curse];
            float pressAmount = Tap(u, pressAt, 0.25f);
            var ripple = Seg(u, releaseAt + 0.1f, releaseAt + 0.8f);
            if (CurseSwaps[curse].Length > 0 && ripple > 0f && ripple < 1f) CardGui.Ring(panel.center, 40f + 60f * ripple, 3f, WithAlpha(accent, 1f - ripple));
            for (int k = 0; k < keys.Length; k++)
            {
                var at = Slot(k);
                at = Traded(k, curse, Smooth(Seg(u, releaseAt + 0.1f, releaseAt + 0.6f)), at, Slot);
                if (!first) at = Traded(k, prev, lift, at, Slot);
                float w = CardGui.KeyWidth(keys[k], keyHeight);
                CardGui.Key(new Rect(at.x - w * 0.5f, at.y - keyHeight * 0.5f, w, keyHeight), keys[k], k == pressed ? pressAmount : 0f, accent);
                Press[keys[k]] = k == pressed ? pressAmount : 0f;
            }

            // ECHO: the key goes down now, the body follows 0.2 s later.
            if (curse == 2 && u >= pressAt && u < pressAt + 0.9f)
            {
                var at = Slot(pressed) + new Vector2(0f, 38f);
                CardGui.Text(new Rect(at.x - 50f, at.y - 2f, 100f, 22f), "+0.2s", 16, Color.Lerp(accent, Ink, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        /// <summary>Where key `k` sits while curse `c` trades slots by `amount` (0 home, 1 traded), hopping over its partner.</summary>
        static Vector2 Traded(int k, int c, float amount, Vector2 at, System.Func<int, Vector2> slot)
        {
            var pairs = CurseSwaps[c];
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                int a = pairs[i], b = pairs[i + 1];
                if (k != a && k != b) continue;
                int other = k == a ? b : a;
                var p = Vector2.Lerp(slot(k), slot(other), amount);
                return p + new Vector2(0f, (k == a ? -1f : 1f) * Mathf.Sin(amount * Mathf.PI) * 30f);
            }
            return at;
        }

        /// <summary>A stand-in for ScreenWarp's look of each curse, laid over the stage (amount 0..1).</summary>
        static void DrawCurseLook(Rect stage, int curse, float amount, float time)
        {
            if (amount <= 0.01f) return;
            switch (curse)
            {
                case 0: // MIRROR: violet tint, deep vignette (the ghost is drawn in the scene)
                    CardGui.Box(stage, new Color(0.45f, 0.35f, 1f, 0.2f * amount));
                    Vignette(stage, new Color(0.12f, 0.05f, 0.3f, 0.55f * amount));
                    break;
                case 1: // SWAP: hues turned over, glitching slices
                    CardGui.Box(stage, new Color(1f, 0.35f, 0.75f, 0.2f * amount));
                    for (int i = 0; i < 5; i++)
                    {
                        float seed = Mathf.Floor(time * 7f) * 5.3f + i * 1.9f;
                        if (DemoBosses.Hash(seed + 0.5f) < 0.35f) continue;
                        float y = stage.y + stage.height * DemoBosses.Hash(seed);
                        float h = 5f + 16f * DemoBosses.Hash(seed + 1.1f);
                        float shift = (DemoBosses.Hash(seed + 2.2f) - 0.5f) * 40f;
                        CardGui.Box(new Rect(stage.x + Mathf.Max(0f, shift), y, stage.width - Mathf.Abs(shift), h),
                                    i % 2 == 0 ? new Color(0.2f, 1f, 1f, 0.22f * amount) : new Color(1f, 0.2f, 0.6f, 0.22f * amount));
                    }
                    Vignette(stage, new Color(0.3f, 0f, 0.25f, 0.5f * amount));
                    break;
                case 2: // ECHO: washed out, cyan (the trails are in the scene)
                    CardGui.Box(stage, new Color(0.6f, 0.62f, 0.66f, 0.18f * amount));
                    CardGui.Box(stage, new Color(0.2f, 0.7f, 1f, 0.18f * amount));
                    Vignette(stage, new Color(0f, 0.08f, 0.2f, 0.6f * amount));
                    break;
                case 3: // INVERSION: upside down (in the scene), hues cycling
                    var hue = Color.HSVToRGB(Mathf.Repeat(0.12f + time * 0.15f, 1f), 0.55f, 1f);
                    CardGui.Box(stage, new Color(hue.r, hue.g, hue.b, 0.2f * amount));
                    Vignette(stage, new Color(0.1f, 0f, 0.2f, 0.65f * amount));
                    break;
            }
        }

        static void Vignette(Rect r, Color c)
        {
            for (int i = 0; i < 6; i++)
            {
                float inset = i * 9f;
                CardGui.Outline(new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f), WithAlpha(c, c.a * (1f - i / 6f) * 0.35f), 9f);
            }
        }

        // ---------- Violet: the run needs every ability, then the king ----------

        // Where the player is along the course: (time, x) points, straight lines between (equal x = standing still).
        static readonly float[] RunTimes = { 0f, 0.92f, 1.14f, 3.45f, 4.2f, 4.9f, 6.75f, 7.6f, 8.55f, 9.42f, 9.55f, 10.95f, 11.5f, 12.3f };
        static readonly float[] RunXs = { 0f, 276f, 430f, 1123f, 1573f, 1783f, 1783f, 2038f, 2038f, 2300f, 2300f, 2630f, 2795f, 2795f };
        static readonly string[] GauntletLabels = { "DASH", "DOUBLE JUMP", "SURF", "SHOOT", "BLAZE", "SERENITY" };
        static readonly string[] GauntletDefaults = { "Shift", "Space", "Down", "RMB", "LMB", "Q" };

        static void Gauntlet(InstructionData d, Rect stage, float ground, float time)
        {
            const float period = 12.3f, kingFrom = 0.4f;
            // When each ability is used (it lights up in the row from then on).
            const float dashAt = 0.92f, jumpAt = 1.87f, doubleAt = 2.12f, surfAt = 3.45f, surfEnd = 4.2f, shootAt = 5.0f;
            // Blaze as in the game: the press swings at once, the heat shows 0.2s in, READY at 0.6s, then the release.
            const float blazePressAt = 7.6f, blazeChargeAt = 7.8f, blazeReadyAt = 8.2f, strikeAt = 8.35f;
            const float serenityAt = 9.45f, serenityEnd = 10.95f, pointAt = 11.4f;
            float[] usedAt = { dashAt, doubleAt, surfAt, shootAt, strikeAt, serenityAt };
            float t = time % period;
            float x = Interpolate(RunTimes, RunXs, t);
            float screenX0 = stage.x + stage.width * 0.2f;
            float S(float worldX) => screenX0 + worldX - x;
            var keys = new string[GauntletDefaults.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = KeyAt(d, i, GauntletDefaults[i]);

            float alpha = CardGui.Alpha;
            CardGui.Alpha *= LoopFade(t, period);
            bool slow = t >= serenityAt && t < serenityEnd;

            // ---- Far away: the king on his hill (he comes closer as you run, then you arrive) ----
            float progress = x / RunXs[RunXs.Length - 1];
            float arrive = Smooth(Seg(t, serenityEnd, 11.5f));
            float hillX = Mathf.Lerp(stage.x + stage.width * 0.88f, stage.x + stage.width * 0.66f, arrive);
            float kingUnit = Mathf.Lerp(Mathf.Lerp(26f, 34f, progress), 44f, arrive);
            float hillH = Mathf.Lerp(70f, 110f, Mathf.Max(progress, arrive));
            var hillColor = Color.Lerp(new Color(0.72f, 0.68f, 0.78f), Stone, Mathf.Max(progress, arrive));
            for (int i = 0; i < 3; i++) // stepped, painted like the level's tiles
            {
                float w = 220f - i * 55f, h = hillH * (i + 1) / 3f;
                CardGui.Box(new Rect(hillX - w * 0.5f, ground - h, w, h), hillColor);
                CardGui.Box(new Rect(hillX - w * 0.5f, ground - h, w, 3f), WithAlpha(Color.white, 0.3f));
            }
            var kingFeet = new Vector2(hillX, ground - hillH);
            var pose = KingAt(t);
            float glint = Mathf.Clamp01(1f - Mathf.Abs(t - pointAt) / 0.35f);
            var (tip, hand) = DemoBosses.King(kingFeet, kingUnit, -1, pose, time, 0f, glint);
            FarFlash(tip, Seg(t, 0.3f, 0.65f));
            FarFlash(hand, Seg(t, 2.9f, 3.25f));
            FarFlash(hand, Seg(t, 4.8f, 5.15f));
            FarFlash(hand, Seg(t, 9.0f, 9.35f));
            FarFlash(tip, Seg(t, pointAt, pointAt + 0.4f));

            // ---- The course ----
            Floor(stage, ground, x);

            // 1. Crescent wave rolling in at floor level: dash through it.
            if (t >= kingFrom && t < 1.6f)
            {
                float wx = 353f + (1.03f - t) * 520f;
                var c = new Vector2(S(wx), ground - 72f);
                CardGui.Sprite(VioletShapes.Crescent, c, new Vector2(130f, 150f), WithAlpha(VioletShapes.Glow, 0.4f), false, 0f);
                CardGui.Sprite(VioletShapes.Crescent, c, new Vector2(110f, 140f), VioletShapes.Bright, false, 0f);
            }

            // 2. A step four tiles high: only a double jump makes it.
            Terrain(new Rect(S(780f), ground - 150f, 180f, 150f), stage);

            // 3. A low beam: only a Down Dash surf fits under it.
            float beamL = S(1160f), beamR = S(1560f), beamTop = ground - 160f, beamBottom = ground - 64f;
            if (t >= 2.95f && t < 4.4f && beamR > stage.x && beamL < stage.xMax)
            {
                var band = new Rect(Mathf.Max(stage.x, beamL), beamTop, Mathf.Min(stage.xMax, beamR) - Mathf.Max(stage.x, beamL), beamBottom - beamTop);
                if (t < 3.35f)
                {
                    float blink = 0.5f + 0.5f * Mathf.Sin(t * 28f);
                    CardGui.Box(band, WithAlpha(VioletShapes.Glow, 0.12f + 0.12f * blink));
                    CardGui.Box(new Rect(band.x, beamBottom - 2f, band.width, 3f), WithAlpha(VioletShapes.Bright, 0.9f));
                }
                else
                {
                    float burn = 1f - Seg(t, 4.2f, 4.4f);
                    CardGui.Box(new Rect(band.x, band.y - 6f, band.width, band.height + 12f), WithAlpha(VioletShapes.Bright, 0.5f * burn));
                    CardGui.Box(band, WithAlpha(VioletShapes.Glow, 0.92f * burn));
                    CardGui.Box(new Rect(band.x, band.center.y - 6f, band.width, 12f), WithAlpha(Color.white, (0.6f + 0.3f * Mathf.Sin(t * 50f)) * burn));
                }
            }

            // 4. Royal Rain: shoot the anchor, the slab drops onto its pillars, hide under it while the swords fall.
            RainHall(t, ground, stage, S, shootAt);

            // 5. The Crystal Gate: only a fully charged Blaze Strike shatters it.
            float crystalX = S(2120f);
            if (t < strikeAt + 0.05f && EdgeFade(crystalX, stage, 42f) > 0f)
                CardGui.Sprite(VioletShapes.Crystal, new Vector2(crystalX, ground), new Vector2(84f, 220f),
                               WithAlpha(VioletShapes.Bright, EdgeFade(crystalX, stage, 42f)), false, 0f);
            Shatter(new Vector2(crystalX, ground - 100f), Seg(t, strikeAt + 0.05f, strikeAt + 0.75f), VioletShapes.Bright, 12, 140f);

            // 6. The needle curtain: follow the gap; Serenity slows the needles down enough to read it.
            float needleClock = t < serenityAt ? t
                              : t < serenityEnd ? serenityAt + (t - serenityAt) * 0.35f
                              : serenityAt + (serenityEnd - serenityAt) * 0.35f + (t - serenityEnd);
            Curtain(needleClock, ground, stage, S, x);

            // ---- The player ----
            float y = 0f; // height above the floor (jump over the step)
            const float onStep = 2.606f, offStep = 2.907f, landed = 3.28f; // lands on the step, walks off its far end, lands
            if (t >= jumpAt && t < landed)
            {
                if (t < doubleAt) y = Parabola(t - jumpAt, 0f, 650f);
                else if (t < onStep) y = Parabola(t - doubleAt, 94f, 650f);
                else if (t < offStep) y = 150f;
                else y = Mathf.Max(0f, 150f - 1100f * (t - offStep) * (t - offStep));
            }
            var feet = new Vector2(screenX0, ground - y);
            bool dashing = t >= dashAt && t < 1.14f;
            bool surfing = t >= surfAt && t < surfEnd;
            bool standing = Mathf.Approximately(Interpolate(RunTimes, RunXs, t + 0.02f), x) && !slow;
            Sprite body;
            if (t >= strikeAt && t < strikeAt + 0.4f) body = AttackFrame(d, t - strikeAt);
            else if (t >= blazePressAt && t < blazePressAt + 0.4f) body = AttackFrame(d, t - blazePressAt); // the tap's own swing
            else if (t >= shootAt && t < shootAt + 0.3f) body = AttackFrame(d, t - shootAt + 0.1f);
            else if (t >= jumpAt && t < landed && !(t >= onStep && t < offStep)) body = JumpFrame(d, t < doubleAt ? t - jumpAt : t - doubleAt);
            else if (standing) body = IdleFrame(d, time);
            else body = RunFrame(d, slow ? time * 0.45f : time);
            if (!body) body = IdleFrame(d, time);

            if (dashing)
            {
                Ghosts(d, body, feet, new Vector2(-40f, 0f), 3);
                CardGui.Glow(feet + new Vector2(0f, -60f), 70f, WithAlpha(d.accent, 0.35f));
            }
            if (t >= doubleAt && t < doubleAt + 0.3f) // the double jump's puff
            {
                float k = (t - doubleAt) / 0.3f;
                CardGui.Ring(new Vector2(screenX0, ground - 94f), 14f + 30f * k, 3f, WithAlpha(d.accent, 1f - k));
            }
            if (surfing)
            {
                for (int i = 0; i < 6; i++) // spray and wake
                {
                    float k = Mathf.Repeat(time * 3f + i / 6f, 1f);
                    CardGui.Diamond(feet + new Vector2(-20f - 90f * k, -6f - 22f * k * (1f - k) * 4f), 10f * (1f - k), WithAlpha(SurfColor, 0.8f * (1f - k)), k * 300f);
                }
                CardGui.Box(new Rect(feet.x - 140f, ground - 3f, 140f, 3f), WithAlpha(SurfColor, 0.6f));
            }
            float charge = t >= blazeChargeAt && t < strikeAt ? Seg(t, blazeChargeAt, blazeReadyAt) : 0f;
            bool blazeReady = t >= blazeReadyAt && t < strikeAt;
            if (charge > 0f) BlazeUnderLight(feet, charge);
            Runner(d, body, feet, false, surfing ? 72f : dashing ? 7f : 0f);
            if (charge > 0f)
            {
                DemoBlade(body, feet, out var blazeHand, out var blazeTip);
                BlazeCharge(blazeHand, blazeTip, charge, blazeReady, t - blazeReadyAt, time);
            }
            float strike = Seg(t, strikeAt, strikeAt + 0.3f);
            if (strike > 0f && strike < 1f) FlameCrescent(feet, strike, time);
            BlazeHit(new Vector2(crystalX - 30f, ground - 90f), Seg(t, strikeAt + 0.05f, strikeAt + 0.3f));
            if (t >= shootAt + 0.05f && t < shootAt + 0.3f) // the Light Shot, up at the anchor
            {
                var from = feet + new Vector2(30f, -76f);
                var to = new Vector2(S(1815f), ground - 296f);
                Orb(d, Vector2.Lerp(from, to, Seg(t, shootAt + 0.05f, shootAt + 0.3f)), 9f, ShotColor);
            }

            // Serenity: the indigo wash and its rings (out when it starts, back in when it ends).
            float serene = Seg(t, serenityAt, serenityAt + 0.25f) * (1f - Seg(t, serenityEnd, serenityEnd + 0.3f));
            if (serene > 0f) CardGui.Box(stage, WithAlpha(SerenityCore, 0.14f * serene));
            for (int i = 0; i < 3; i++)
            {
                float kOut = Seg(t, serenityAt + i * 0.07f, serenityAt + i * 0.07f + 0.55f);
                if (kOut > 0f && kOut < 1f) CardGui.Ring(feet + new Vector2(0f, -55f), 20f + 160f * (1f - (1f - kOut) * (1f - kOut) * (1f - kOut)), 6f, WithAlpha(SerenityEdge, (1f - i * 0.18f) * (1f - kOut)));
                float kIn = Seg(t, serenityEnd + i * 0.05f, serenityEnd + i * 0.05f + 0.35f);
                if (kIn > 0f && kIn < 1f) CardGui.Ring(feet + new Vector2(0f, -55f), Mathf.Lerp(170f * (1f - i * 0.2f), 10f, kIn * kIn), 4f, WithAlpha(SerenityCore, 0.85f * (1f - kIn * 0.5f)));
            }

            // ---- Every ability, one after another ----
            var presses = new float[keys.Length];
            presses[0] = Tap(t, dashAt);
            presses[1] = Mathf.Max(Tap(t, jumpAt), Tap(t, doubleAt));
            presses[2] = surfing ? 1f : 0f;
            presses[3] = Tap(t, shootAt);
            presses[4] = t >= blazePressAt && t < strikeAt ? 1f : t >= strikeAt && t < strikeAt + 0.08f ? 1f - (t - strikeAt) / 0.08f : 0f;
            presses[5] = Tap(t, serenityAt);
            AbilityRow(stage, keys, presses, usedAt, t, d.accent, surfing ? Tap(t, surfAt) : 0f);
            for (int i = 0; i < keys.Length; i++) Press[keys[i]] = Mathf.Max(presses[i], Press.TryGetValue(keys[i], out var p) ? p : 0f);
            CardGui.Alpha = alpha;
        }

        /// <summary>The king's pose at `t`: throne, with a gesture before each attack (as VioletApproach starts them).</summary>
        static DemoBosses.KingPose KingAt(float t)
        {
            var throne = DemoBosses.KingPose.Throne;
            if (t < 0.3f) return DemoBosses.KingPose.Lerp(throne, DemoBosses.KingPose.SlashUp, Smooth(t / 0.25f));
            if (t < 1.1f) return DemoBosses.KingPose.Lerp(DemoBosses.KingPose.SlashDown, throne, Smooth(Seg(t, 0.65f, 1.1f)));
            if (t >= 2.6f && t < 3.6f) return Gesture(t, 2.6f, 2.9f, 3.6f, DemoBosses.KingPose.Cast);
            if (t >= 4.5f && t < 5.5f) return Gesture(t, 4.5f, 4.8f, 5.5f, DemoBosses.KingPose.Cast);
            // The needles: his hand stays raised over the curtain; then he raises the sword and points it at you.
            if (t >= 8.7f && t < 10.95f) return DemoBosses.KingPose.Lerp(throne, DemoBosses.KingPose.Cast, Smooth(Seg(t, 8.7f, 9.0f)));
            if (t >= 10.95f)
            {
                var raised = DemoBosses.KingPose.Lerp(DemoBosses.KingPose.Cast, DemoBosses.KingPose.Intro, Smooth(Seg(t, 10.95f, 11.25f)));
                return DemoBosses.KingPose.Lerp(raised, DemoBosses.KingPose.Point, Smooth(Seg(t, 11.3f, 11.45f)));
            }
            return throne;
        }

        static DemoBosses.KingPose Gesture(float t, float from, float peak, float to, DemoBosses.KingPose gesture)
        {
            var throne = DemoBosses.KingPose.Throne;
            if (t < peak) return DemoBosses.KingPose.Lerp(throne, gesture, Smooth(Seg(t, from, peak)));
            return DemoBosses.KingPose.Lerp(gesture, throne, Smooth(Seg(t, to - 0.35f, to)));
        }

        /// <summary>The burst of light as an attack leaves his blade or hand.</summary>
        static void FarFlash(Vector2 at, float k)
        {
            if (k <= 0f || k >= 1f) return;
            CardGui.Glow(at, 30f + 40f * k, WithAlpha(VioletShapes.Glow, 0.9f * (1f - k)));
            Spark(at, k, VioletShapes.Glow, 36f);
        }

        static void RainHall(float t, float ground, Rect stage, System.Func<float, float> S, float shootAt)
        {
            float hitAt = shootAt + 0.3f, landAt = hitAt + 0.25f;
            const float slabW = 270f, slabH = 26f, pillarH = 120f;
            float cx = S(1815f);
            if (cx < stage.x - 300f || cx > stage.xMax + 300f) return;

            foreach (var px in new[] { S(1700f), S(1930f) }) Terrain(new Rect(px - 14f, ground - pillarH, 28f, pillarH), stage);

            float hang = ground - 230f, rest = ground - pillarH - slabH;
            float drop = Seg(t, hitAt, landAt);
            float slabY = Mathf.Lerp(hang, rest, drop * drop);
            var anchor = new Vector2(cx, ground - 296f);
            float edge = EdgeFade(cx, stage, 20f);
            float a0 = CardGui.Alpha;
            CardGui.Alpha *= edge;
            if (t < hitAt)
            {
                CardGui.Line(anchor, new Vector2(cx, hang), 3f, WithAlpha(Ink, 0.7f));
                for (float cy = anchor.y + 8f; cy < hang; cy += 14f) CardGui.Ring(new Vector2(cx, cy), 4f, 2f, WithAlpha(Ink, 0.7f));
                CardGui.Diamond(anchor, 22f, VioletShapes.Mid);
                CardGui.Diamond(anchor, 12f, VioletShapes.Glow);
            }
            Shatter(anchor, Seg(t, hitAt, hitAt + 0.5f), VioletShapes.Mid, 6, 60f);
            Spark(anchor, Seg(t, hitAt, hitAt + 0.3f), ShotColor, 50f);
            CardGui.Alpha = a0;
            var slab = new Rect(cx - slabW * 0.5f, slabY, slabW, slabH);
            var shown = slab;
            if (ClipTo(ref shown, stage))
            {
                CardGui.Box(shown, Color.Lerp(Stone, Ink, 0.35f));
                CardGui.Box(new Rect(shown.x, shown.y, shown.width, 4f), WithAlpha(Color.white, 0.3f));
            }
            Burst(new Vector2(cx, rest + slabH), Seg(t, landAt, landAt + 0.4f), new Color(0.55f, 0.45f, 0.65f), 120f);

            // The swords gather overhead, then fall: on the slab they shatter, beside it they hit the floor.
            for (int i = 0; i < 7; i++)
            {
                float sx = cx - 230f + i * 76f;
                if (sx < stage.x + 14f || sx > stage.xMax - 14f) continue;
                float gatherA = Seg(t, shootAt - 0.1f, shootAt + 0.6f);
                float fallAt = 5.85f + i * 0.11f;
                if (t < shootAt - 0.1f || gatherA <= 0f) continue;
                bool overSlab = sx > slab.x + 10f && sx < slab.xMax - 10f;
                float stopY = overSlab ? rest : ground;
                // y(s) = start + 300 s + 1500 s^2; it lands when its tip (44 px below its middle) reaches stopY.
                float drop2 = Mathf.Max(0f, stopY - 44f - (stage.y + 26f));
                float impact = fallAt + (-300f + Mathf.Sqrt(300f * 300f + 4f * 1500f * drop2)) / 3000f;
                float s2 = Mathf.Max(0f, t - fallAt);
                float y = stage.y + 26f + 300f * s2 + 1500f * s2 * s2;
                if (t < impact)
                    CardGui.Sprite(VioletShapes.SpectralSword, new Vector2(sx, y), new Vector2(44f, 44f),
                                   WithAlpha(VioletShapes.Glow, t > fallAt ? 0.95f : 0.35f + 0.4f * gatherA), false, 0f);
                else Shatter(new Vector2(sx, stopY), Seg(t, impact, impact + 0.6f), VioletShapes.Glow, 4, 40f);
            }
        }

        static void Curtain(float clock, float ground, Rect stage, System.Func<float, float> S, float playerX)
        {
            const float from = 2330f, to = 2640f, ceiling = 176f, gapWidth = 96f;
            float l = S(from), r = S(to);
            if (r < stage.x || l > stage.xMax) return;
            float top = ground - ceiling;
            Terrain(new Rect(l - 30f, top - 22f, r - l + 60f, 22f), stage);

            // The gap: waits at the entrance, then runs ahead in bursts; the player keeps inside it.
            float gapX = S(Mathf.Clamp(playerX + 30f, from + gapWidth * 0.5f, to + 40f));
            var column = new Rect(gapX - gapWidth * 0.5f, top, gapWidth, ceiling);
            if (ClipTo(ref column, stage)) CardGui.Box(column, WithAlpha(VioletShapes.Glow, 0.18f));
            if (EdgeFade(gapX, stage, 60f) > 0f)
                CardGui.GlowRect(new Rect(gapX - gapWidth * 0.7f, ground - 10f, gapWidth * 1.4f, 20f), WithAlpha(VioletShapes.Glow, 0.6f * EdgeFade(gapX, stage, 60f)));

            for (float cx = l + 10f; cx < r - 6f; cx += 22f)
            {
                if (Mathf.Abs(cx - gapX) < gapWidth * 0.5f + 6f) continue;
                if (cx < stage.x + 6f || cx > stage.xMax - 6f) continue;
                float phase = DemoBosses.Hash(Mathf.Round(cx - l));
                float k = Mathf.Repeat(clock * 2.4f + phase, 1f); // the clock runs at a third of the speed in Serenity
                float y = top + 10f + k * (ceiling - 10f);
                CardGui.Sprite(VioletShapes.Needle, new Vector2(cx, y), new Vector2(34f, 30f), VioletShapes.Bright, false, 90f);
                CardGui.Disc(new Vector2(cx, top + 4f), 3f, WithAlpha(VioletShapes.Glow, 0.8f)); // emitters along the ceiling
            }
        }

        /// <summary>The level's terrain on the stage (gray-violet until Violet is restored), cut off at the stage's edges.</summary>
        static void Terrain(Rect r, Rect stage)
        {
            bool whole = stage.Contains(r.min) && stage.Contains(r.max);
            if (!ClipTo(ref r, stage)) return;
            CardGui.Box(r, Stone);
            CardGui.Box(new Rect(r.x, r.y, r.width, 3f), WithAlpha(Color.white, 0.35f));
            if (whole) CardGui.Outline(r, WithAlpha(Ink, 0.35f), 1.5f);
        }

        /// <summary>
        /// IMGUI doesn't clip to the stage, so things scrolling in are cut here: `r` becomes its part inside the stage
        /// (false: none of it is).
        /// </summary>
        static bool ClipTo(ref Rect r, Rect stage)
        {
            float xMin = Mathf.Max(r.xMin, stage.xMin), xMax = Mathf.Min(r.xMax, stage.xMax);
            float yMin = Mathf.Max(r.yMin, stage.yMin), yMax = Mathf.Min(r.yMax, stage.yMax);
            if (xMax <= xMin || yMax <= yMin) return false;
            r = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        /// <summary>1 well inside the stage, fading to 0 as a prop of half-width `half` reaches its left or right edge.</summary>
        static float EdgeFade(float x, Rect stage, float half) =>
            Smooth(Seg(stage.xMax - half - x, 0f, 50f)) * Smooth(Seg(x - stage.x - half, 0f, 50f));

        /// <summary>The row of abilities across the top: each lights up in turn as the run uses it.</summary>
        static void AbilityRow(Rect stage, string[] keys, float[] presses, float[] usedAt, float t, Color accent, float shiftInSurf)
        {
            const float keyH = 40f, slotW = 116f;
            float x0 = stage.x + 24f, y = stage.y + 34f;
            for (int i = 0; i < keys.Length; i++)
            {
                var c = new Vector2(x0 + slotW * (i + 0.5f), y);
                bool lit = t >= usedAt[i];
                float pop = Seg(t, usedAt[i], usedAt[i] + 0.45f);
                if (lit) CardGui.Round(new Rect(c.x - slotW * 0.5f + 4f, y - 26f, slotW - 8f, 78f), WithAlpha(accent, 0.12f), 6f);
                if (pop > 0f && pop < 1f) CardGui.Ring(c, 24f + 40f * pop, 3f * (1f - pop) + 1f, WithAlpha(accent, 1f - pop));

                float a = CardGui.Alpha;
                if (!lit) CardGui.Alpha *= 0.45f;
                if (i == 2) // SURF: Down + the dash key
                {
                    float w1 = CardGui.KeyWidth(keys[2], keyH), w2 = CardGui.KeyWidth(keys[0], keyH), gap = 18f;
                    float left = c.x - (w1 + w2 + gap) * 0.5f;
                    CardGui.Key(new Rect(left, y - keyH * 0.5f, w1, keyH), keys[2], presses[2], accent);
                    CardGui.Text(new Rect(left + w1, y - keyH * 0.5f, gap, keyH), "+", 18, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                    CardGui.Key(new Rect(left + w1 + gap, y - keyH * 0.5f, w2, keyH), keys[0], Mathf.Max(presses[2], shiftInSurf), accent);
                }
                else
                {
                    float w = CardGui.KeyWidth(keys[i], keyH);
                    CardGui.Key(new Rect(c.x - w * 0.5f, y - keyH * 0.5f, w, keyH), keys[i], presses[i], accent);
                }
                string label = i == 4 ? "HOLD  " + GauntletLabels[i] : GauntletLabels[i];
                CardGui.Text(new Rect(c.x - slotW * 0.5f, y + 24f, slotW, 22f), label, 14, lit ? Color.Lerp(accent, Ink, 0.35f) : Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                CardGui.Alpha = a;
            }
        }

        // ---------- Blaze Strike, drawn as the game draws it (BlazeStrikeVisuals' default look) ----------

        static Color BlazeHeat(float k) => BlazeStrikeLook.DefaultHeat(k);

        /// <summary>The demo player's sword hand and blade tip on the card (BlazeStrikeAbility's anchors for this frame).</summary>
        static void DemoBlade(Sprite body, Vector2 feet, out Vector2 hand, out Vector2 tip)
        {
            var a = BladeAnchor.Find(BladeAnchor.Defaults, body ? body.name : "");
            var pivot = body ? new Vector2(feet.x, feet.y - body.pivot.y * PlayerUnit / body.pixelsPerUnit) : feet + new Vector2(0f, -0.6f * PlayerUnit);
            hand = pivot + new Vector2(a.hand.x, -a.hand.y) * PlayerUnit;
            tip = pivot + new Vector2(a.tip.x, -a.tip.y) * PlayerUnit;
        }

        /// <summary>The warm light under the feet while charging.</summary>
        static void BlazeUnderLight(Vector2 feet, float charge) =>
            CardGui.GlowRect(new Rect(feet.x - 48f, feet.y - 13f, 96f, 26f), WithAlpha(BlazeHeat(0.6f), 0.45f * Smooth(charge)));

        /// <summary>
        /// The charge on the player: embers streaking in to the hand, heat creeping up the blade from the hilt to the tip
        /// (`charge` 0 -> 1: the blade is the gauge), the hand's flame growing from dark red to white-hot. At READY a
        /// ring of flame bursts, the tip glints, and flames keep licking up the blade.
        /// </summary>
        static void BlazeCharge(Vector2 hand, Vector2 tip, float charge, bool ready, float sinceReady, float time)
        {
            if (!ready)
                for (int i = 0; i < 10; i++)
                {
                    float cycle = time * 2.8f + i / 10f, k = Mathf.Repeat(cycle, 1f);
                    float a = DemoBosses.Hash(i * 3.3f + Mathf.Floor(cycle) * 1.7f) * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var p = hand + dir * Mathf.Lerp(Mathf.Lerp(50f, 90f, DemoBosses.Hash(i * 7.1f)), 4f, k * k);
                    CardGui.Line(p, p + dir * (6f + 10f * k), 3f, WithAlpha(BlazeHeat(0.55f + 0.4f * k), Mathf.Sin(Mathf.PI * k) * Mathf.Min(1f, 0.5f + charge)));
                }

            var front = Vector2.Lerp(hand, tip, ready ? 1f : charge);
            CardGui.Line(hand, front, 10f, WithAlpha(BlazeHeat(0.3f + 0.4f * charge), 0.4f));
            CardGui.Line(hand, front, 4f, WithAlpha(BlazeHeat(0.6f + 0.4f * charge), 0.95f));
            if (!ready) CardGui.Glow(front, 10f, WithAlpha(BlazeHeat(1f), Mathf.Min(1f, charge * 6f)));

            float size = Mathf.Lerp(15f, 39f, Smooth(charge)) * (1f + 0.15f * Mathf.Sin(time * 31f) * Mathf.Sin(time * 13f));
            CardGui.Glow(hand + new Vector2(0f, -size * 0.25f), size * 0.9f, WithAlpha(BlazeHeat(0.15f + 0.6f * charge), 0.8f));
            CardGui.Lozenge(hand + new Vector2(0f, -size * 0.35f), size * 0.45f, size * 1.1f, WithAlpha(BlazeHeat(0.3f + 0.65f * charge), 0.9f));
            if (charge > 0.45f) CardGui.Glow(hand, size * 0.45f, WithAlpha(BlazeHeat(1f), Seg(charge, 0.45f, 1f)));
            if (!ready) return;

            float burst = Seg(sinceReady, 0f, 0.28f);
            if (burst < 1f) CardGui.Ring(hand, Mathf.Lerp(9f, 54f, 1f - (1f - burst) * (1f - burst)), 4f * (1f - burst) + 1f, WithAlpha(BlazeHeat(0.7f), 1f - burst));
            float glint = burst < 1f ? Mathf.Lerp(57f, 19f, Smooth((burst - 0.15f) / 0.85f)) * Mathf.Clamp01(burst / 0.1f)
                                     : 19f * (0.8f + 0.25f * Mathf.Sin(time * 17f) * Mathf.Sin(time * 5.3f));
            Glint(tip, glint, 45f * burst + time * 40f);
            for (int i = 0; i < 6; i++)
            {
                float k = Mathf.Repeat(time * 3.2f + i * 0.37f, 1f);
                var p = Vector2.Lerp(hand, tip, (i + 0.5f) / 6f) + new Vector2(0f, -24f * k);
                CardGui.Lozenge(p, 7f * (1f - k) + 2f, 15f * (1f - k) + 3f, WithAlpha(BlazeHeat(0.9f - 0.45f * k), 0.9f * (1f - k)));
            }
        }

        /// <summary>A four-point glint: two thin streaks and a soft center.</summary>
        static void Glint(Vector2 at, float size, float degrees)
        {
            if (size <= 0f) return;
            CardGui.Glow(at, size * 0.4f, WithAlpha(BlazeHeat(0.9f), 0.8f));
            var prev = CardGui.Rotate(at, degrees);
            CardGui.Lozenge(at, size * 0.14f, size, WithAlpha(Color.white, 0.95f));
            CardGui.Lozenge(at, size, size * 0.14f, WithAlpha(Color.white, 0.95f));
            GUI.matrix = prev;
        }

        /// <summary>
        /// The strike's flaming crescent, fitted to its hitbox as in the game (2.4 x 1.8 world units, 1.65 ahead at hip
        /// height): a smear inside, the red ragged edge, the orange body, the white-hot rim, and embers sprayed along the
        /// swing. `k` 0 -> 1 over its life: it sweeps down, then burns away from the top.
        /// </summary>
        static void FlameCrescent(Vector2 feet, float k, float time)
        {
            const float worldPx = PlayerUnit / 1.5f; // the game draws the player at 1.5x
            const float top = 78f * Mathf.Deg2Rad;
            const int steps = 24;
            float w = 2.4f * worldPx, h = 1.8f * worldPx;
            float rx = w * 0.85f, ry = h * 0.5f;
            var e = new Vector2(feet.x + 1.65f * worldPx + w * 0.5f - rx, feet.y - 0.6f * PlayerUnit);
            rx *= 0.88f;
            ry *= 0.88f;
            float head = 1f - Mathf.Pow(1f - Seg(k, 0f, 0.3f), 3f), burn = Seg(k, 0.3f, 1f);
            float thin = 1f - 0.55f * burn, dim = 1f - burn * burn;
            Vector2 At(float u, float s)
            {
                float a = Mathf.Lerp(top, -top, u);
                return e + new Vector2(Mathf.Cos(a) * rx * s, -Mathf.Sin(a) * ry * s);
            }
            float Visible(float u) => Mathf.Clamp01((head * 1.15f - u) / 0.15f) * Mathf.Clamp01((u - (burn * 1.3f - 0.3f)) / 0.3f) * dim;
            float Thickness(float u) => 0.42f * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.65f) * thin;

            var body = new Color(1f, 0.5f, 0.08f);
            var edge = new Color(0.78f, 0.07f, 0.02f);
            var core = new Color(1f, 0.96f, 0.82f);
            for (int layer = 0; layer < 4; layer++)
                for (int i = 0; i < steps; i++)
                {
                    float u0 = i / (float)steps, u1 = (i + 1f) / steps, um = (u0 + u1) * 0.5f;
                    float vis = Visible(um), th = Thickness(um);
                    if (vis <= 0.01f) continue;
                    switch (layer)
                    {
                        case 0: ArcLine(At(u0, 1f - th * 1.45f), At(u1, 1f - th * 1.45f), th * rx * 0.9f, WithAlpha(body, 0.3f * vis)); break;
                        case 1: ArcLine(At(u0, 1.02f), At(u1, 1.02f), th * rx * 0.35f + 4f, WithAlpha(edge, 0.95f * vis)); break;
                        case 2: ArcLine(At(u0, 1f - th * 0.5f), At(u1, 1f - th * 0.5f), th * rx * 0.8f, WithAlpha(body, 0.92f * vis)); break;
                        case 3: ArcLine(At(u0, 1f - th * 0.07f), At(u1, 1f - th * 0.07f), 2f + 3f * th / 0.42f, WithAlpha(core, vis)); break;
                    }
                }

            for (int i = 0; i < 9; i++) // ragged flame licks off the outer edge, leaning back from the swing
            {
                float u = Mathf.Repeat((i + 0.5f) / 9f + time * 0.19f, 1f);
                float vis = Visible(u);
                if (vis <= 0.05f) continue;
                var p = At(u, 1.03f);
                var outward = (At(u, 1.3f) - p).normalized;
                float length = 0.12f * rx * Mathf.Sqrt(Mathf.Sin(Mathf.PI * u)) * (0.5f + 0.5f * Mathf.PerlinNoise(i * 1.7f, time * 9f));
                var prev = CardGui.Rotate(p, Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 8f);
                CardGui.Lozenge(p + new Vector2(length * 0.5f, 0f), length, 6f, WithAlpha(edge, 0.85f * vis));
                GUI.matrix = prev;
            }

            for (int i = 0; i < 10; i++) // embers thrown off along the swing as the blade passes
            {
                float u = (i + 0.5f) / 10f;
                float passed = 0.3f * (1f - Mathf.Pow(1f - u, 1f / 3f)); // when `head` reached u
                float s = (k - passed) * 0.3f; // seconds since (the crescent lives 0.3 s)
                if (s < 0f || s > 0.3f) continue;
                var p = At(u, 1f);
                var along = (At(u + 0.02f, 1f) - p).normalized;
                var outward = (At(u, 1.3f) - p).normalized;
                p += (along * 0.7f + outward * 0.5f) * 6.5f * worldPx * s * (0.7f + 0.6f * DemoBosses.Hash(i * 2.3f));
                CardGui.Diamond(p, 7f * (1f - s / 0.3f) + 2f, WithAlpha(BlazeHeat(0.95f - s * 1.5f), 1f - s / 0.3f), s * 600f);
            }
        }

        /// <summary>A crescent segment: a line overlapping its neighbours a little, so the arc has no gaps.</summary>
        static void ArcLine(Vector2 a, Vector2 b, float width, Color c)
        {
            var d = (b - a).normalized * 1.5f;
            CardGui.Line(a - d, b + d, width, c);
        }

        /// <summary>The strike landing at `at`: a white-hot flash and sparks off it (`k` 0 -> 1).</summary>
        static void BlazeHit(Vector2 at, float k)
        {
            if (k <= 0f || k >= 1f) return;
            Glint(at, 70f * (1f - 0.5f * k), 20f * k);
            for (int i = 0; i < 12; i++)
            {
                float a = (DemoBosses.Hash(i * 4.3f) - 0.5f) * 2.6f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (i % 5 == 0) dir.x *= -0.6f;
                var p = at + dir * (40f + 120f * DemoBosses.Hash(i * 1.9f)) * Mathf.Sqrt(k);
                CardGui.Line(p - dir * 12f * (1f - k), p, 2.5f, WithAlpha(BlazeHeat(0.9f), 1f - k));
            }
        }

        // ---------- Shared pieces for these demos ----------

        static string KeyAt(InstructionData d, int i, string fallback) =>
            d.keys != null && i < d.keys.Length && !string.IsNullOrEmpty(d.keys[i]) ? d.keys[i] : fallback;

        /// <summary>Fades the demo in at the start of a loop and out at its end, so the jump back reads as a cut.</summary>
        static float LoopFade(float t, float period) => Smooth(Seg(t, 0f, 0.2f)) * (1f - Smooth(Seg(t, period - 0.35f, period)));

        static Sprite Pick(Sprite[] frames, float t, float fps, bool loop)
        {
            if (frames == null || frames.Length == 0) return null;
            int i = (int)(Mathf.Max(0f, t) * Mathf.Max(0.01f, fps));
            return frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)];
        }

        static Sprite Or(Sprite a, Sprite b) => a ? a : b;
        static Sprite IdleFrame(InstructionData d, float t) => Loop(d.playerLoop, t, d.playerLoopFps);
        static Sprite RunFrame(InstructionData d, float t) => Or(Pick(d.playerRun, t, d.playerRunFps, true), IdleFrame(d, t));
        static Sprite JumpFrame(InstructionData d, float since) => Or(Pick(d.playerJump, since, d.playerJumpFps, false), IdleFrame(d, since));
        static Sprite HurtFrame(InstructionData d) => Or(d.playerHurt, IdleFrame(d, 0f));

        /// <summary>The attack frames, once, from `since` = 0; null once they're done (or when there are none).</summary>
        static Sprite AttackFrame(InstructionData d, float since)
        {
            if (d.playerAction == null || d.playerAction.Length == 0 || since < 0f) return null;
            int i = (int)(since * Mathf.Max(0.01f, d.playerActionFps));
            return i < d.playerAction.Length ? d.playerAction[i] : null;
        }

        /// <summary>The player sprite standing on `feet` (a stick figure if the card has no art).</summary>
        static void Runner(InstructionData d, Sprite s, Vector2 feet, bool flip, float degrees = 0f)
        {
            if (s) CardGui.SpriteOnGround(s, feet, PlayerUnit, Color.white, flip, degrees);
            else Player(d, feet, 0f, -1f, null, flip, degrees);
        }

        /// <summary>Fading copies behind a dash.</summary>
        static void Ghosts(InstructionData d, Sprite s, Vector2 feet, Vector2 step, int count)
        {
            if (!s) return;
            for (int i = count; i >= 1; i--)
                CardGui.SpriteOnGround(s, feet + step * i, PlayerUnit, new Color(1f, 1f, 1f, 0.32f * (1f - i / (count + 1f))), false, 7f);
        }

        /// <summary>A keycap centered on `center` (`alpha` fades it on top of the card's own fade).</summary>
        static void FloatKey(string key, Vector2 center, float height, float press, Color accent, float alpha = 1f)
        {
            if (alpha <= 0f) return;
            float w = CardGui.KeyWidth(key, height);
            float a = CardGui.Alpha;
            CardGui.Alpha *= alpha;
            CardGui.Key(new Rect(center.x - w * 0.5f, center.y - height * 0.5f, w, height), key, press, accent);
            CardGui.Alpha = a;
        }

        /// <summary>Little squares flying out and fading (HeatPuff's look); k 0 -> 1 over its life.</summary>
        static void Burst(Vector2 at, float k, Color color, float reach)
        {
            if (k <= 0f || k >= 1f) return;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 0.2f + 0.4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                CardGui.Diamond(at + dir * reach * (0.2f + 0.8f * Mathf.Sqrt(k)), 14f * (1f - k) + 3f, WithAlpha(color, 1f - k), k * 240f + i * 30f);
            }
        }

        /// <summary>Shards of something breaking (VioletShapes.Shard) flying out and falling.</summary>
        static void Shatter(Vector2 at, float k, Color color, int count, float reach)
        {
            if (k <= 0f || k >= 1f) return;
            for (int i = 0; i < count; i++)
            {
                float a = DemoBosses.Hash(i * 2.7f) * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), -Mathf.Abs(Mathf.Sin(a)) - 0.3f) * reach * (0.6f + 0.6f * DemoBosses.Hash(i * 5.1f));
                var p = at + v * k + new Vector2(0f, 260f * k * k);
                CardGui.Sprite(VioletShapes.Shard, p, Vector2.one * (18f + 14f * DemoBosses.Hash(i)), WithAlpha(color, 1f - k * k), false, k * 400f + i * 50f);
            }
        }

        /// <summary>Height of a jump `s` seconds after take-off from height `y0` with upward speed `v0` (px, px/s).</summary>
        static float Parabola(float s, float y0, float v0) => y0 + v0 * s - 1100f * s * s;

        static float Interpolate(float[] times, float[] values, float t)
        {
            if (t <= times[0]) return values[0];
            for (int i = 1; i < times.Length; i++)
                if (t < times[i]) return Mathf.Lerp(values[i - 1], values[i], (t - times[i - 1]) / (times[i] - times[i - 1]));
            return values[values.Length - 1];
        }

        static string Spaced(string s) => string.Join(" ", s.ToCharArray());
    }
}
