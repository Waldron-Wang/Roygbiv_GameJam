using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// THE CURTAIN (Serenity): a dense curtain of fast magic needles falling from the ceiling over a stretch,
    /// everywhere except one narrow GAP that moves. The gap waits at the entrance until the player steps in, then
    /// runs a scripted path to the far side: bursts at nearly run speed, sudden stops, a step back. Following it is
    /// physically possible at normal speed (the gap never outruns or out-accelerates the player), but the changes
    /// come too fast to react to. In Serenity the same path is three times slower to read, and the emitters along
    /// the ceiling show where the gap is ABOUT to be (lit = needles coming, dark = the way).
    ///
    /// Sized for one Serenity: the run takes about 1.2 seconds of game time (~3.4 real seconds in Serenity; it lasts
    /// 4). The safe spot before it lets the player wait for Serenity to recharge. Dash can't do it: the curtain is
    /// ~2x a dash long and the needles pierce Dash's i-frames, and every needle that touches the player throws them
    /// back to the entrance (even during post-hit i-frames), which also resets the run.
    /// </summary>
    public class VioletCurtain : MonoBehaviour
    {
        [Serializable]
        public struct Step
        {
            [Tooltip("Seconds (game time).")] public float time;
            [Tooltip("Gap speed to head for, units per second (+ = toward the exit). Reached at gapAcceleration.")]
            public float speed;
            public Step(float time, float speed) { this.time = time; this.speed = speed; }
        }

        float x0, x1, floorY, ceilY;
        Settings s;
        readonly List<float> columns = new();
        readonly List<float> nextFire = new();
        readonly List<SpriteRenderer> emitters = new();
        float[] pathX; // gap left edge per pathStep seconds after the run starts
        const float PathStep = 0.01f;
        SpriteRenderer floorGlow, safeGlow;
        TextMesh hint;
        bool running;
        float runTime, parkX;

        [Serializable]
        public class Settings
        {
            [Tooltip("Distance between needle columns.")] public float columnSpacing = 0.35f;
            [Tooltip("Needle speed (falling).")] public float needleSpeed = 26f;
            [Tooltip("Needle length.")] public float needleLength = 0.7f;
            [Tooltip("Seconds between needles in one column: with the speed and length, closes the column with no gap.")]
            public float fireInterval = 0.07f;
            [Tooltip("Width of the safe gap. The player is 0.8 wide.")] public float gapWidth = 2f;
            [Tooltip("How fast the gap can change speed (u/s²). Keep it under the player's ground acceleration (70) so it's always followable.")]
            public float gapAcceleration = 60f;
            [Tooltip("How far ahead (seconds) the ceiling emitters show the gap.")] public float previewTime = 0.3f;
            [Tooltip("How much of the gap overlaps the safe spot while it waits.")] public float parkOverlap = 0.9f;
            [Tooltip("Damage per needle (they pierce Dash's i-frames).")] public int damage = 1;
            [Tooltip("A needle throws the player back toward the entrance at this velocity...")] public Vector2 shove = new(-13f, 6f);
            [Tooltip("...for this long.")] public float shoveTime = 0.35f;
            [Tooltip("The gap's path after the player steps in. The first hold lets needles already in the air land first.")]
            public Step[] path =
            {
                new(0.2f, 0f), new(0.16f, 9.5f), new(0.08f, 0f), new(0.13f, 9.5f), new(0.07f, -5f),
                new(0.07f, 0f), new(0.17f, 9.5f), new(0.07f, 0f), new(0.5f, 9.5f),
            };
        }

        /// <summary>The director turns it on while the player is near (off = no needles at all).</summary>
        public bool Active { get; set; }

        /// <summary>Gap's left edge right now.</summary>
        public float GapX => GapAt(runTime);

        public void Setup(float startX, float endX, float floor, float ceiling, Settings settings, Color color)
        {
            x0 = startX;
            x1 = endX;
            floorY = floor;
            ceilY = ceiling;
            s = settings ?? new Settings();
            parkX = x0 - s.parkOverlap;

            for (float x = x0 + s.columnSpacing * 0.5f; x < x1; x += s.columnSpacing)
            {
                columns.Add(x);
                nextFire.Add(UnityEngine.Random.Range(0f, s.fireInterval));
                var e = VioletShapes.Create("Emitter", VioletShapes.Gem, transform, new Vector2(x - transform.position.x, ceilY - 0.12f - transform.position.y), color, 22);
                e.transform.localScale = new Vector3(0.22f, 0.3f, 1f);
                emitters.Add(e);
            }
            floorGlow = FlatSprite.Create("GapGlow", transform, new Vector2(x0, floorY + 0.03f), new Vector2(s.gapWidth, 0.08f), Color.clear, 6);
            safeGlow = FlatSprite.Create("SafeSpot", transform, new Vector2(x0 - 3f, floorY + 0.03f), new Vector2(5f, 0.06f), new Color(0.5f, 0.45f, 1f, 0.35f), 6);
            hint = IndigoShapes.Label("SerenityHint", transform, new Vector2(x0 - 3f - transform.position.x, floorY + 3.2f - transform.position.y), "[Q]", 0.6f, new Color(0.75f, 0.7f, 1f, 0f), 40);
            BuildPath();
        }

        void BuildPath()
        {
            var xs = new List<float>();
            float x = parkX, v = 0f;
            foreach (var step in s.path)
                for (float t = 0f; t < step.time; t += PathStep)
                {
                    v = Mathf.MoveTowards(v, step.speed, s.gapAcceleration * PathStep);
                    x += v * PathStep;
                    xs.Add(x);
                }
            pathX = xs.ToArray();
        }

        /// <summary>Where the gap's left edge is `t` seconds after the run started (parked before it, still running after the script ends).</summary>
        float GapAt(float t)
        {
            if (!running) return parkX;
            if (pathX.Length == 0) return parkX;
            int i = Mathf.FloorToInt(t / PathStep);
            if (i < pathX.Length) return pathX[Mathf.Max(0, i)];
            var lastSpeed = s.path.Length > 0 ? s.path[s.path.Length - 1].speed : 0f;
            return pathX[pathX.Length - 1] + (t - pathX.Length * PathStep) * lastSpeed;
        }

        bool InGap(float x, float t)
        {
            float g = GapAt(t);
            return x > g && x < g + s.gapWidth;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var player = VioletHits.Player;
            float px = player ? player.transform.position.x : float.NegativeInfinity;
            bool hasSerenity = player && player.Loadout.Has(AbilityId.Serenity);

            // Hint at the safe spot, only for a player who has Serenity and is near.
            if (hint)
            {
                float want = Active && hasSerenity && px < x0 && px > x0 - 10f ? 0.7f + 0.3f * Mathf.Sin(Time.time * 3f) : 0f;
                hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, Mathf.MoveTowards(hint.color.a, want, dt * 2f));
            }

            if (!Active) { running = false; runTime = 0f; DrawIdle(); return; }

            // Back at the entrance (or never left it): the gap waits there. Stepping in starts a run.
            if (px < x0) { running = false; runTime = 0f; }
            else if (!running && px > x0 + 0.25f && px < x1) { running = true; runTime = 0f; }
            if (running) runTime += dt;

            float lead = (ceilY - floorY - 0.6f) / s.needleSpeed; // emission -> needle at the player's middle
            var bounds = Rect.MinMaxRect(x0 - 1f, floorY - 0.2f, x1 + 1f, ceilY + 1f);
            for (int c = 0; c < columns.Count; c++)
            {
                nextFire[c] -= dt;
                while (nextFire[c] <= 0f)
                {
                    nextFire[c] += s.fireInterval;
                    if (InGap(columns[c], runTime + lead)) continue;
                    VioletNeedle.Spawn(new Vector2(columns[c], ceilY - 0.2f), new Vector2(0f, -s.needleSpeed), s.needleLength, s.damage, bounds, true, 1f)
                        .Shoving(s.shove, s.shoveTime);
                }

                // Emitters preview the gap: dark where it will be, lit where needles will keep coming.
                bool future = InGap(columns[c], runTime + lead + s.previewTime);
                var e = emitters[c];
                float lit = future ? 0.12f : 0.85f + 0.15f * Mathf.Sin(Time.time * 30f + c);
                e.color = new Color(e.color.r, e.color.g, e.color.b, Mathf.MoveTowards(e.color.a, lit, dt * 12f));
            }

            float g = GapAt(runTime);
            floorGlow.transform.position = new Vector3(Mathf.Clamp(g + s.gapWidth * 0.5f, x0 - 1f, x1 + 1f), floorY + 0.04f, 0f);
            floorGlow.color = new Color(0.8f, 0.85f, 1f, 0.5f + 0.2f * Mathf.Sin(Time.time * 8f));
        }

        void DrawIdle()
        {
            foreach (var e in emitters) e.color = new Color(e.color.r, e.color.g, e.color.b, 0.25f);
            if (floorGlow) floorGlow.color = Color.clear;
        }
    }
}
