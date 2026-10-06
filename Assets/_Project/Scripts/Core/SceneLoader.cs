using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>Loads scenes with a fade. Always go through Game.Scenes — never SceneManager directly.</summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] float fadeDuration = 0.3f;

        float fadeAlpha;
        Texture2D black;

        public bool IsLoading { get; private set; }
        public string Current => SceneManager.GetActiveScene().name;

        void Awake()
        {
            black = new Texture2D(1, 1);
            black.SetPixel(0, 0, Color.black);
            black.Apply();
        }

        public void Load(string sceneName)
        {
            if (IsLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Scene '{sceneName}' is not in Build Settings.");
                return;
            }
            StartCoroutine(LoadRoutine(sceneName));
        }

        public void Reload() => Load(Current);

        IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            yield return Fade(0f, 1f);

            Game.Time.ResetAll(); // normal speed: no pause or slow motion carries into the next scene
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
            GameEvents.RaiseSceneLoaded(sceneName);

            yield return Fade(1f, 0f);
            IsLoading = false;
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                fadeAlpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            fadeAlpha = to;
        }

        void OnGUI()
        {
            if (fadeAlpha <= 0f) return;
            GUI.depth = -1000;
            GUI.color = new Color(0, 0, 0, fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
            GUI.color = Color.white;
        }
    }
}
