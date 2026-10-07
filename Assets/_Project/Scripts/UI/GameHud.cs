using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The in-level HUD, in the UiKit look. It only LISTENS to GameEvents and READS state; it never drives gameplay.
    ///   Top left, a panel in the level's color:
    ///     HP          slanted pips; a lost one flashes, the last one blinks red.
    ///     Colors      the seven gems in play order: restored ones lit (a new one pops), this level's marked.
    ///     Abilities   one keycap per ability with its keys (as InputReader binds them): dim while locked; when Green
    ///                 steals one it flashes red and stays struck through until it's returned (then it pops).
    ///     Serenity    once unlocked: a segmented meter, ACTIVE (draining) / RECHARGING (filling) / READY [Q];
    ///                 a press that's refused shakes it red.
    ///   Top center, while a boss is fighting: its name in its color, HP segments with a tick at every phase threshold
    ///   (damage trails behind), or catch pips for the bosses you catch (Orange, Blue). Red adds its rage meter (blinking
    ///   OVERHEAT), Green shows what it's holding. Violet's appears for the duel (its run isn't a BossBase fight).
    /// It fades out while a cinematic plays (letterbox). Unscaled time. DevOverlay (F12) has the debug readout.
    /// HP, the boss and Serenity's state are READ every frame (the player, LevelController.Current.Boss, the Serenity
    /// ability), not only taken from events: a level announces them in its first frame, possibly before SceneLoaded
    /// reaches this. The events add the flashes (a hit, a steal, a restored color, a refused Serenity).
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        int hp, hpMax, lastHp;
        float hpHitAt = -10f;

        BossBase boss;
        float bossSeen, bossGhost = 1f, bossHitAt = -10f, bossDefeatedAt = -10f, lastBossFraction = 1f;

        readonly HashSet<AbilityId> stolen = new();
        readonly Dictionary<AbilityId, float> stolenAt = new(), returnedAt = new(), unlockedAt = new();
        readonly Dictionary<ColorId, float> restoredAt = new();

        SerenityState serenity = SerenityState.Unavailable;
        float serenityFraction, deniedAt = -10f, readyAt = -10f;

        bool cinematic;
        float shown;

        void OnEnable()
        {
            GameEvents.BossFightStarted += OnBossStarted;
            GameEvents.BossHealthChanged += OnBossHealth;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.SceneLoaded += OnSceneLoaded;
            GameEvents.AbilityStolen += OnStolen;
            GameEvents.AbilityReturned += OnReturned;
            GameEvents.AbilityUnlocked += OnUnlocked;
            GameEvents.ColorRestored += OnRestored;
            GameEvents.SerenityChanged += OnSerenity;
            GameEvents.SerenityDenied += OnSerenityDenied;
            GameEvents.CinematicChanged += OnCinematic;
        }

        void OnDisable()
        {
            GameEvents.BossFightStarted -= OnBossStarted;
            GameEvents.BossHealthChanged -= OnBossHealth;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.SceneLoaded -= OnSceneLoaded;
            GameEvents.AbilityStolen -= OnStolen;
            GameEvents.AbilityReturned -= OnReturned;
            GameEvents.AbilityUnlocked -= OnUnlocked;
            GameEvents.ColorRestored -= OnRestored;
            GameEvents.SerenityChanged -= OnSerenity;
            GameEvents.SerenityDenied -= OnSerenityDenied;
            GameEvents.CinematicChanged -= OnCinematic;
        }

        static float Now => Time.unscaledTime;

        void OnBossStarted(BossBase b)
        {
            boss = b;
            bossDefeatedAt = -10f;
            bossGhost = lastBossFraction = b && b.Health ? b.Health.Fraction : 1f;
        }

        void OnBossHealth(BossBase b)
        {
            if (b != boss || !b.Health) return;
            float f = b.Health.Fraction;
            if (f < lastBossFraction) bossHitAt = Now;
            if (f > bossGhost) bossGhost = f; // healed / reset (Violet's checkpoint)
            lastBossFraction = f;
        }

        void OnBossDefeated(BossBase b)
        {
            if (b != boss) return;
            bossDefeatedAt = Now;
            bossHitAt = Now;
            lastBossFraction = 0f;
        }

        void OnSceneLoaded(string _)
        {
            boss = null;
            bossSeen = 0f;
            hpMax = 0;
            stolen.Clear();
            stolenAt.Clear();
            returnedAt.Clear();
            cinematic = false;
        }

        void OnStolen(AbilityId id)
        {
            stolen.Add(id);
            stolenAt[id] = Now;
        }

        void OnReturned(AbilityId id)
        {
            if (stolen.Remove(id)) returnedAt[id] = Now;
        }

        void OnUnlocked(AbilityId id) => unlockedAt[id] = Now;
        void OnRestored(ColorId id) => restoredAt[id] = Now;

        void OnSerenity(SerenityState state, float fraction) => serenityFraction = fraction; // its state is read in Update

        void OnSerenityDenied() => deniedAt = Now;
        void OnCinematic(bool playing) => cinematic = playing;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool inLevel = LevelController.Current != null || PlayerController.Instance != null;
            shown = Mathf.MoveTowards(shown, inLevel && !cinematic ? 1f : 0f, dt / 0.3f);

            // The player's HP (a drop flashes the pips it took).
            var pc = PlayerController.Instance;
            if (pc && pc.Health)
            {
                int current = pc.Health.Current;
                if (hpMax > 0 && current < hp)
                {
                    hpHitAt = Now;
                    lastHp = hp;
                }
                hp = current;
                hpMax = pc.Health.Max;
            }
            else hpMax = 0;

            // Serenity: READY / ACTIVE / RECHARGING straight from the ability (the events carry the meter's fraction).
            var ability = pc && pc.Loadout != null ? pc.Loadout.Get(AbilityId.Serenity) as SerenityAbility : null;
            var state = ability ? ability.State : SerenityState.Unavailable;
            if (state == SerenityState.Ready && serenity == SerenityState.Recharging) readyAt = Now;
            if (state == SerenityState.Ready) serenityFraction = 1f;
            serenity = state;

            // The level's boss, once it's fighting (whether or not this saw BossFightStarted).
            var levelBoss = LevelController.Current ? LevelController.Current.Boss : null;
            if (!boss && levelBoss && levelBoss.IsFighting) OnBossStarted(levelBoss);

            bool fighting = boss && boss.IsFighting;
            bool lingering = boss && Now - bossDefeatedAt < 1.6f;
            bossSeen = Mathf.MoveTowards(bossSeen, (fighting || lingering) ? 1f : 0f, dt / (fighting ? 0.35f : 0.5f));

            // Recent damage trails behind the bar, then drains down to it.
            if (boss && boss.Health && Now - bossHitAt > 0.45f)
                bossGhost = Mathf.MoveTowards(bossGhost, boss.Health.Fraction, dt * 0.8f);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || shown <= 0f) return;
            GUI.depth = 10; // under everything else
            var view = UiKit.Fill();
            view.Begin();
            CardGui.Alpha = UiKit.Smooth(shown);

            DrawPlayerPanel(new Vector2(28f, 24f), UiKit.CurrentAccent);
            if (boss && bossSeen > 0f)
            {
                float a = CardGui.Alpha;
                CardGui.Alpha *= UiKit.Smooth(bossSeen);
                DrawBossBar(view.Rect);
                CardGui.Alpha = a;
            }
            UiKit.End();
        }

        // ---------- Player ----------

        void DrawPlayerPanel(Vector2 at, Color accent)
        {
            var pc = PlayerController.Instance;
            var abilities = AbilityList();
            bool hasSerenity = serenity != SerenityState.Unavailable;

            // Ability slots size themselves to their keys and labels.
            const float keyH = 30f;
            float slotsWidth = 0f;
            var widths = new float[abilities.Count];
            for (int i = 0; i < abilities.Count; i++)
            {
                var info = UiKit.Ability(abilities[i]);
                widths[i] = Mathf.Max(UiKit.Keys(info.Keys, 0f, 0f, keyH, 0f, accent, false), CardGui.Measure(info.Short, 11, FontStyle.Bold).x) + 16f;
                slotsWidth += widths[i];
            }

            float width = Mathf.Max(400f, slotsWidth + 40f);
            float height = 168f + (hasSerenity ? 40f : 0f);
            var panel = new Rect(at.x, at.y, width, height);
            UiKit.Panel(panel, accent, -1f, 16f, 0.8f);
            float x = panel.x + 20f;

            // HP.
            float rowY = panel.y + 26f;
            Label(new Rect(x, rowY - 12f, 40f, 24f), "HP");
            if (hpMax > 0)
            {
                float since = Now - hpHitAt;
                var shake = since < 0.3f ? new Vector2(Mathf.Sin(since * 90f), 0f) * 4f * (1f - since / 0.3f) : Vector2.zero;
                bool critical = hp <= 1 && hp > 0;
                for (int i = 0; i < hpMax; i++)
                {
                    var pip = new Rect(x + 48f + i * 34f, rowY - 9f, 26f, 18f);
                    pip.position += shake;
                    UiKit.Slanted(UiKit.Expand(pip, 2f), new Color(0f, 0f, 0f, 0.6f));
                    bool full = i < hp;
                    bool justLost = !full && i < lastHp && since < 0.45f;
                    Color c;
                    if (justLost) c = Color.Lerp(Color.white, UiKit.Danger, since / 0.45f);
                    else if (!full) c = new Color(0.16f, 0.17f, 0.2f, 0.9f);
                    else if (critical) c = Color.Lerp(UiKit.Danger, Color.white, Blink(2.5f) * 0.6f);
                    else c = Color.Lerp(UiKit.TextColor, accent, 0.25f);
                    UiKit.Slanted(pip, c);
                }
            }

            // The seven colors, in play order.
            rowY = panel.y + 66f;
            var bands = new Color[UiKit.Spectrum.Length]; // a little heart, filled with what's been restored
            for (int i = 0; i < bands.Length; i++) bands[i] = UiKit.Restored(UiKit.Spectrum[i]) ? UiKit.Accent(UiKit.Spectrum[i]) : UiKit.Gray;
            UiKit.Heart(new Rect(x + 2f, rowY - 13f, 28f, 26f), bands, UiKit.WithAlpha(accent, 0.6f), 1f);
            var order = Game.Config ? Game.Config.colorOrder : null;
            for (int i = 0; order != null && i < order.Count; i++)
            {
                var data = order[i];
                if (!data) continue;
                var c = new Vector2(x + 58f + i * 30f, rowY);
                bool lit = UiKit.Restored(data.id);
                float pop = restoredAt.TryGetValue(data.id, out var t) ? Mathf.Clamp01(1f - (Now - t) / 1.5f) : 0f;
                bool here = LevelController.Current && LevelController.Current.Color == data.id;
                UiKit.Gem(c, 22f, UiKit.Accent(data.id), lit, pop + (here && lit ? 0.3f : 0f));
                if (here) CardGui.Box(new Rect(c.x - 7f, c.y + 15f, 14f, 3f), UiKit.Accent(data.id));
            }

            // Abilities: keycaps with their keys, in the order the colors grant them.
            rowY = panel.y + 116f;
            float sx = x;
            for (int i = 0; i < abilities.Count; i++)
            {
                var id = abilities[i];
                var info = UiKit.Ability(id);
                bool isStolen = stolen.Contains(id);
                bool owned = pc && pc.Loadout != null && pc.Loadout.Has(id);
                float slot = widths[i];
                float keysW = UiKit.Keys(info.Keys, 0f, 0f, keyH, 0f, accent, false);
                float kx = sx + (slot - keysW) * 0.5f;
                var center = new Vector2(sx + slot * 0.5f, rowY);

                float a = CardGui.Alpha;
                if (!owned && !isStolen) CardGui.Alpha *= 0.28f;
                float stolenFlash = isStolen && stolenAt.TryGetValue(id, out var st) ? Mathf.Clamp01(1f - (Now - st) / 0.6f) : 0f;
                if (isStolen) CardGui.Glow(center, 34f, UiKit.WithAlpha(UiKit.Danger, 0.25f + 0.5f * stolenFlash));
                UiKit.Keys(info.Keys, kx, rowY, keyH, 0f, isStolen ? UiKit.Danger : accent);
                if (isStolen)
                {
                    CardGui.Box(new Rect(kx - 2f, rowY - keyH * 0.5f, keysW + 4f, keyH), UiKit.WithAlpha(UiKit.Danger, 0.35f));
                    CardGui.Line(new Vector2(kx - 4f, rowY + keyH * 0.5f + 2f), new Vector2(kx + keysW + 4f, rowY - keyH * 0.5f - 2f), 3f, UiKit.Danger);
                }
                if (!string.IsNullOrEmpty(info.Tag) && owned)
                    CardGui.Text(new Rect(kx + keysW - 6f, rowY - keyH * 0.5f - 12f, 40f, 14f), info.Tag, 10, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                string label = isStolen ? "STOLEN" : info.Short;
                var labelColor = isStolen ? Color.Lerp(UiKit.Danger, Color.white, Blink(2f) * 0.4f) : UiKit.TextDim;
                CardGui.Text(new Rect(sx, rowY + keyH * 0.5f + 2f, slot, 16f), label, 11, labelColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                CardGui.Alpha = a;

                float back = returnedAt.TryGetValue(id, out var rt) ? (Now - rt) / 0.5f : 2f;
                float fresh = unlockedAt.TryGetValue(id, out var ut) ? (Now - ut) / 0.8f : 2f;
                float pop = Mathf.Min(back, fresh);
                if (pop >= 0f && pop < 1f) CardGui.Ring(center, 18f + 30f * pop, 3f * (1f - pop) + 1f, UiKit.WithAlpha(accent, 1f - pop));
                sx += slot;
            }

            if (hasSerenity) DrawSerenity(new Rect(x, panel.y + 160f, width - 40f, 30f));
        }

        void DrawSerenity(Rect row)
        {
            var core = new Color(0.48f, 0.42f, 1f);
            float denied = Mathf.Clamp01(1f - (Now - deniedAt) / 0.35f);
            float shake = denied * Mathf.Sin(Now * 70f) * 5f;
            row.x += shake;

            Color fill;
            string state;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Now * 4f);
            switch (serenity)
            {
                case SerenityState.Active:
                    fill = Color.Lerp(core, Color.white, 0.35f);
                    state = "ACTIVE";
                    break;
                case SerenityState.Recharging:
                    fill = new Color(0.34f, 0.32f, 0.5f);
                    state = "RECHARGING";
                    break;
                default:
                    float pop = Mathf.Clamp01(1f - (Now - readyAt) / 0.5f);
                    fill = Color.Lerp(core, Color.white, 0.15f * pulse + 0.6f * pop);
                    state = "READY";
                    break;
            }
            fill = Color.Lerp(fill, UiKit.Danger, denied);

            Label(new Rect(row.x, row.y, 120f, row.height), "SERENITY", 13);
            var bar = new Rect(row.x + 112f, row.center.y - 6f, 150f, 12f);
            if (serenity == SerenityState.Ready) CardGui.Glow(bar.center, 100f, UiKit.WithAlpha(core, 0.15f + 0.1f * pulse));
            UiKit.SegmentBar(bar, serenityFraction, 12, fill, new Color(0.12f, 0.12f, 0.18f, 0.9f), 3f);
            float textX = bar.xMax + 12f;
            CardGui.Text(new Rect(textX, row.y, 120f, row.height), state, 13, serenity == SerenityState.Recharging ? UiKit.TextDim : fill,
                         TextAnchor.MiddleLeft, FontStyle.Bold);
            if (serenity == SerenityState.Ready)
            {
                float w = CardGui.Measure(state, 13, FontStyle.Bold).x;
                CardGui.Key(new Rect(textX + w + 10f, row.center.y - 12f, CardGui.KeyWidth("Q", 24f), 24f), "Q", 0f, core);
            }
        }

        /// <summary>The abilities the colors grant, in play order (Violet grants none).</summary>
        static List<AbilityId> AbilityList()
        {
            var list = new List<AbilityId>();
            if (!Game.Config) return list;
            foreach (var data in Game.Config.colorOrder)
                if (data && data.grantedAbility != AbilityId.None && !list.Contains(data.grantedAbility))
                    list.Add(data.grantedAbility);
            return list;
        }

        // ---------- Boss ----------

        void DrawBossBar(Rect screen)
        {
            var h = boss.Health;
            if (!h) return;
            var accent = UiKit.Accent(boss.Color);
            bool catches = boss is OrangeBoss || boss is BlueBoss;
            var red = boss as RedBoss;
            var green = boss as GreenBoss;
            bool holding = green && green.Stolen.Count > 0;

            float width = Mathf.Min(900f, screen.width - 640f);
            float height = 92f + (red ? 30f : 0f) + (holding ? 36f : 0f);
            var panel = new Rect(screen.center.x - width * 0.5f, 22f, width, height);
            UiKit.Panel(panel, accent, -1f, 16f, 0.75f);

            // Name and phase.
            var top = new Rect(panel.x + 22f, panel.y + 8f, panel.width - 44f, 30f);
            CardGui.Box(new Rect(top.x, top.y + 7f, 6f, 16f), accent);
            CardGui.Text(new Rect(top.x + 16f, top.y, top.width - 200f, top.height), UiKit.Spaced(boss.DisplayName.ToUpperInvariant()), 20, accent,
                         TextAnchor.MiddleLeft, FontStyle.Bold);
            bool defeated = h.IsDead;
            string right = defeated ? (catches ? "CAUGHT" : "DEFEATED")
                         : catches ? "CATCH IT"
                         : $"PHASE  {boss.Phase + 1} / {boss.PhaseThresholds.Count + 1}";
            CardGui.Text(new Rect(top.xMax - 200f, top.y, 200f, top.height), right, 15,
                         defeated ? accent : UiKit.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);

            var bar = new Rect(panel.x + 22f, panel.y + 50f, panel.width - 44f, 18f);
            if (catches) DrawCatches(bar, h, accent);
            else
            {
                bool immune = h.Invulnerable && !defeated;
                var fill = Color.Lerp(accent, Color.white, Mathf.Clamp01(1f - (Now - bossHitAt) / 0.15f) * 0.8f);
                if (immune) fill = Color.Lerp(fill, UiKit.Gray, 0.55f);
                int segments = h.Max <= 40 ? h.Max : 40;
                UiKit.SegmentBar(bar, h.Fraction, segments, fill, new Color(0.1f, 0.11f, 0.14f, 0.92f), segments > 24 ? 2f : 3f,
                                 boss.PhaseThresholds, UiKit.WithAlpha(UiKit.TextColor, 0.85f), bossGhost, UiKit.WithAlpha(Color.Lerp(accent, Color.white, 0.6f), 0.7f));
                if (immune) CardGui.Text(new Rect(bar.xMax - 140f, bar.yMax + 2f, 140f, 18f), "IMMUNE", 12, UiKit.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
            }

            float y = bar.yMax + 14f;
            if (red)
            {
                bool hot = red.Overheated || red.CurrentState == RedBoss.State.OverheatTell || red.CurrentState == RedBoss.State.Falling;
                float blink = Blink(2.5f);
                var rageRow = new Rect(panel.x + 22f, y, panel.width - 44f, 18f);
                var label = hot ? Color.Lerp(UiKit.Accent(ColorId.Yellow), Color.white, blink) : UiKit.TextDim;
                CardGui.Text(new Rect(rageRow.x, rageRow.y - 2f, 130f, 22f), hot ? "OVERHEAT" : "RAGE", 14, label, TextAnchor.MiddleLeft, FontStyle.Bold);
                var rageBar = new Rect(rageRow.x + 130f, rageRow.y + 3f, rageRow.width - 130f, 10f);
                var rageFill = hot ? Color.Lerp(UiKit.Accent(ColorId.Yellow), Color.white, blink * 0.8f)
                                   : Color.Lerp(UiKit.Accent(ColorId.Orange), UiKit.Accent(ColorId.Red), red.RageFraction);
                if (hot) CardGui.Glow(rageBar.center, rageBar.width * 0.55f, UiKit.WithAlpha(UiKit.Accent(ColorId.Yellow), 0.2f * blink));
                UiKit.SegmentBar(rageBar, hot ? 1f : red.RageFraction, 24, rageFill, new Color(0.14f, 0.08f, 0.07f, 0.9f), 2f);
                y += 30f;
            }
            if (holding)
            {
                CardGui.Text(new Rect(panel.x + 22f, y, 120f, 26f), "HOLDING", 14, UiKit.Danger, TextAnchor.MiddleLeft, FontStyle.Bold);
                float kx = panel.x + 140f;
                foreach (var id in green.Stolen)
                {
                    var info = UiKit.Ability(id);
                    kx += UiKit.Keys(info.Keys, kx, y + 13f, 24f, 0f, UiKit.Danger) + 18f;
                }
            }
        }

        /// <summary>Catch bosses: one diamond per catch to win, lit as you catch it.</summary>
        void DrawCatches(Rect bar, Health h, Color accent)
        {
            int total = Mathf.Max(1, h.Max), caught = Mathf.Clamp(h.Max - h.Current, 0, total);
            float size = 24f, gap = 30f;
            float x0 = bar.center.x - (total - 1) * (size + gap) * 0.5f;
            bool outOfReach = h.Invulnerable && !h.IsDead;
            for (int i = 0; i < total; i++)
            {
                var c = new Vector2(x0 + i * (size + gap), bar.center.y);
                bool lit = i < caught;
                float pop = lit && i == caught - 1 ? Mathf.Clamp01(1f - (Now - bossHitAt) / 0.8f) : 0f;
                UiKit.Gem(c, size, accent, lit, pop);
            }
            // Blue hangs out of reach until you reach the summit (Orange is only ever caught in a cage: no hint needed).
            if (boss is BlueBoss && outOfReach)
                CardGui.Text(new Rect(bar.x, bar.yMax + 2f, bar.width, 18f), UiKit.Spaced("OUT OF REACH"), 12, UiKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        // ---------- Helpers ----------

        static void Label(Rect r, string text, int size = 14) =>
            CardGui.Text(r, UiKit.Spaced(text), size, UiKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);

        /// <summary>0..1..0 at `hz` blinks a second (kept under ~3 for comfort).</summary>
        static float Blink(float hz) => 0.5f + 0.5f * Mathf.Sin(Now * hz * Mathf.PI * 2f);
    }
}
