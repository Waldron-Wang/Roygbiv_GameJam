using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// THE CURTAIN (Serenity): fast magic needles fall from a ceiling everywhere over a stretch, except one GAP that
    /// moves in short forward BURSTS and sudden STOPS. The gap is always slower and gentler than the player (its top
    /// speed and acceleration are fractions of the real PlayerMotor's, read at runtime), and wide (3 units for a
    /// 0.8-wide player), so following it is always physically possible. The hard part is REACTING: at normal speed
    /// the starts and stops come every 0.15-0.25 s, too fast; in Serenity the same rhythm takes ~3x longer in real
    /// time and reads comfortably.
    ///
    /// The gap is truly empty from the ceiling to the floor: a needle is only fired if its column stays out of the
    /// gap for its whole fall (the path is known in advance), and any needle that somehow ends up inside is cleared.
    /// A light column through the curtain and a glow on the floor show the gap now; the emitters along the ceiling
    /// go dark where it's about to be.
    ///
    /// Course mode (Setup): the gap waits at the entrance until the player steps in, then runs to the far side.
    /// Arena mode (SetupArena / Park / BeginRun): Royal Decree parks it, starts the needles, then runs it.
    /// Needles pierce Dash's i-frames; course needles also shove the player back to the entrance (resetting the run).
    /// In the editor and development builds every curtain checks itself when built: a simulated player follows the
    /// gap with the real run speed and acceleration, and any moment its body would be under a needle is logged.
    /// </summary>
    public class VioletCurtain : MonoBehaviour
    {
        [Serializable]
        public class Settings
        {
            [Tooltip("Distance between needle columns.")] public float columnSpacing = 0.35f;
            [Tooltip("Needle speed (falling).")] public float needleSpeed = 26f;
            [Tooltip("Needle length.")] public float needleLength = 0.7f;
            [Tooltip("Seconds between needles in one column: with the speed and length, a column has no holes.")]
            public float fireInterval = 0.07f;
            [Tooltip("Width of the safe gap. The player is 0.8 wide.")] public float gapWidth = 3f;
            [Tooltip("The gap's top speed, as a fraction of the player's run speed (read from PlayerMotor at runtime).")]
            [Range(0.2f, 0.9f)] public float speedFraction = 0.75f;
            [Tooltip("The gap's acceleration, as a fraction of the player's ground acceleration.")]
            [Range(0.2f, 0.9f)] public float accelerationFraction = 0.75f;
            [Tooltip("Seconds (game time) of each forward burst, random in [x, y].")]
            public Vector2 burstTime = new(0.15f, 0.25f);
            [Tooltip("Seconds (game time) of each stop, random in [x, y].")]
            public Vector2 stopTime = new(0.15f, 0.22f);
            [Tooltip("Seed for the rhythm, so the same curtain always moves the same way.")]
            public int seed = 7;
            [Tooltip("How far ahead (seconds) the ceiling emitters show the gap.")] public float previewTime = 0.3f;
            [Tooltip("Course: how far into the safe spot the waiting gap reaches (so the player can step into it).")]
            public float parkOverlap = 1.5f;
            [Tooltip("Needles keep at least this far outside the gap's edges.")] public float margin = 0.12f;
            [Tooltip("Damage per needle (they pierce Dash's i-frames).")] public int damage = 1;
            [Tooltip("Course: a needle throws the player back toward the entrance at this velocity (0 = no shove)...")]
            public Vector2 shove = new(-13f, 6f);
            [Tooltip("...for this long.")] public float shoveTime = 0.35f;
        }

        const float PathStep = 0.01f;
        static readonly Color LightColor = new(0.85f, 0.85f, 1f);

        readonly List<float> columns = new();
        readonly List<float> nextFire = new();
        readonly List<SpriteRenderer> emitters = new();
        readonly List<VioletNeedle> needles = new();
        Settings s;
        float x0, x1, floorY, ceilY, vmax, amax, parkLeft, runTime;
        int dir = 1;
        float[] pathX = { };
        bool running, courseMode;
        SpriteRenderer floorGlow, column, edgeL, edgeR;
        TextMesh hint;
        string label = "Curtain";

        /// <summary>Off = no needles at all (the director turns a course curtain on while the player is near).</summary>
        public bool Active { get; set; }
        public float GapWidth => s.gapWidth;
        /// <summary>Seconds (game time) a needle takes from the ceiling to below the floor.</summary>
        public float NeedleLife => (ceilY - floorY + s.needleLength) / Mathf.Max(1f, s.needleSpeed) + 0.05f;
        /// <summary>The run's first hold: needles fired while the gap waited are all gone before it moves.</summary>
        float Hold => NeedleLife + 0.05f;

        // ---------- The player's limits ----------

        /// <summary>The real player's run speed and ground acceleration (PlayerMotor), or the code defaults.</summary>
        public static (float run, float accel) PlayerLimits()
        {
            var pc = PlayerController.Instance;
            var motor = pc ? pc.GetComponent<PlayerMotor>() : null;
            return motor ? (motor.runSpeed, motor.groundAcceleration) : (7f, 70f);
        }

        /// <summary>Game seconds one Serenity lasts (real duration x its time scale), minus a moment to step in after pressing it.</summary>
        public static float SerenityBudget(float reserve = 0.25f)
        {
            var pc = PlayerController.Instance;
            if (pc && pc.Loadout.Get(AbilityId.Serenity) is SerenityAbility serenity) return serenity.Duration * serenity.TimeScale - reserve;
            return 1.4f - reserve;
        }

        // ---------- Setup ----------

        /// <summary>Course mode: the curtain spans [startX, endX] under a ceiling; the gap waits at the entrance.</summary>
        public void Setup(float startX, float endX, float floor, float ceiling, Settings settings, Color color, string name)
        {
            courseMode = true;
            Init(startX, endX, floor, ceiling, settings, color, name);
            parkLeft = x0 - s.parkOverlap;
            dir = 1;
            pathX = BuildPath(parkLeft, 1, CourseTravel(), true);
            var safe = FlatSprite.Create("SafeSpot", transform, new Vector2(x0 - 3f, floorY + 0.03f), new Vector2(5f, 0.06f), new Color(0.5f, 0.45f, 1f, 0.35f), 6);
            safe.transform.SetParent(transform, true);
            hint = IndigoShapes.Label("SerenityHint", transform, new Vector2(x0 - 3f, floorY + 3.2f) - (Vector2)transform.position, "[Q]", 0.6f, new Color(0.75f, 0.7f, 1f, 0f), 40);
            SelfCheck();
        }

        /// <summary>Arena mode (Royal Decree): needles over [minX, maxX] from `ceiling`; Park then BeginRun from code.</summary>
        public void SetupArena(float minX, float maxX, float floor, float ceiling, Settings settings, Color color, string name)
        {
            courseMode = false;
            Init(minX, maxX, floor, ceiling, settings, color, name);
            Park((minX + maxX) * 0.5f);
        }

        void Init(float startX, float endX, float floor, float ceiling, Settings settings, Color color, string name)
        {
            s = settings ?? new Settings();
            x0 = startX;
            x1 = endX;
            floorY = floor;
            ceilY = ceiling;
            label = name;
            var (run, accel) = PlayerLimits();
            vmax = run * s.speedFraction;
            amax = accel * s.accelerationFraction;

            for (float x = x0 + s.columnSpacing * 0.5f; x < x1; x += s.columnSpacing)
            {
                columns.Add(x);
                nextFire.Add(Random.Range(0f, s.fireInterval));
                var e = VioletShapes.Create("Emitter", VioletShapes.Gem, transform, new Vector2(x, ceilY - 0.12f) - (Vector2)transform.position, color, 22);
                e.transform.localScale = new Vector3(0.22f, 0.3f, 1f);
                emitters.Add(e);
            }
            column = FlatSprite.Create("GapLight", transform, Vector2.zero, Vector2.one, Color.clear, 5);
            edgeL = FlatSprite.Create("GapEdge", transform, Vector2.zero, Vector2.one, Color.clear, 6);
            edgeR = FlatSprite.Create("GapEdge", transform, Vector2.zero, Vector2.one, Color.clear, 6);
            floorGlow = FlatSprite.Create("GapGlow", transform, Vector2.zero, Vector2.one, Color.clear, 6);
        }

        /// <summary>The travel that carries a player in the gap out past the far end.</summary>
        float CourseTravel() => x1 + 0.45f - s.gapWidth - parkLeft;

        /// <summary>Arena mode: the gap waits centered on `center` (clamped inside), shown as a light column.</summary>
        public void Park(float center)
        {
            running = false;
            runTime = 0f;
            parkLeft = Mathf.Clamp(center - s.gapWidth * 0.5f, x0, x1 - s.gapWidth);
        }

        /// <summary>
        /// Arena mode: the gap starts its run from where it's parked, toward the side with more room, travelling as far
        /// as it can in `seconds` without reaching the wall. Start it at least NeedleLife after needles began to fall.
        /// </summary>
        public void BeginRun(float seconds)
        {
            float roomRight = x1 - (parkLeft + s.gapWidth), roomLeft = parkLeft - x0;
            dir = roomRight >= roomLeft ? 1 : -1;
            float room = Mathf.Max(0f, (dir > 0 ? roomRight : roomLeft) - 0.2f);
            pathX = BuildPath(parkLeft, dir, Mathf.Min(room, ReachIn(seconds)), false);
            running = true;
            runTime = 0f;
        }

        /// <summary>How far the burst-and-stop rhythm carries the gap in `seconds` (after the hold).</summary>
        float ReachIn(float seconds)
        {
            var path = BuildPath(0f, 1, 1000f, false, Hold + seconds);
            return path.Length > 0 ? path[path.Length - 1] : 0f;
        }

        // ---------- The path ----------

        /// <summary>
        /// The gap's left edge every PathStep from the start of the run: a hold, then bursts at vmax and stops (both
        /// reached at amax) until it has travelled `travel`; then it keeps going (course: off the far side) or stops.
        /// </summary>
        float[] BuildPath(float start, int direction, float travel, bool keepGoing, float maxTime = 30f)
        {
            var xs = new List<float>();
            var rng = new System.Random(s.seed);
            float x = start, v = 0f, moved = 0f, t = 0f;
            for (; t < Hold && t < maxTime; t += PathStep) xs.Add(x);

            bool burst = true;
            while (moved < travel && t < maxTime)
            {
                var range = burst ? s.burstTime : s.stopTime;
                float duration = Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
                float target = burst ? vmax : 0f;
                for (float d = 0f; d < duration && moved < travel && t < maxTime; d += PathStep, t += PathStep)
                {
                    v = Mathf.MoveTowards(v, target, amax * PathStep);
                    float step = Mathf.Min(v * PathStep, travel - moved);
                    moved += step;
                    x += direction * step;
                    xs.Add(x);
                }
                burst = !burst;
            }
            // After the travel: off the far side (course), or brake and wait (arena).
            for (float d = 0f; d < 1.5f && t < maxTime; d += PathStep, t += PathStep)
            {
                v = Mathf.MoveTowards(v, keepGoing ? vmax : 0f, amax * PathStep);
                if (keepGoing) x += direction * v * PathStep;
                xs.Add(x);
            }
            return xs.ToArray();
        }

        /// <summary>Gap's left edge `t` seconds after the run started (parked before it, or while not running).</summary>
        float GapAt(float t) => GapAt(t, running);

        float GapAt(float t, bool isRunning)
        {
            if (!isRunning || t < 0f || pathX.Length == 0) return parkLeft;
            int i = Mathf.FloorToInt(t / PathStep);
            return pathX[Mathf.Min(i, pathX.Length - 1)];
        }

        bool InGap(float x, float t, float margin) => InGap(x, t, margin, running);

        bool InGap(float x, float t, float margin, bool isRunning)
        {
            float g = GapAt(t, isRunning);
            return x > g - margin && x < g + s.gapWidth + margin;
        }

        /// <summary>A needle fired at `t` in this column stays out of the gap (plus margin) for its whole fall.</summary>
        bool ColumnClear(float x, float t, bool isRunning)
        {
            float life = NeedleLife;
            for (float tau = 0f; tau <= life; tau += 0.02f)
                if (InGap(x, t + tau, s.margin, isRunning)) return false;
            return true;
        }

        // ---------- Running ----------

        void Update()
        {
            float dt = Time.deltaTime;
            var player = VioletHits.Player;
            float px = player ? player.transform.position.x : float.NegativeInfinity;

            if (hint)
            {
                bool show = Active && player && player.Loadout.Has(AbilityId.Serenity) && px < x0 && px > x0 - 10f;
                float want = show ? 0.7f + 0.3f * Mathf.Sin(Time.time * 3f) : 0f;
                hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, Mathf.MoveTowards(hint.color.a, want, dt * 2f));
            }

            if (courseMode)
            {
                if (!Active || px < x0) { running = false; runTime = 0f; } // back at the entrance: the gap waits there
                else if (!running && px > x0 + 0.25f && px < x1) { running = true; runTime = 0f; }
            }
            if (running) runTime += dt;

            DrawGap(Active);
            if (!Active) return;

            var bounds = Rect.MinMaxRect(x0 - 1f, floorY - 0.3f, x1 + 1f, ceilY + 1f);
            for (int c = 0; c < columns.Count; c++)
            {
                nextFire[c] -= dt;
                while (nextFire[c] <= 0f)
                {
                    nextFire[c] += s.fireInterval;
                    if (!ColumnClear(columns[c], runTime, running)) continue;
                    var n = VioletNeedle.Spawn(new Vector2(columns[c], ceilY - 0.2f), new Vector2(0f, -s.needleSpeed), s.needleLength, s.damage, bounds, true, NeedleLife + 0.2f);
                    if (courseMode && s.shoveTime > 0f) n.Shoving(s.shove, s.shoveTime);
                    needles.Add(n);
                }
            }

            // Safety net: nothing may hang inside the gap.
            needles.RemoveAll(n => !n);
            float g = GapAt(runTime);
            foreach (var n in needles)
            {
                float x = n.transform.position.x;
                if (x <= g || x >= g + s.gapWidth) continue;
                VioletHits.Burst(n.transform.position, VioletNeedle.RimColor, 3, 2f, 0.15f);
                Destroy(n.gameObject);
            }
        }

        void DrawGap(bool active)
        {
            float lead = (ceilY - floorY) * 0.5f / Mathf.Max(1f, s.needleSpeed);
            for (int c = 0; c < emitters.Count; c++)
            {
                // Dark where the gap is about to be, lit where needles keep coming.
                bool gapSoon = InGap(columns[c], runTime + lead + s.previewTime, 0f);
                var e = emitters[c];
                float want = !active ? 0.25f : gapSoon ? 0.08f : 0.85f + 0.15f * Mathf.Sin(Time.time * 30f + c);
                e.color = new Color(e.color.r, e.color.g, e.color.b, Mathf.MoveTowards(e.color.a, want, Time.deltaTime * 12f));
            }

            float g = GapAt(runTime), w = s.gapWidth, h = ceilY - floorY;
            float cx = Mathf.Clamp(g + w * 0.5f, x0 - w, x1 + w);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
            float a = active ? 1f : 0.35f;
            Place(column, new Vector2(cx, floorY + h * 0.5f), new Vector2(w, h), new Color(LightColor.r, LightColor.g, LightColor.b, 0.13f * pulse * a));
            Place(edgeL, new Vector2(cx - w * 0.5f, floorY + h * 0.5f), new Vector2(0.05f, h), new Color(LightColor.r, LightColor.g, LightColor.b, 0.4f * a));
            Place(edgeR, new Vector2(cx + w * 0.5f, floorY + h * 0.5f), new Vector2(0.05f, h), new Color(LightColor.r, LightColor.g, LightColor.b, 0.4f * a));
            Place(floorGlow, new Vector2(cx, floorY + 0.05f), new Vector2(w, 0.1f), new Color(0.8f, 0.85f, 1f, (0.45f + 0.2f * Mathf.Sin(Time.time * 8f)) * a));
        }

        static void Place(SpriteRenderer sr, Vector2 center, Vector2 size, Color color)
        {
            sr.transform.position = center;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.color = color;
        }

        /// <summary>Clears every needle of this curtain (Royal Decree ends, the boss dies).</summary>
        public void ClearNeedles()
        {
            foreach (var n in needles) if (n) Destroy(n.gameObject);
            needles.Clear();
        }

        void OnDestroy() => ClearNeedles();

        // ---------- Self-check ----------

        /// <summary>The outcome of following the gap: whether a body ever ends up under a needle, and when it got out.</summary>
        public struct Check
        {
            public bool ok;
            public float exitTime, failTime, failX, failColumn;
        }

        /// <summary>
        /// Simulates a player who follows the gap's center with the real run speed and acceleration, against every
        /// needle the curtain could have in the air (a column counts as deadly whenever a needle fired in the last
        /// NeedleLife seconds could be anywhere in it, top to bottom). Course: until the player is out past the end.
        /// </summary>
        public Check Simulate(float startCenter, float duration, bool untilExit)
        {
            var (run, accel) = PlayerLimits();
            const float dt = 0.005f, half = 0.4f, needleRadius = 0.07f;
            float life = NeedleLife;
            float window0 = -life - 0.1f;
            int steps = Mathf.CeilToInt((duration - window0) / PathStep) + 1;

            // allowed[c][k]: a needle fired in column c at time window0 + k * PathStep is allowed.
            var allowed = new bool[columns.Count][];
            for (int c = 0; c < columns.Count; c++)
            {
                allowed[c] = new bool[steps];
                for (int k = 0; k < steps; k++) allowed[c][k] = ColumnClear(columns[c], window0 + k * PathStep, true);
            }

            var result = new Check { ok = true, exitTime = -1f };
            float p = startCenter, v = 0f;
            for (float t = 0f; t < duration; t += dt)
            {
                float g = GapAt(t, true), gNext = GapAt(t + PathStep, true);
                float target = g + s.gapWidth * 0.5f, gv = (gNext - g) / PathStep;
                float want = Mathf.Clamp(gv + 8f * (target - p), -run, run);
                v = Mathf.MoveTowards(v, want, accel * dt);
                p += v * dt;

                for (int c = 0; c < columns.Count && result.ok; c++)
                {
                    if (Mathf.Abs(columns[c] - p) >= half + needleRadius) continue;
                    int kTo = Mathf.Min(steps - 1, Mathf.FloorToInt((t - window0) / PathStep));
                    int kFrom = Mathf.Max(0, Mathf.FloorToInt((t - life - window0) / PathStep));
                    for (int k = kFrom; k <= kTo; k++)
                        if (allowed[c][k]) { result = new Check { ok = false, failTime = t, failX = p, failColumn = columns[c], exitTime = -1f }; break; }
                }
                if (!result.ok) return result;
                if (untilExit && p - half > x1 + s.margin) { result.exitTime = t; return result; }
            }
            if (untilExit) result.ok = false; // never got out
            return result;
        }

        /// <summary>Editor / development builds: checks this course curtain and logs any problem.</summary>
        void SelfCheck()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var (run, accel) = PlayerLimits();
            float budget = SerenityBudget();
            var r = Simulate(parkLeft + s.gapWidth * 0.5f, pathX.Length * PathStep + 2f, true);
            if (!r.ok && r.failTime > 0f)
                Debug.LogError($"[Violet] {label}: a player following the gap gets hit at t={r.failTime:0.00}s, x={r.failX:0.00} (needle column x={r.failColumn:0.00}). Player run {run}, accel {accel}; gap top speed {vmax:0.0}, accel {amax:0.0}.", this);
            else if (!r.ok)
                Debug.LogError($"[Violet] {label}: a player following the gap never gets out past x={x1:0.0}.", this);
            else if (r.exitTime > budget)
                Debug.LogError($"[Violet] {label}: the run takes {r.exitTime:0.00} game-s, but one Serenity gives {budget:0.00} (after stepping in). Make the curtain shorter or Serenity longer.", this);
            else
                Debug.Log($"[Violet] {label} checked: width {x1 - x0:0.0}, gap {s.gapWidth}, a gap-follower exits at {r.exitTime:0.00} game-s (Serenity budget {budget:0.00}); player run {run}, gap top speed {vmax:0.0}.", this);
#endif
        }

        /// <summary>
        /// Editor / development builds: checks an arena curtain (Royal Decree) the way it will be used: parked at
        /// `center`, needles for `preRoll` seconds, then a run of `seconds`. Returns false (and logs) on a problem.
        /// </summary>
        public bool CheckArenaRun(float center, float preRoll, float seconds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Park(center);
            BeginRun(seconds);
            var r = Simulate(parkLeft + s.gapWidth * 0.5f, Hold + seconds + NeedleLife, false);
            Park(center);
            if (!r.ok)
            {
                var (run, _) = PlayerLimits();
                Debug.LogError($"[Violet] {label} (from x={center:0.0}): a player following the gap gets hit at t={r.failTime:0.00}s, x={r.failX:0.00} (needle column x={r.failColumn:0.00}). Player run {run}, gap top speed {vmax:0.0}.", this);
                return false;
            }
            if (preRoll < NeedleLife) Debug.LogError($"[Violet] {label}: needles start {preRoll:0.00}s before the run but take {NeedleLife:0.00}s to fall: the gap's first move could meet needles in the air.", this);
#endif
            return true;
        }

        /// <summary>
        /// The longest curtain (between minWidth and maxWidth, in 0.25 steps) whose run a gap-follower clears within one
        /// Serenity, for the real player. Used by the course to size the curtain before building it.
        /// </summary>
        public static float FitWidth(Settings settings, float floor, float ceiling, float minWidth, float maxWidth)
        {
            float budget = SerenityBudget();
            var probe = new GameObject("CurtainProbe").AddComponent<VioletCurtain>();
            float best = minWidth;
            try
            {
                for (float w = maxWidth; w >= minWidth; w -= 0.25f)
                {
                    probe.Reset();
                    probe.courseMode = true;
                    probe.Init(0f, w, floor, ceiling, settings, Color.clear, "probe");
                    probe.parkLeft = -probe.s.parkOverlap;
                    probe.pathX = probe.BuildPath(probe.parkLeft, 1, probe.CourseTravel(), true);
                    probe.running = true;
                    var r = probe.Simulate(probe.parkLeft + probe.s.gapWidth * 0.5f, probe.pathX.Length * PathStep + 2f, true);
                    if (r.ok && r.exitTime <= budget) { best = w; break; }
                }
            }
            finally
            {
                DestroyImmediate(probe.gameObject);
            }
            return best;
        }

        void Reset()
        {
            foreach (var e in emitters) if (e) DestroyImmediate(e.gameObject);
            emitters.Clear();
            columns.Clear();
            nextFire.Clear();
            foreach (var sr in new[] { column, edgeL, edgeR, floorGlow }) if (sr) DestroyImmediate(sr.gameObject);
        }
    }
}
