using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The boss reveal, played by LevelController.StartBoss() when the level's ColorData.bossIntro is on (the arena gate):
    ///   letterbox in, player frozen, boss idle (its fight hasn't started) -> camera pans to the boss and zooms in ->
    ///   a beat -> BossRevealed (MusicDirector starts the boss song) -> camera eases back -> letterbox out -> the fight.
    /// It only raises events, so the music, letterbox and HUD react on their own. Timings live in GameConfig.
    /// Added at runtime; if the scene unloads mid-intro it still gives back the input and the camera.
    /// </summary>
    public class BossIntro : MonoBehaviour
    {
        bool running;
        CameraFollow follow;
        Camera cam;
        float baseSize;

        public bool IsRunning => running;

        public IEnumerator Play(BossBase boss)
        {
            var config = Game.Config;
            running = true;
            cam = Camera.main;
            follow = cam ? cam.GetComponent<CameraFollow>() : null;
            baseSize = cam ? cam.orthographicSize : 0f;

            Game.Input.BlockGameplay();
            var pc = PlayerController.Instance;
            if (pc && pc.Body) pc.Body.linearVelocity = new Vector2(0f, pc.Body.linearVelocity.y); // stop running; still land
            GameEvents.RaiseBossIntroStarted(boss);
            GameEvents.RaiseCinematicChanged(true); // letterbox in; Serenity ends

            // Pan + zoom onto the boss.
            Vector2 from = cam ? (Vector2)cam.transform.position : Vector2.zero;
            yield return Move(() => from, () => boss ? (Vector2)boss.transform.position : from,
                              baseSize, baseSize * config.bossIntroZoom, config.bossIntroPanSeconds);

            yield return new WaitForSeconds(config.bossIntroBeforeMusic);
            GameEvents.RaiseBossRevealed(boss);
            yield return new WaitForSeconds(config.bossIntroAfterMusic);

            // Ease back to where the follow camera wants to be (the player may have landed meanwhile).
            Vector2 held = cam ? (Vector2)cam.transform.position : Vector2.zero;
            yield return Move(() => held, FollowSpot, baseSize * config.bossIntroZoom, baseSize, config.bossIntroReturnSeconds);

            Finish();
        }

        // Animates the held camera from one spot to another (both re-read every frame) while zooming.
        IEnumerator Move(System.Func<Vector2> from, System.Func<Vector2> to, float sizeFrom, float sizeTo, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / seconds);
                Frame(Vector2.Lerp(from(), to(), e), Mathf.Lerp(sizeFrom, sizeTo, e));
                yield return null;
            }
            Frame(to(), sizeTo);
        }

        void Frame(Vector2 spot, float size)
        {
            if (follow) follow.Hold(spot, snap: true);
            if (cam) cam.orthographicSize = size;
        }

        Vector2 FollowSpot()
        {
            var pc = PlayerController.Instance;
            if (!pc || !follow) return cam ? (Vector2)cam.transform.position : Vector2.zero;
            return (Vector2)pc.transform.position + follow.Offset;
        }

        void Finish()
        {
            if (!running) return;
            running = false;
            if (follow) follow.Release();
            if (cam) cam.orthographicSize = baseSize;
            GameEvents.RaiseCinematicChanged(false); // letterbox out
            if (Game.Input) Game.Input.UnblockGameplay();
        }

        // Scene unloaded mid-intro (pause -> Hub / Restart): don't leave the input blocked or the letterbox up.
        void OnDisable() => Finish();
    }
}
