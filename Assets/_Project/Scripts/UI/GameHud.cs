using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The in-level HUD, in the UiKit look. It only LISTENS to GameEvents and READS state; it never drives gameplay.
    ///   Top left, a panel in the level's color:
    ///     HP          pips; a lost one flashes, the last one blinks red.
    ///     Colors      a little heart and the seven gems in play order: restored ones lit (a new one pops), this level's marked.
    ///     Abilities   a slot per ability you HAVE, three to a row, appearing one by one as they're unlocked (with a glow);
    ///                 a big keycap with its keys (as InputReader binds them) and its name under it. When Green steals one
    ///                 it flashes red and stays struck through until it's returned (then it pops). The panel grows to fit.
    ///     Serenity    once unlocked: a segmented meter, ACTIVE (draining) / RECHARGING (filling) / READY [Q];
    ///                 a press that's refused shakes it red.
    ///   Its text is UiKit.Label (22+ px on the 1080p canvas, outlined, on whole pixels); shapes are snapped to pixels.
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
            panelHeight = -1f;
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

        const float PanelWidth = 472f, SlotWidth = 144f, SlotHeight = 92f, KeyHeight = 40f, GridTop = 116f;
        float panelHeight = -1f;

        void DrawPlayerPanel(Vector2 at, Color accent)
        {
            var pc = PlayerController.Instance;
            // Only what the player has (stolen counts: it's still theirs), in the order the colors grant them.
            var have = new List<AbilityId>();
            foreach (var id in AbilityList())
                if (stolen.Contains(id) || (pc && pc.Loadout != null && pc.Loadout.Has(id))) have.Add(id);
            bool hasSerenity = serenity != SerenityState.Unavailable;
            int rows = (have.Count + 2) / 3;

            // The panel grows (smoothly) when a new row of abilities appears.
            float target = GridTop + rows * SlotHeight + (hasSerenity ? 50f : 0f) + 8f;
            panelHeight = panelHeight < 0f ? target : Mathf.MoveTowards(panelHeight, target, Time.unscaledDeltaTime * 500f);
            var panel = UiKit.Snap(new Rect(at.x, at.y, PanelWidth, panelHeight));
            UiKit.Panel(panel, accent, -1f, 16f, 0.82f);
            float x = panel.x + 20f;

            // HP.
            float rowY = panel.y + 36f;
            UiKit.Label(new Rect(x, rowY - 18f, 52f, 36f), "HP", UiKit.TextLabel, UiKit.TextColor, TextAnchor.MiddleLeft);
            if (hpMax > 0)
            {
                float since = Now - hpHitAt;
                float shake = since < 0.3f ? Mathf.Sin(since * 90f) * 4f * (1f - since / 0.3f) : 0f;
                bool critical = hp <= 1 && hp > 0;
                for (int i = 0; i < hpMax; i++)
                {
                    var pip = UiKit.Snap(new Rect(x + 58f + i * 36f + shake, rowY - 10f, 28f, 20f));
                    CardGui.Box(UiKit.Snap(UiKit.Expand(pip, 2f)), new Color(0f, 0f, 0f, 0.75f));
                    bool full = i < hp;
                    bool justLost = !full && i < lastHp && since < 0.45f;
                    Color c;
                    if (justLost) c = Color.Lerp(Color.white, UiKit.Danger, since / 0.45f);
                    else if (!full) c = new Color(0.2f, 0.21f, 0.25f, 0.95f);
                    else if (critical) c = Color.Lerp(UiKit.Danger, Color.white, Blink(2.5f) * 0.6f);
                    else c = Color.Lerp(UiKit.TextColor, accent, 0.2f);
                    CardGui.Box(pip, c);
                    if (full && !justLost) CardGui.Box(new Rect(pip.x, pip.y, pip.width, 3f), UiKit.WithAlpha(Color.white, 0.45f));
                }
            }

            // The seven colors, in play order, after a little heart filled with what's been restored.
            rowY = panel.y + 82f;
            var bands = new Color[UiKit.Spectrum.Length];
            for (int i = 0; i < bands.Length; i++) bands[i] = UiKit.Restored(UiKit.Spectrum[i]) ? UiKit.Accent(UiKit.Spectrum[i]) : UiKit.Gray;
            UiKit.Heart(UiKit.Snap(new Rect(x + 2f, rowY - 15f, 32f, 30f)), bands, UiKit.WithAlpha(accent, 0.6f), 1f);
            var order = Game.Config ? Game.Config.colorOrder : null;
            for (int i = 0; order != null && i < order.Count; i++)
            {
                var data = order[i];
                if (!data) continue;
                var c = new Vector2(x + 64f + i * 34f, rowY);
                bool lit = UiKit.Restored(data.id);
                float pop = restoredAt.TryGetValue(data.id, out var t) ? Mathf.Clamp01(1f - (Now - t) / 1.5f) : 0f;
                bool here = LevelController.Current && LevelController.Current.Color == data.id;
                UiKit.Gem(c, 26f, UiKit.Accent(data.id), lit, pop + (here && lit ? 0.3f : 0f));
                if (here) CardGui.Box(UiKit.Snap(new Rect(c.x - 8f, c.y + 17f, 16f, 3f)), UiKit.Accent(data.id));
            }

            // Abilities: three to a row, each appearing when it's unlocked.
            float gridTop = panel.y + GridTop;
            for (int i = 0; i < have.Count; i++)
            {
                var slot = new Rect(x + (i % 3) * SlotWidth, gridTop + (i / 3) * SlotHeight, SlotWidth, SlotHeight);
                if (slot.yMax > panel.yMax + 4f) continue; // its row is still opening
                DrawSlot(have[i], slot, accent);
            }

            if (hasSerenity)
            {
                var row = new Rect(x, gridTop + rows * SlotHeight + 4f, PanelWidth - 40f, 40f);
                if (row.yMax <= panel.yMax + 4f) DrawSerenity(row);
            }
        }

        /// <summary>One ability: its keycap(s) (+ HOLD / x2) and its name under them, or STOLEN in red.</summary>
        void DrawSlot(AbilityId id, Rect slot, Color accent)
        {
            var info = UiKit.Ability(id);
            bool isStolen = stolen.Contains(id);
            var keyColor = isStolen ? UiKit.Danger : accent;

            // As big as fits the slot.
            float h = KeyHeight, width = UiKit.Keys(info.Keys, 0f, 0f, h, 0f, keyColor, false, UiKit.TextMin, info.Tag);
            for (int k = 0; k < 3 && width > slot.width - 8f; k++)
            {
                h = Mathf.Max(30f, h * (slot.width - 8f) / width);
                width = UiKit.Keys(info.Keys, 0f, 0f, h, 0f, keyColor, false, UiKit.TextMin, info.Tag);
            }
            float keyY = slot.y + 28f;
            float kx = slot.center.x - width * 0.5f;
            var center = new Vector2(slot.center.x, keyY);

            // A newly unlocked one fades in; the glow ring below marks it.
            float a = CardGui.Alpha;
            if (unlockedAt.TryGetValue(id, out var ut)) CardGui.Alpha *= Mathf.Clamp01((Now - ut) / 0.35f);
            float stolenFlash = isStolen && stolenAt.TryGetValue(id, out var st) ? Mathf.Clamp01(1f - (Now - st) / 0.6f) : 0f;
            if (isStolen) CardGui.Glow(center, 40f, UiKit.WithAlpha(UiKit.Danger, 0.25f + 0.5f * stolenFlash));
            UiKit.Keys(info.Keys, kx, keyY, h, 0f, keyColor, true, UiKit.TextMin, info.Tag);
            if (isStolen)
            {
                float keysOnly = UiKit.Keys(info.Keys, 0f, 0f, h, 0f, keyColor, false, UiKit.TextMin);
                CardGui.Line(new Vector2(kx - 4f, keyY + h * 0.5f + 2f), new Vector2(kx + keysOnly + 4f, keyY - h * 0.5f - 2f), 3f, UiKit.Danger);
            }
            var nameColor = isStolen ? Color.Lerp(UiKit.Danger, Color.white, Blink(2f) * 0.35f) : UiKit.TextColor;
            UiKit.Label(new Rect(slot.x, slot.y + 52f, slot.width, 32f), isStolen ? "STOLEN" : info.Short, UiKit.TextMin, nameColor);
            CardGui.Alpha = a;

            float back = returnedAt.TryGetValue(id, out var rt) ? (Now - rt) / 0.5f : 2f;
            float fresh = unlockedAt.TryGetValue(id, out var ut2) ? (Now - ut2) / 0.8f : 2f;
            float pop = Mathf.Min(back, fresh);
            if (pop >= 0f && pop < 1f) CardGui.Ring(center, 22f + 36f * pop, 3f * (1f - pop) + 1f, UiKit.WithAlpha(accent, 1f - pop));
        }

        void DrawSerenity(Rect row)
        {
            var core = new Color(0.48f, 0.42f, 1f);
            float denied = Mathf.Clamp01(1f - (Now - deniedAt) / 0.35f);
            row.x += denied * Mathf.Sin(Now * 70f) * 5f;

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
                    fill = new Color(0.4f, 0.38f, 0.58f);
                    state = "RECHARGING";
                    break;
                default:
                    float pop = Mathf.Clamp01(1f - (Now - readyAt) / 0.5f);
                    fill = Color.Lerp(core, Color.white, 0.15f * pulse + 0.6f * pop);
                    state = "READY";
                    break;
            }
            fill = Color.Lerp(fill, UiKit.Danger, denied);

            var bar = UiKit.Snap(new Rect(row.x, row.center.y - 7f, 220f, 14f));
            if (serenity == SerenityState.Ready) CardGui.Glow(bar.center, 120f, UiKit.WithAlpha(core, 0.15f + 0.1f * pulse));
            UiKit.SegmentBar(bar, serenityFraction, 12, fill, new Color(0.14f, 0.14f, 0.2f, 0.95f), 3f);
            float textX = bar.xMax + 14f;
            var textColor = serenity == SerenityState.Recharging ? UiKit.TextColor : Color.Lerp(fill, Color.white, 0.4f);
            UiKit.Label(new Rect(textX, row.y, 170f, row.height), state, UiKit.TextMin, textColor, TextAnchor.MiddleLeft);
            if (serenity == SerenityState.Ready)
                UiKit.Keys(new[] { "Q" }, textX + UiKit.LabelWidth(state, UiKit.TextMin) + 12f, row.center.y, 34f, 0f, core, true, UiKit.TextMin);
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

        /// <summary>0..1..0 at `hz` blinks a second (kept under ~3 for comfort).</summary>
        static float Blink(float hz) => 0.5f + 0.5f * Mathf.Sin(Now * hz * Mathf.PI * 2f);
    }
}
