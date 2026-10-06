using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// PLACEHOLDER Serenity look + HUD meter (IMGUI). It only LISTENS to GameEvents.SerenityChanged / SerenityDenied
    /// and reads the player's position for where the ripples start; gameplay never calls it.
    ///   Look:  when Serenity starts a soft ring ripples out from the player and the screen eases into a calm indigo
    ///          vignette with slightly washed-out colors (SerenityFilter on the main camera); when it ends, a gentle
    ///          wider ring and the look eases back out.
    ///   Meter: top left, under the HUD. Active = a bright bar draining; recharging = a dim bar filling up;
    ///          ready = full and glowing with READY. A press while it isn't ready shakes it red for a moment.
    /// Everything runs on unscaled time, so it stays smooth in the slow motion and keeps animating while paused.
    /// </summary>
    public class SerenityView : MonoBehaviour
    {
        [SerializeField] Color activeColor = new(0.62f, 0.55f, 1f);
        [SerializeField] Color readyColor = new(0.48f, 0.42f, 1f);
        [SerializeField] Color rechargeColor = new(0.32f, 0.3f, 0.48f);
        [SerializeField] Color deniedColor = new(1f, 0.35f, 0.4f);
        [Tooltip("Seconds the screen look takes to come in / go out (unscaled).")]
        [SerializeField] float easeIn = 0.35f;
        [SerializeField] float easeOut = 0.8f;

        SerenityState state = SerenityState.Unavailable;
        float fraction, look, deniedAt = -10f, readyAt = -10f;
        SerenityFilter filter;
        GUIStyle label;

        void OnEnable()
        {
            GameEvents.SerenityChanged += OnChanged;
            GameEvents.SerenityDenied += OnDenied;
            GameEvents.SceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.SerenityChanged -= OnChanged;
            GameEvents.SerenityDenied -= OnDenied;
            GameEvents.SceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(string _)
        {
            state = SerenityState.Unavailable;
            look = 0f;
            filter = null; // it lived on the old scene's camera
        }

        void OnDenied() => deniedAt = Time.unscaledTime;

        void OnChanged(SerenityState newState, float newFraction)
        {
            var old = state;
            state = newState;
            fraction = newFraction;
            if (old == newState) return;

            if (newState == SerenityState.Active) Ring(true);
            else if (old == SerenityState.Active) Ring(false);
            if (newState == SerenityState.Ready && old == SerenityState.Recharging) readyAt = Time.unscaledTime;
        }

        void Ring(bool starting)
        {
            var player = PlayerController.Instance;
            var cam = Camera.main;
            if (!player || !cam) return;
            filter = SerenityFilter.On(cam);
            if (filter) filter.Ripple(player.transform.position, starting ? 0.9f : 0.5f, starting ? 0.9f : 1.3f);
            StartCoroutine(RingSprite(player.transform.position, starting));
        }

        /// <summary>A visible ring on top of the distortion: tight and bright going in, wide and soft letting go.</summary>
        IEnumerator RingSprite(Vector2 at, bool starting)
        {
            var c = starting ? activeColor : Color.Lerp(activeColor, Color.white, 0.5f);
            var sr = IndigoShapes.Create("SerenityRing", IndigoShapes.ThinRing, null, at, 0.5f, c, 60);
            float time = starting ? 0.8f : 1.2f, size = starting ? 14f : 22f;
            for (float t = 0f; t < time && sr; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, size, 1f - (1f - k) * (1f - k));
                sr.color = new Color(c.r, c.g, c.b, (starting ? 0.8f : 0.45f) * (1f - k));
                yield return null;
            }
            if (sr) Destroy(sr.gameObject);
        }

        void Update()
        {
            bool on = state == SerenityState.Active;
            look = Mathf.MoveTowards(look, on ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, on ? easeIn : easeOut));
            if (look <= 0f && !filter) return;

            if (!filter && Camera.main) filter = SerenityFilter.On(Camera.main);
            if (!filter) return;
            filter.Amount = Mathf.SmoothStep(0f, 1f, look);
            if (look > 0f) filter.enabled = true;
        }

        void OnGUI()
        {
            if (state == SerenityState.Unavailable || !PlayerController.Instance) return;
            label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13, richText = true };

            float now = Time.unscaledTime;
            float denied = Mathf.Clamp01(1f - (now - deniedAt) / 0.35f);
            float shake = denied * Mathf.Sin(now * 70f) * 5f;
            var bar = new Rect(12f + shake, 104f, 200f, 12f);

            Color fill;
            string text;
            switch (state)
            {
                case SerenityState.Active:
                    fill = activeColor;
                    text = "SERENITY";
                    break;
                case SerenityState.Recharging:
                    fill = rechargeColor;
                    text = "SERENITY  <color=#9a96b8>recharging</color>";
                    break;
                default:
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f);
                    float pop = Mathf.Clamp01(1f - (now - readyAt) / 0.5f);
                    fill = Color.Lerp(readyColor, Color.white, 0.15f * pulse + 0.6f * pop);
                    text = "SERENITY  <color=#c8c0ff>READY</color>  [Q]";
                    break;
            }
            fill = Color.Lerp(fill, deniedColor, denied);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
            GUI.color = new Color(fill.r * 0.35f, fill.g * 0.35f, fill.b * 0.35f, 0.9f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fraction), bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x, bar.y - 22f, 320f, 22f), text, label);
        }
    }
}
