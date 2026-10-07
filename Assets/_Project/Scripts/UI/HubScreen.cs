using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The hub, where you choose the next district (IMGUI, UiKit look). Lives in the Hub scene; it only reads the save
    /// and calls Game.Manager (EnterLevel, ReturnToMenu, NewGame to reset).
    ///   The heart, in the middle: seven bands (red at the top to violet at the tip), gray until that color is restored.
    ///   It beats slowly; the next color to reclaim flickers in its band (GlitchClock); the selected district's band
    ///   lights up and is named beside it. Under it: "N / 7" COLORS RESTORED.
    ///   The districts, a row of cards in play order, each framed in its color, showing only its name and number, its
    ///   boss and its state: locked = gray and static-y (and why), available = its color around a gray boss (the color
    ///   isn't back yet), restored = full color, gem lit. The selected card grows. A "NEXT" tag marks the color to reclaim.
    ///   Ambience: a night backdrop that picks up the restored colors, slow dust, faint scanlines, frame corners.
    ///   Corners: "Main menu" (bottom left) and a quiet "Reset progress" (bottom right) that asks before erasing the save.
    /// Made for the mouse: hover selects (with a little glitch slide), a click enters. The keyboard works too, silently
    /// (Left / Right, Z / Enter, Esc). Every label is UiKit.Label: big enough, outlined, crisp. HubTitle isn't drawn.
    /// </summary>
    public class HubScreen : MonoBehaviour
    {
        const float CardW = 196f, CardH = 300f, FocusW = 344f, FocusH = 420f, Gap = 16f, CardsBottom = 996f;
        static readonly Color LockedColor = new(0.34f, 0.35f, 0.39f);
        static readonly Color LockedText = new(0.78f, 0.8f, 0.84f);
        static readonly Color GrayBand = new(0.2f, 0.21f, 0.25f);
        static readonly Rect MenuButton = new(56f, 1010f, 250f, 52f);
        static readonly Rect ResetButton = new(1920f - 56f - 250f, 1010f, 250f, 52f);
        static readonly Rect Dialog = new(960f - 420f, 540f - 170f, 840f, 340f);
        static readonly Rect EraseButton = new(Dialog.x + 50f, Dialog.yMax - 116f, 430f, 76f);
        static readonly Rect CancelButton = new(Dialog.xMax - 50f - 270f, Dialog.yMax - 116f, 270f, 76f);

        readonly MenuNav nav = new();
        readonly GlitchClock heartGlitch = new() { MinGap = 1.5f, MaxGap = 3.4f };
        readonly GlitchClock cardGlitch = new() { MinGap = 0f, MaxGap = 0f }; // kicked by hand, on each new selection
        int selected = -1;
        float[] focus = new float[0];
        float openedAt = -1f, lockedAt = -10f, enterAt = -10f, dialogAt = -10f;
        Vector2 lastMouse;
        bool entering, confirming;
        int choice = 1; // in the dialog: 0 = erase, 1 = cancel (the safe one)

        static float Now => Time.unscaledTime;
        static List<ColorData> Order => Game.Config ? Game.Config.colorOrder : null;

        void Update()
        {
            var order = Order;
            if (order == null || order.Count == 0 || !Game.Manager) return;
            if (openedAt < 0f) openedAt = Now;
            if (focus.Length != order.Count) focus = new float[order.Count];
            if (selected < 0 || selected >= order.Count)
            {
                selected = DefaultSelection(order);
                focus[selected] = 1f;
            }

            heartGlitch.Tick(Now);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < focus.Length; i++) focus[i] = Mathf.MoveTowards(focus[i], i == selected ? 1f : 0f, dt / 0.2f);

            if (!Game.Input || entering || Game.Scenes.IsLoading) return;
            var intent = Game.Input.Intent;
            int step = nav.Step(Game.Input.Navigate.x);
            if (confirming)
            {
                if (step != 0) choice = Mathf.Clamp(choice + step, 0, 1);
                if (intent.confirmPressed && Now - dialogAt > 0.3f) Answer(choice == 0);
                else if (intent.pausePressed) Answer(false);
                return;
            }
            if (step != 0) Select(Mathf.Clamp(selected + step, 0, order.Count - 1));
            if (intent.confirmPressed && Now - openedAt > 0.3f) Enter(selected);
            else if (intent.pausePressed) Game.Manager.ReturnToMenu();
        }

        /// <summary>The next color to reclaim, or the last one you can play when everything's done.</summary>
        static int DefaultSelection(List<ColorData> order)
        {
            int last = 0;
            for (int i = 0; i < order.Count; i++)
            {
                if (!order[i] || !Game.Manager.IsUnlocked(order[i].id)) continue;
                if (!UiKit.Restored(order[i].id)) return i;
                last = i;
            }
            return last;
        }

        void Select(int i)
        {
            if (i == selected) return;
            selected = i;
            cardGlitch.Kick(Now, 0.18f);
        }

        void Enter(int i)
        {
            var data = Order[i];
            if (!data) return;
            if (!Game.Manager.IsUnlocked(data.id))
            {
                lockedAt = Now; // it shakes its head
                return;
            }
            entering = true;
            enterAt = Now;
            Game.Manager.EnterLevel(data.id);
        }

        void OpenDialog()
        {
            confirming = true;
            choice = 1;
            dialogAt = Now;
        }

        void Answer(bool erase)
        {
            confirming = false;
            if (!erase) return;
            entering = true; // ignore input while the hub reloads
            enterAt = -10f;  // (no "entering" flash on a card)
            Game.Manager.NewGame(); // wipes the save and comes back to a fresh hub
        }

        // ---------- Drawing ----------

        void OnGUI()
        {
            var order = Order;
            if (order == null || focus.Length != order.Count || selected < 0) return;
            var e = Event.current;
            var full = UiKit.Fill();
            var view = UiKit.Fit();
            var cards = CardRects(order.Count);
            var mouse = view.Mouse;

            if (e.type == EventType.MouseDown && e.button == 0 && !entering)
            {
                if (confirming)
                {
                    if (EraseButton.Contains(mouse)) Answer(true);
                    else if (CancelButton.Contains(mouse)) Answer(false);
                    e.Use();
                    return;
                }
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i].Contains(mouse)) { Select(i); Enter(i); e.Use(); return; }
                if (MenuButton.Contains(mouse)) { Game.Manager.ReturnToMenu(); e.Use(); return; }
                if (ResetButton.Contains(mouse)) { OpenDialog(); e.Use(); return; }
            }
            if (e.type != EventType.Repaint) return;
            bool moved = (mouse - lastMouse).sqrMagnitude > 1f;
            lastMouse = mouse;
            if (moved && !entering)
            {
                if (confirming)
                {
                    if (EraseButton.Contains(mouse)) choice = 0;
                    else if (CancelButton.Contains(mouse)) choice = 1;
                }
                else
                    for (int i = 0; i < cards.Length; i++)
                        if (cards[i].Contains(mouse)) Select(i);
            }

            full.Begin();
            DrawBackdrop(full.Rect);

            view.Begin();
            CardGui.Alpha = UiKit.Smooth((Now - openedAt) / 0.6f);
            UiKit.Notches(new Rect(24f, 18f, 1872f, 1050f), UiKit.WithAlpha(UiKit.Neutral, 0.3f), 44f, 3f);
            int next = NextIndex(order);
            DrawHeart(order, next);
            for (int i = 0; i < order.Count; i++)
                if (i != selected) DrawCard(order, i, cards[i], next);
            DrawCard(order, selected, cards[selected], next); // the selected one in front

            bool free = !confirming && !entering;
            UiKit.Button(MenuButton, "Main menu", UiKit.Neutral, free && MenuButton.Contains(mouse), true, UiKit.TextMin, Now);
            UiKit.Button(ResetButton, "Reset progress", UiKit.Gray, free && ResetButton.Contains(mouse), true, UiKit.TextMin, Now);

            if (confirming) DrawDialog(full);
            UiKit.End();
        }

        Rect[] CardRects(int n)
        {
            var rects = new Rect[n];
            var w = new float[n];
            var h = new float[n];
            float total = Gap * (n - 1);
            for (int i = 0; i < n; i++)
            {
                float f = UiKit.Smooth(focus[i]);
                w[i] = Mathf.Lerp(CardW, FocusW, f);
                h[i] = Mathf.Lerp(CardH, FocusH, f);
                total += w[i];
            }
            float x = UiKit.RefWidth * 0.5f - total * 0.5f;
            for (int i = 0; i < n; i++)
            {
                // They rise into place one after another when the hub opens; the selected one floats a little.
                float rise = (1f - UiKit.Smooth((Now - openedAt - 0.15f - i * 0.05f) / 0.45f)) * 60f;
                float bob = i == selected ? Mathf.Sin(Now * 1.6f) * 2.5f : 0f;
                rects[i] = new Rect(x, CardsBottom - h[i] + rise + bob, w[i], h[i]);
                x += w[i] + Gap;
            }
            return rects;
        }

        static void DrawBackdrop(Rect r)
        {
            var palette = UiKit.RestoredColors();
            var tint = UiKit.Average(palette, UiKit.Gray);
            UiKit.Gradient(r, UiKit.Night, UiKit.Backdrop(tint, 0.05f + 0.1f * palette.Count / 7f));
            // Soft pools of each restored color along the bottom, drifting slowly.
            for (int i = 0; i < UiKit.Spectrum.Length; i++)
            {
                var id = UiKit.Spectrum[i];
                if (!UiKit.Restored(id)) continue;
                float x = r.x + r.width * (i + 0.5f) / UiKit.Spectrum.Length + Mathf.Sin(Now * 0.2f + i * 1.3f) * 40f;
                CardGui.Glow(new Vector2(x, r.yMax - r.height * 0.08f), r.height * 0.42f, UiKit.WithAlpha(UiKit.Accent(id), 0.08f));
            }
            var dust = new List<Color>(palette) { UiKit.Gray, UiKit.Gray };
            UiKit.Dust(r, Now, 70, UiKit.Gray, dust, 0.17f);
            UiKit.Scanlines(r, new Color(1f, 1f, 1f, 0.018f), 4f, 1f);
            UiKit.Vignette(r, new Color(0f, 0f, 0f, 0.75f));
        }

        void DrawHeart(List<ColorData> order, int next)
        {
            float beat = UiKit.Heartbeat(Now);
            var size = new Vector2(300f, 280f) * (1f + 0.025f * beat);
            var r = new Rect(960f - size.x * 0.5f, 286f - size.y * 0.5f, size.x, size.y);
            var palette = UiKit.RestoredColors();
            var tint = UiKit.Average(palette, UiKit.Gray);
            var selId = order[selected] ? order[selected].id : ColorId.Red;
            var nextId = next >= 0 && order[next] ? order[next].id : (ColorId?)null;

            // A slow ring of ticks around it, like a gauge.
            CardGui.Glow(r.center, 270f, UiKit.WithAlpha(tint, 0.1f + 0.06f * beat));
            CardGui.Ring(r.center, 206f, 1.5f, UiKit.WithAlpha(UiKit.Neutral, 0.14f));
            for (int i = 0; i < 48; i++)
            {
                float a = (i * 7.5f + Now * 4f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float len = i % 4 == 0 ? 12f : 5f;
                CardGui.Line(r.center + dir * 212f, r.center + dir * (212f + len), 1.5f, UiKit.WithAlpha(UiKit.Neutral, i % 4 == 0 ? 0.3f : 0.15f));
            }

            float g = heartGlitch.Amount(Now);
            var bands = new Color[UiKit.Spectrum.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                var id = UiKit.Spectrum[i];
                var accent = UiKit.Accent(id);
                var c = UiKit.Restored(id) ? Color.Lerp(accent, Color.white, 0.12f * beat) : GrayBand;
                if (nextId == id) c = Color.Lerp(c, accent, 0.18f * (0.5f + 0.5f * Mathf.Sin(Now * 2f)) + 0.85f * g); // trying to come back
                if (id == selId) c = Color.Lerp(c, UiKit.Restored(id) ? Color.white : accent, 0.22f);
                bands[i] = c;
            }
            if (g > 0.05f && nextId.HasValue)
            {
                var jolt = (heartGlitch.Rand(1) - 0.5f) * 10f * g;
                UiKit.HeartShape(new Rect(r.x - 6f * g + jolt, r.y, r.width, r.height), new Color(1f, 0.2f, 0.35f, 0.3f * g));
                UiKit.HeartShape(new Rect(r.x + 6f * g + jolt, r.y, r.width, r.height), new Color(0.2f, 0.9f, 1f, 0.3f * g));
            }
            UiKit.Heart(r, bands, UiKit.WithAlpha(Color.Lerp(UiKit.Neutral, UiKit.Accent(selId), 0.5f), 0.6f));

            // The selected district's band, named beside the heart.
            int band = System.Array.IndexOf(UiKit.Spectrum, selId);
            if (band >= 0)
            {
                var at = UiKit.HeartBand(r, band);
                var from = new Vector2(r.xMax - 8f, at.y);
                var to = new Vector2(r.xMax + 70f, at.y);
                var accent = UiKit.Accent(selId);
                CardGui.Line(from, to, 2f, UiKit.WithAlpha(accent, 0.8f));
                CardGui.Diamond(from, 9f, accent);
                UiKit.Label(new Rect(to.x + 12f, at.y - 18f, 320f, 36f), order[selected].displayName.ToUpperInvariant(), 26,
                            Color.Lerp(accent, Color.white, 0.3f), TextAnchor.MiddleLeft);
            }

            int restored = UiKit.RestoredCount;
            UiKit.Label(new Rect(660f, 438f, 600f, 52f), $"{restored} / {UiKit.Spectrum.Length}", 44, UiKit.TextColor);
            UiKit.Label(new Rect(660f, 486f, 600f, 34f), "COLORS RESTORED", 26, UiKit.TextColor);
        }

        void DrawCard(List<ColorData> order, int i, Rect r, int next)
        {
            var data = order[i];
            if (!data) return;
            var id = data.id;
            bool restored = UiKit.Restored(id), locked = !Game.Manager.IsUnlocked(id), sel = i == selected;
            float f = UiKit.Smooth(focus[i]);
            var accent = UiKit.Accent(id);
            var frame = locked ? LockedColor : accent;
            var bright = Color.Lerp(accent, Color.white, 0.3f);

            // A new selection slides in with a little glitch; a locked one shakes its head when you try it.
            float g = sel ? cardGlitch.Amount(Now) : 0f;
            float sinceLocked = Now - lockedAt;
            if (sel && sinceLocked < 0.35f) r.x += Mathf.Sin(sinceLocked * 80f) * 8f * (1f - sinceLocked / 0.35f);
            if (g > 0f) r.x += (cardGlitch.Rand(i) - 0.5f) * 16f * g;
            if (g > 0.05f)
            {
                CardGui.Outline(new Rect(r.x - 6f * g, r.y, r.width, r.height), new Color(1f, 0.2f, 0.35f, 0.5f * g), 2f);
                CardGui.Outline(new Rect(r.x + 6f * g, r.y, r.width, r.height), new Color(0.2f, 0.9f, 1f, 0.5f * g), 2f);
            }

            // The frame and the boss dim on cards you're not looking at; the words never do.
            float a = CardGui.Alpha;
            if (!sel) CardGui.Alpha *= locked ? 0.75f : 0.92f;
            if (sel) CardGui.Glow(r.center, r.width, UiKit.WithAlpha(frame, 0.12f));
            UiKit.Panel(r, frame, sel ? Now : -1f, Mathf.Lerp(14f, 24f, f), 0.9f);
            float headerH = Mathf.Lerp(40f, 48f, f);
            var header = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, headerH);
            CardGui.Box(header, UiKit.WithAlpha(frame, 0.16f));
            CardGui.Box(new Rect(r.x, header.yMax, r.width, 2f), UiKit.WithAlpha(frame, 0.6f));
            CardGui.Box(new Rect(r.x + 14f, header.y + headerH * 0.28f, 6f, headerH * 0.44f), frame);

            var screen = new Rect(r.x + 14f, header.yMax + 12f, r.width - 28f, Mathf.Lerp(150f, 250f, f));
            CardGui.Box(screen, new Color(0f, 0f, 0f, 0.38f));
            for (float gx = screen.x + 20f; gx < screen.xMax; gx += 20f) CardGui.Box(new Rect(gx, screen.y, 1f, screen.height), new Color(1f, 1f, 1f, 0.03f));
            for (float gy = screen.y + 20f; gy < screen.yMax; gy += 20f) CardGui.Box(new Rect(screen.x, gy, screen.width, 1f), new Color(1f, 1f, 1f, 0.03f));
            var stage = new Rect(screen.x + 12f, screen.y + 12f, screen.width - 24f, screen.height - 20f);
            if (locked)
            {
                DemoBosses.Portrait(id, stage, Now, new Color(0.3f, 0.31f, 0.35f));
                Static(screen, i);
            }
            else
            {
                DemoBosses.Portrait(id, stage, Now);
                if (!restored) // the color isn't back yet: gray haze, lifting for a moment when it glitches
                {
                    float bleed = i == next ? heartGlitch.Amount(Now) : 0f;
                    CardGui.Box(screen, new Color(0.3f, 0.31f, 0.35f, 0.55f * (1f - 0.8f * bleed)));
                }
            }
            CardGui.Outline(screen, UiKit.WithAlpha(frame, 0.35f), 1.5f);
            if (restored) UiKit.Gem(new Vector2(screen.xMax - 18f, screen.y + 18f), 22f, accent, true, 0.2f);
            CardGui.Alpha = a;

            // Name and number.
            int nameSize = Mathf.RoundToInt(Mathf.Lerp(UiKit.TextMin, 28f, f));
            UiKit.Label(new Rect(header.x + 28f, header.y, header.width - 80f, header.height), data.displayName.ToUpperInvariant(), nameSize,
                        locked ? LockedText : bright, TextAnchor.MiddleLeft);
            UiKit.Label(new Rect(header.xMax - 54f, header.y, 46f, header.height), (i + 1).ToString("00"), UiKit.TextMin,
                        locked ? LockedText : UiKit.TextColor, TextAnchor.MiddleRight);

            // The state: one word on a small card; a word and a line on the selected one.
            string word = restored ? "RESTORED" : locked ? "LOCKED" : "AVAILABLE";
            var wordColor = restored ? bright : locked ? LockedText : UiKit.TextColor;
            float below = screen.yMax + 10f;
            if (f < 0.5f)
            {
                UiKit.Gem(new Vector2(r.center.x, below + 16f), 24f, accent, restored, restored ? 0.2f : 0f);
                UiKit.Label(new Rect(r.x, below + 32f, r.width, 34f), word, UiKit.TextMin, wordColor);
            }
            else
            {
                UiKit.Label(new Rect(r.x, below + 4f, r.width, 40f), word, 30, wordColor);
                string line = locked ? Reason(order, i) : restored ? "Click to replay" : "Click to enter";
                float pulse = locked ? 1f : 0.8f + 0.2f * Mathf.Sin(Now * 3f);
                float la = CardGui.Alpha;
                CardGui.Alpha *= pulse;
                UiKit.Label(new Rect(r.x, below + 44f, r.width, 34f), line, UiKit.TextLabel, locked ? LockedText : UiKit.TextColor, TextAnchor.MiddleCenter, false);
                CardGui.Alpha = la;
            }

            // The color to reclaim next.
            if (i == next && !locked)
            {
                float bounce = Mathf.Sin(Now * 3f) * 3f;
                UiKit.Label(new Rect(r.center.x - 70f, r.y - 48f + bounce, 140f, 30f), "NEXT", UiKit.TextMin, bright);
                var tip = new Vector2(r.center.x, r.y - 12f + bounce);
                CardGui.Line(tip + new Vector2(-9f, -9f), tip, 3f, bright);
                CardGui.Line(tip + new Vector2(9f, -9f), tip, 3f, bright);
            }

            // Entering: a flash and a ring as the scene starts to fade.
            if (entering && sel && !confirming)
            {
                float k = Mathf.Clamp01((Now - enterAt) / 0.4f);
                CardGui.Box(r, UiKit.WithAlpha(Color.white, 0.45f * (1f - k)));
                CardGui.Ring(r.center, 40f + 300f * k, 4f * (1f - k) + 1f, UiKit.WithAlpha(accent, 1f - k));
            }
        }

        /// <summary>"Start over?": erase the save (GameManager.NewGame) or cancel. Over a dimmed hub.</summary>
        void DrawDialog(UiKit.View full)
        {
            float t = Now - dialogAt;
            float a = CardGui.Alpha;
            var screen = GUI.matrix;
            full.Begin();
            CardGui.Alpha = UiKit.Smooth(t / 0.15f);
            CardGui.Box(full.Rect, new Color(0f, 0f, 0f, 0.65f));
            GUI.matrix = screen;

            var danger = UiKit.Danger;
            UiKit.Panel(Dialog, danger, Now, 26f, 0.95f);
            UiKit.Header(Dialog, "Start over?", danger, 56f, UiKit.TextTitle, true, true);
            UiKit.Label(new Rect(Dialog.x + 40f, Dialog.y + 100f, Dialog.width - 80f, 60f), "Your restored colors will be lost.", UiKit.TextTitle,
                        Color.white, TextAnchor.MiddleCenter, false);
            UiKit.Button(EraseButton, "Erase and start over", danger, choice == 0, true, 26, Now);
            UiKit.Button(CancelButton, "Cancel", UiKit.Neutral, choice == 1, true, 26, Now);
            CardGui.Alpha = a;
        }

        /// <summary>Why a district is locked: the first color before it that isn't restored yet.</summary>
        static string Reason(List<ColorData> order, int i)
        {
            for (int k = 0; k < i; k++)
                if (order[k] && !UiKit.Restored(order[k].id)) return $"Restore {order[k].displayName} first";
            return "Locked";
        }

        static int NextIndex(List<ColorData> order)
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i] && Game.Manager.IsUnlocked(order[i].id) && !UiKit.Restored(order[i].id)) return i;
            return -1;
        }

        /// <summary>A dead channel: specks of static and a rolling band (locked districts). Slow enough to stay calm.</summary>
        static void Static(Rect r, int seed)
        {
            int frame = Mathf.FloorToInt(Now * 10f);
            for (int k = 0; k < 46; k++)
            {
                float n = frame * 31.7f + k * 1.31f + seed * 7.1f;
                float w = 2f + 6f * UiKit.Hash(n + 3.3f), h = 1.5f + 1.5f * UiKit.Hash(n + 4.4f);
                var speck = new Rect(r.x + UiKit.Hash(n) * (r.width - w), r.y + UiKit.Hash(n + 1.7f) * (r.height - h), w, h);
                CardGui.Box(speck, new Color(0.62f, 0.63f, 0.68f, 0.1f + 0.22f * UiKit.Hash(n + 2.9f)));
            }
            float band = r.y + Mathf.Repeat(Now * 36f + seed * 40f, r.height - 8f);
            CardGui.Box(new Rect(r.x, band, r.width, 8f), new Color(1f, 1f, 1f, 0.05f));
        }
    }
}
