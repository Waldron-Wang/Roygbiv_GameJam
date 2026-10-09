using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Played by GameManager after the last color is restored, before the Ending: the player is frozen, the camera
    /// rises off the level and the background (ParallaxLayer.FullView) eases into its whole, uncropped picture,
    /// now in full color, then holds there. Timings live in GameConfig.
    /// No letterbox on purpose: the bars would hide the top and bottom of the picture.
    /// Added at runtime to the level scene; if the scene unloads mid-way it still gives back the input.
    /// </summary>
    public class FinalShowcase : MonoBehaviour
    {
        bool running, blocked;
        CameraFollow follow;
        ParallaxLayer[] layers;

        /// <summary>Runs in the active scene and yields until the hold is over (or the scene unloads).</summary>
        public static IEnumerator Play()
        {
            var show = new GameObject("FinalShowcase").AddComponent<FinalShowcase>();
            show.StartCoroutine(show.Run());
            while (show && show.running) yield return null;
        }

        IEnumerator Run()
        {
            var config = Game.Config;
            running = true;
            Game.Input.BlockGameplay();
            blocked = true;
            var pc = PlayerController.Instance;
            if (pc && pc.Body) pc.Body.linearVelocity = new Vector2(0f, pc.Body.linearVelocity.y); // stop running; still land

            var cam = Camera.main;
            follow = cam ? cam.GetComponent<CameraFollow>() : null;
            layers = FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None);

            Vector2 from = cam ? (Vector2)cam.transform.position : Vector2.zero;
            Vector2 to = from + Vector2.up * (cam ? cam.orthographicSize * 2f * config.finalShowcaseRise : 0f);
            float seconds = config.finalShowcaseRiseSeconds;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / seconds);
                Frame(Vector2.Lerp(from, to, e), e);
                yield return null;
            }
            Frame(to, 1f);

            yield return new WaitForSeconds(config.finalShowcaseHoldSeconds);
            running = false; // the camera stays held: the Ending loads next
        }

        void Frame(Vector2 spot, float fullView)
        {
            if (follow) follow.Hold(spot, snap: true);
            foreach (var layer in layers)
                if (layer) layer.FullView = fullView;
        }

        // Scene unloaded (the Ending loading, or pause -> Hub mid-way): don't leave the input blocked.
        void OnDisable()
        {
            running = false;
            if (blocked && Game.Input) Game.Input.UnblockGameplay();
            blocked = false;
        }
    }
}
