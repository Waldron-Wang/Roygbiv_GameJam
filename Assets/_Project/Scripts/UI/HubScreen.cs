using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The hub, where you choose the next district (IMGUI, UiKit look). Lives in the Hub scene; it only reads the save
    /// and calls Game.Manager.EnterLevel / ReturnToMenu.
    ///   The heart, in the middle: seven bands (red at the top to violet at the tip), gray until that color is restored.
    ///   It beats slowly; the next color to reclaim flickers in its band (GlitchClock); the selected district's band
    ///   lights up and is named beside it. Under it: "3 / 7 COLORS RESTORED".
    ///   The districts, a row of cards in play order, each framed in its color: locked = gray and static-y (and why),
    ///   available = its color around a gray boss (the color isn't back yet), restored = full color, gem lit.
    ///   The selected card grows into a preview: the boss (DemoBosses.Portrait), its emotion, the reward's keys and
    ///   what to do. A "NEXT" tag marks the color to reclaim.
    ///   Ambience: a night backdrop that picks up the restored colors, slow dust, faint scanlines, frame corners.
    ///   Input: Left / Right (or hovering) selects with a little glitch slide; Z / Enter (or a click) enters; Esc or the
    ///   button goes back to the main menu. Unscaled time.
    /// </summary>
    public class HubScreen : MonoBehaviour
    {
        const float CardW = 196f, CardH = 300f, FocusW = 344f, FocusH = 446f, Gap = 16f, CardsBottom = 1000f;
        static readonly Color LockedColor = new(0.34f, 0.35f, 0.39f);
        static readonly Color GrayBand = new(0.2f, 0.21f, 0.25f);

        readonly MenuNav nav = new();
        readonly GlitchClock heartGlitch = new() { MinGap = 1.5f, MaxGap = 3.4f };
        readonly GlitchClock cardGlitch = new() { MinGap = 0f, MaxGap = 0f }; // kicked by hand, on each new selection
        int selected = -1;
        float[] focus = new float[0];
        float openedAt = -1f, lockedAt = -10f, enterAt = -10f;
        Vector2 lastMouse;
        bool entering;

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
            int step = nav.Step(Game.Input.Navigate.x);
            if (step != 0) Select(Mathf.Clamp(selected + step, 0, order.Count - 1));
            var intent = Game.Input.Intent;
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

        // ---------- Drawing ----------

        void OnGUI()
        {
            var order = Order;
            if (order == null || focus.Length != order.Count || selected < 0) return;
            var e = Event.current;
            var full = UiKit.Fill();
            var view = UiKit.Fit();
            var cards = CardRects(order.Count);
            var menuButton = new Rect(56f, 1014f, 240f, 44f);

            if (e.type == EventType.MouseDown && e.button == 0 && !entering)
            {
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i].Contains(view.Mouse)) { Select(i); Enter(i); e.Use(); return; }
                if (menuButton.Contains(view.Mouse)) { Game.Manager.ReturnToMenu(); e.Use(); return; }
            }
            if (e.type != EventType.Repaint) return;
            bool moved = (view.Mouse - lastMouse).sqrMagnitude > 1f;
            lastMouse = view.Mouse;
            if (moved && !entering)
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i].Contains(view.Mouse)) Select(i);

            full.Begin();
            DrawBackdrop(full.Rect);

            view.Begin();
            CardGui.Alpha = UiKit.Smooth((Now - openedAt) / 0.6f);
            UiKit.Notches(new Rect(36f, 30f, 1848f, 1020f), UiKit.WithAlpha(UiKit.Neutral, 0.3f), 44f, 3f);
            DrawTitle();
            int next = NextIndex(order);
            DrawHeart(order, next);
            for (int i = 0; i < order.Count; i++)
                if (i != selected) DrawCard(order, i, cards[i], next);
            DrawCard(order, selected, cards[selected], next); // the selected one in front

            UiKit.Button(menuButton, "Main menu", UiKit.Neutral, menuButton.Contains(view.Mouse), true, 16, Now);
            UiKit.Hint(new Vector2(1080f, 1036f), "[Left] [Right] Select   [Z] Enter   [Esc] Main menu", UiKit.Neutral, 20, 0.85f);
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

        static void DrawTitle()
        {
            CardGui.Text(new Rect(84f, 52f, 900f, 22f), UiKit.Spaced(UiKit.GameTitle), 15, UiKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            CardGui.Box(new Rect(84f, 86f, 8f, 40f), UiKit.Neutral);
            CardGui.Text(new Rect(104f, 80f, 1100f, 52f), UiKit.Spaced(UiKit.HubTitle), 40, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            CardGui.Text(new Rect(106f, 132f, 900f, 28f), UiKit.HubSubtitle, 20, UiKit.TextDim, TextAnchor.MiddleLeft);
            CardGui.Box(new Rect(84f, 170f, 420f, 2f), UiKit.WithAlpha(UiKit.Neutral, 0.35f));
        }

        void DrawHeart(List<ColorData> order, int next)
        {
            float beat = UiKit.Heartbeat(Now);
            var size = new Vector2(300f, 280f) * (1f + 0.025f * beat);
            var r = new Rect(960f - size.x * 0.5f, 296f - size.y * 0.5f, size.x, size.y);
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
                CardGui.Text(new Rect(to.x + 10f, at.y - 14f, 300f, 28f), UiKit.Spaced(order[selected].displayName.ToUpperInvariant()), 17, accent,
                             TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            int restored = UiKit.RestoredCount;
            CardGui.Text(new Rect(760f, 456f, 400f, 44f), $"{restored} / {UiKit.Spectrum.Length}", 36, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            CardGui.Text(new Rect(660f, 496f, 600f, 24f), UiKit.Spaced("COLORS RESTORED"), 15, UiKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
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

            float a = CardGui.Alpha;
            if (!sel) CardGui.Alpha *= locked ? 0.72f : 0.94f;
            if (sel) CardGui.Glow(r.center, r.width, UiKit.WithAlpha(frame, 0.12f));
            UiKit.Panel(r, frame, sel ? Now : -1f, Mathf.Lerp(14f, 24f, f), 0.9f);
            var header = UiKit.Header(r, data.displayName, frame, Mathf.Lerp(36f, 44f, f), Mathf.RoundToInt(Mathf.Lerp(15f, 20f, f)));
            CardGui.Text(new Rect(header.xMax - 64f, header.y, 52f, header.height), (i + 1).ToString("00"), 14, UiKit.WithAlpha(frame, 0.8f),
                         TextAnchor.MiddleRight, FontStyle.Bold);

            // The boss, on a dark inset screen.
            var screen = new Rect(r.x + 14f, header.yMax + 12f, r.width - 28f, Mathf.Lerp(150f, 200f, f));
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

            // Compact (unselected): a gem and one word.
            float compact = 1f - f;
            if (compact > 0.01f)
            {
                float ca = CardGui.Alpha;
                CardGui.Alpha *= compact;
                float y = screen.yMax + 30f;
                UiKit.Gem(new Vector2(r.center.x, y), 24f, accent, restored, restored ? 0.2f : 0f);
                string word = restored ? "RESTORED" : locked ? "LOCKED" : "AVAILABLE";
                CardGui.Text(new Rect(r.x, y + 18f, r.width, 24f), UiKit.Spaced(word), 12, restored ? accent : locked ? UiKit.Gray : UiKit.TextDim,
                             TextAnchor.MiddleCenter, FontStyle.Bold);
                CardGui.Alpha = ca;
            }

            // Selected: the preview.
            if (f > 0.01f)
            {
                float fa = CardGui.Alpha;
                CardGui.Alpha *= f;
                float y = screen.yMax + 12f;
                CardGui.Text(new Rect(r.x + 16f, y, r.width - 32f, 26f), data.emotion, 17, UiKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Italic);
                y += 36f;

                CardGui.Box(new Rect(r.x + 20f, y, r.width - 40f, 1.5f), UiKit.WithAlpha(frame, 0.35f));
                y += 12f;
                if (data.grantedAbility != AbilityId.None)
                {
                    var info = UiKit.Ability(data.grantedAbility);
                    CardGui.Text(new Rect(r.x + 20f, y, 90f, 34f), UiKit.Spaced("REWARD"), 12, UiKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
                    float kx = r.x + 104f;
                    kx += UiKit.Keys(info.Keys, kx, y + 17f, 30f, 0f, locked ? UiKit.Gray : accent) + 10f;
                    CardGui.Text(new Rect(kx, y, r.xMax - kx - 12f, 34f), info.Name, 15, locked ? UiKit.Gray : UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                }
                else CardGui.Text(new Rect(r.x, y, r.width, 34f), UiKit.Spaced("THE FINAL COLOR"), 14, locked ? UiKit.Gray : accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                y += 46f;

                var state = new Rect(r.x + 16f, r.yMax - 52f, r.width - 32f, 38f);
                if (locked)
                {
                    CardGui.Text(new Rect(state.x, state.y - 18f, state.width, 24f), UiKit.Spaced("LOCKED"), 15, UiKit.Gray, TextAnchor.MiddleCenter, FontStyle.Bold);
                    CardGui.Text(new Rect(state.x, state.y + 8f, state.width, 24f), Reason(order, i), 15, UiKit.TextDim, TextAnchor.MiddleCenter);
                }
                else
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Now * 3f);
                    string line = restored ? "RESTORED   [Z] Replay" : "[Z] Enter";
                    UiKit.Hint(new Vector2(state.center.x, state.center.y), line, accent, 20, restored ? 1f : pulse);
                    if (restored) UiKit.Gem(new Vector2(r.xMax - 34f, header.yMax + 34f), 22f, accent, true, 0.3f);
                }
                CardGui.Alpha = fa;
            }

            // The color to reclaim next.
            if (i == next && !locked)
            {
                float bounce = Mathf.Sin(Now * 3f) * 3f;
                CardGui.Text(new Rect(r.center.x - 60f, r.y - 40f + bounce, 120f, 20f), UiKit.Spaced("NEXT"), 13, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                var tip = new Vector2(r.center.x, r.y - 12f + bounce);
                CardGui.Line(tip + new Vector2(-8f, -8f), tip, 2.5f, accent);
                CardGui.Line(tip + new Vector2(8f, -8f), tip, 2.5f, accent);
            }

            // Entering: a flash and a ring as the scene starts to fade.
            if (entering && sel)
            {
                float k = Mathf.Clamp01((Now - enterAt) / 0.4f);
                CardGui.Box(r, UiKit.WithAlpha(Color.white, 0.45f * (1f - k)));
                CardGui.Ring(r.center, 40f + 300f * k, 4f * (1f - k) + 1f, UiKit.WithAlpha(accent, 1f - k));
            }
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
