using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// PLACEHOLDER title card (IMGUI): a big centered title with a subtitle that fades in, holds and fades out,
    /// whenever GameEvents.TitleCardShown is raised (Violet's name in its intro, its phase names).
    /// Listens only. Unscaled time, so it reads the same in slow motion or while paused.
    /// </summary>
    public class TitleCardView : MonoBehaviour
    {
        [SerializeField] float fadeIn = 0.35f;
        [SerializeField] float hold = 1.6f;
        [SerializeField] float fadeOut = 0.6f;
        [SerializeField] Color titleColor = new(0.9f, 0.75f, 1f);

        string title, subtitle;
        float shownAt = -100f;
        GUIStyle titleStyle, subStyle;

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
            if (string.IsNullOrEmpty(title) || t > fadeIn + hold + fadeOut) return;
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a));

            int big = Mathf.Max(32, Screen.height / 7);
            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            subStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            titleStyle.fontSize = big;
            subStyle.fontSize = Mathf.Max(14, big / 4);

            // The letters spread apart as it comes in.
            float spread = 1f + 0.15f * (1f - a);
            var area = new Rect(0f, Screen.height * 0.28f, Screen.width, big * 1.3f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f * a);
            GUI.DrawTexture(new Rect(0f, area.y - big * 0.1f, Screen.width, big * 1.9f), Texture2D.whiteTexture);
            var spaced = string.Join(spread > 1.05f ? "  " : " ", title.ToCharArray());
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.Label(new Rect(area.x + 3f, area.y + 3f, area.width, area.height), spaced, titleStyle);
            GUI.color = new Color(titleColor.r, titleColor.g, titleColor.b, a);
            GUI.Label(area, spaced, titleStyle);
            if (!string.IsNullOrEmpty(subtitle))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.85f * a);
                GUI.Label(new Rect(0f, area.yMax - big * 0.1f, Screen.width, big * 0.5f), subtitle, subStyle);
            }
            GUI.color = Color.white;
        }
    }
}
