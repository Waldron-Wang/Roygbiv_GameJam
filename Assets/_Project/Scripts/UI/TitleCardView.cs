using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The title card (IMGUI, UiKit look): whenever GameEvents.TitleCardShown is raised (Violet's name as he draws his
    /// sword, his phase names), a band across the screen in the level's color, a big letter-spaced title whose letters
    /// close in as it appears, and the subtitle under it. Fades in, holds, fades out. Listens only. Unscaled time, so it
    /// reads the same in slow motion or while paused.
    /// </summary>
    public class TitleCardView : MonoBehaviour
    {
        [SerializeField] float fadeIn = 0.35f;
        [SerializeField] float hold = 1.6f;
        [SerializeField] float fadeOut = 0.6f;

        string title, subtitle;
        float shownAt = -100f;

        void OnEnable()
        {
            GameEvents.TitleCardShown += Show;
            GameEvents.SceneLoaded += Hide;
        }

        void OnDisable()
        {
            GameEvents.TitleCardShown -= Show;
            GameEvents.SceneLoaded -= Hide;
        }

        void Show(string newTitle, string newSubtitle)
        {
            title = newTitle;
            subtitle = newSubtitle;
            shownAt = Time.unscaledTime;
        }

        void Hide(string _) => shownAt = -100f;

        void OnGUI()
        {
            float t = Time.unscaledTime - shownAt;
            if (string.IsNullOrEmpty(title) || t > fadeIn + hold + fadeOut || Event.current.type != EventType.Repaint) return;
            GUI.depth = -60; // over the HUD and dialogue
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            a = UiKit.Smooth(a);

            var view = UiKit.Fill();
            view.Begin();
            var accent = UiKit.CurrentAccent;
            var light = Color.Lerp(accent, Color.white, 0.35f);
            const int size = 120;
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            float bandH = hasSub ? 250f : 196f;
            var band = new Rect(0f, view.Rect.height * 0.28f - 30f, view.Rect.width, bandH);

            // The band opens from its middle line.
            float open = UiKit.Smooth(t / (fadeIn * 0.8f));
            var shownBand = band;
            shownBand.height = Mathf.Max(3f, band.height * open);
            shownBand.y += (band.height - shownBand.height) * 0.5f;
            CardGui.Alpha = a;
            CardGui.Box(shownBand, new Color(0f, 0f, 0f, 0.62f));
            CardGui.Box(shownBand, UiKit.WithAlpha(accent, 0.08f));
            UiKit.Scanlines(shownBand, UiKit.WithAlpha(accent, 0.05f));
            CardGui.Box(new Rect(0f, shownBand.y, band.width, 2f), UiKit.WithAlpha(accent, 0.9f));
            CardGui.Box(new Rect(0f, shownBand.yMax - 2f, band.width, 2f), UiKit.WithAlpha(accent, 0.9f));

            // The letters close in from wide apart; notches frame the title.
            float spread = 1f - UiKit.Smooth(t / (fadeIn + 0.25f));
            float tracking = size * (0.22f + 0.5f * spread);
            float total = 0f;
            var widths = new float[title.Length];
            for (int i = 0; i < title.Length; i++)
            {
                widths[i] = title[i] == ' ' ? size * 0.5f : CardGui.Measure(title[i].ToString(), size, FontStyle.Bold).x;
                total += widths[i] + (i < title.Length - 1 ? tracking : 0f);
            }
            float cy = band.y + (hasSub ? 100f : band.height * 0.5f);
            float x = band.center.x - total * 0.5f;
            var frame = new Rect(band.center.x - total * 0.5f - 60f, band.y + 22f, total + 120f, band.height - 44f);
            UiKit.Notches(frame, UiKit.WithAlpha(accent, open), 34f, 4f);
            for (int i = 0; i < title.Length; i++)
            {
                if (title[i] != ' ')
                {
                    var r = new Rect(x - 6f, cy - size, widths[i] + 12f, size * 2f);
                    CardGui.Text(new Rect(r.x + 4f, r.y + 4f, r.width, r.height), title[i].ToString(), size, new Color(0f, 0f, 0f, 0.7f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    CardGui.Text(r, title[i].ToString(), size, light, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                x += widths[i] + tracking;
            }
            if (hasSub)
                CardGui.Text(new Rect(0f, cy + size * 0.62f, band.width, 40f), subtitle, 30, UiKit.WithAlpha(UiKit.TextColor, 0.88f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.End();
        }
    }
}
