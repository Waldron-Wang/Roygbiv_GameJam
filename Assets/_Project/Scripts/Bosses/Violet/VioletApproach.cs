using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Runs Violet's level around the boss fight: phase 1 (THE APPROACH) and the hand-off to the duel.
    ///   Start:  builds the course, puts the player at the last checkpoint (VioletCheckpoint) and the king on his dais.
    ///   Intro:  first entry only. Input blocked, the camera on the king in his attack stance (title "VIOLET"), then a
    ///           hard pull all the way back along the course to the player. Skippable with confirm after a moment.
    ///   Approach: while the player is inside one of the course's zones, that zone's long-range attack runs. Every
    ///           attack is announced at the right edge of the screen at its exact height (VioletTelegraph), and the king
    ///           plays the matching gesture on his dais (visible once you're close). He can't be hurt.
    ///   Arena:  walking in seals the door, locks the camera on the arena and starts the fight (LevelController.StartBoss;
    ///           the scene has startBossImmediately off). The boss takes it from there.
    /// Checkpoints: one per segment, the arena entrance, and Twin Blades (phaseThreeCheckpoint). Respawns skip the intro.
    /// </summary>
    public class VioletApproach : MonoBehaviour
    {
        [SerializeField] VioletCourse course;

        [Header("Intro")]
        [Tooltip("Seconds the camera holds on the king before the pull.")]
        [SerializeField] float introHold = 1.5f;
        [Tooltip("Seconds of the pull back along the course to the player.")]
        [SerializeField] float introPull = 2.75f;
        [Tooltip("Confirm skips the intro after this many seconds.")]
        [SerializeField] float introSkipAfter = 1f;
        [Tooltip("Where the camera frames the king during the intro, relative to his feet.")]
        [SerializeField] Vector2 introFrame = new(-3f, 2.5f);
        [SerializeField] string title = "VIOLET";
        [SerializeField] string subtitle = "The Sovereign";

        [Header("Checkpoints")]
        [Tooltip("Remember the start of Twin Blades (respawn there at the threshold, cape already gone).")]
        [SerializeField] bool phaseThreeCheckpoint = true;

        [Header("Testing")]
        [Tooltip("EDITOR ONLY: playing Level_Violet directly, grant every ability the player doesn't have yet (no F1-F6 needed). The save isn't touched.")]
        [SerializeField] bool grantAllAbilitiesInEditor = true;

        [Header("Crescent Waves")]
        [Tooltip("Telegraph seconds before each wave.")]
        [SerializeField] float waveTelegraph = 0.7f;
        [Tooltip("Wave speed.")]
        [SerializeField] float waveSpeed = 11f;
        [Tooltip("Wave thickness: about 0.05 s of overlap with a dashing player, well inside Dash's i-frames.")]
        [SerializeField] float waveThickness = 1f;

        [Header("Rising Steps")]
        [Tooltip("Slam wind-up (ripples spawn after it).")]
        [SerializeField] float slamWindUp = 0.7f;
        [Tooltip("Ripple height: a single jump clears it.")]
        [SerializeField] float rippleHeight = 0.9f;
        [Tooltip("Ripple speed.")]
        [SerializeField] float rippleSpeed = 9f;

        [Header("Royal Rain")]
        [Tooltip("Swords per second during the barrage: dense enough that 7 s in the open costs more than 5 HP.")]
        [SerializeField] float rainRate = 30f;
        [Tooltip("Height above the floor the swords fall from.")]
        [SerializeField] float rainHeight = 12f;
        [Tooltip("Sword falling speed.")]
        [SerializeField] float rainSpeed = 24f;
        [Tooltip("Seconds the seals take to close / open.")]
        [SerializeField] float sealTime = 0.5f;

        [Header("Crystal Gate pressure")]
        [Tooltip("Seconds a sword's landing spot is marked.")]
        [SerializeField] float pressureSwordWarn = 0.7f;
        [Tooltip("Every Nth pressure attack is a gold orb lobbed over the crystal (punch it back).")]
        [SerializeField] int lobEvery = 3;

        [Header("Opening volley")]
        [Tooltip("Orbs he throws as the intro camera arrives.")]
        [SerializeField] int openingOrbs = 3;
        [SerializeField] float openingOrbSpeed = 7f;

        [SerializeField] Color attackColor = new(0.76f, 0.4f, 1f);

        readonly List<GameObject> live = new(); // telegraphs of the running encounter
        VioletBoss boss;
        CameraFollow cam;
        VioletCourse.Zone activeZone;
        Coroutine encounter;
        bool approachRunning, duelStarted, inputBlocked;

        void Awake()
        {
            if (!course) course = GetComponent<VioletCourse>();
        }

        void OnEnable() => GameEvents.LevelCompleted += OnLevelCompleted;
        void OnDisable() => GameEvents.LevelCompleted -= OnLevelCompleted;

        void OnDestroy()
        {
            // Reloaded mid-intro (pause menu restart): never leave the input blocked.
            if (inputBlocked && Game.Input != null) Game.Input.UnblockGameplay();
            inputBlocked = false;
        }

        void OnLevelCompleted(ColorId color)
        {
            if (color != ColorId.Violet) return;
            VioletCheckpoint.Clear();
            approachRunning = false;
            StopEncounter();
        }

        IEnumerator Start()
        {
            course.Build();
            VioletCheckpoint.Bind(gameObject.scene.name);
            VioletCheckpoint.SegmentCount = course.Checkpoints.Count;
            cam = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;

            boss = LevelController.Current ? LevelController.Current.Boss as VioletBoss : FindAnyObjectByType<VioletBoss>();
            if (boss)
            {
                boss.SetArena(course.ArenaFloor, course.ArenaMinX, course.ArenaMaxX);
                boss.PlaceAt(course.DaisFeet);
            }

            int cp = VioletCheckpoint.Index;
            for (int i = 0; i <= Mathf.Min(cp, course.Checkpoints.Count - 1); i++) course.LightCheckpoint(i, false);
            PlacePlayer(cp >= VioletCheckpoint.ArenaIndex ? course.ArenaSpawn : course.Checkpoints.Count > 0 ? course.Checkpoints[Mathf.Clamp(cp, 0, course.Checkpoints.Count - 1)] : Vector2.zero);

            yield return null; // every loadout has synced with the save by now
            GrantAbilitiesForTesting();

            if (cp >= VioletCheckpoint.ArenaIndex)
            {
                BeginDuel(true, cp >= VioletCheckpoint.TwinBladesIndex);
                yield break;
            }
            if (!VioletCheckpoint.IntroSeen)
            {
                VioletCheckpoint.IntroSeen = true;
                yield return Intro();
            }
            approachRunning = true;
        }

        void PlacePlayer(Vector2 feet)
        {
            var pc = PlayerController.Instance;
            if (!pc) return;
            var col = pc.GetComponent<Collider2D>();
            float half = col ? col.bounds.extents.y : 0.6f;
            var p = new Vector2(feet.x, feet.y + half + 0.05f);
            pc.transform.position = p;
            if (pc.Body) { pc.Body.position = p; pc.Body.linearVelocity = Vector2.zero; }
            if (cam) cam.SnapToPlayer();
        }

        void GrantAbilitiesForTesting()
        {
#if UNITY_EDITOR
            var pc = PlayerController.Instance;
            if (!grantAllAbilitiesInEditor || !pc) return;
            var granted = new List<AbilityId>();
            foreach (var id in new[] { AbilityId.LightShot, AbilityId.Dash, AbilityId.BlazeStrike, AbilityId.DoubleJump, AbilityId.DownDash, AbilityId.Serenity })
            {
                if (pc.Loadout.Has(id) || !pc.Loadout.Get(id)) continue;
                pc.Loadout.Grant(id);
                granted.Add(id);
            }
            if (granted.Count > 0) Debug.Log($"[Violet] Editor testing: granted {string.Join(", ", granted)} (grantAllAbilitiesInEditor; the save is untouched).", this);
#endif
        }

        // ---------- Intro ----------

        IEnumerator Intro()
        {
            var pc = PlayerController.Instance;
            if (!cam || !boss || !pc) yield break;
            Game.Input.BlockGameplay();
            inputBlocked = true;

            boss.IntroStance(true);
            boss.Glint();
            var king = (Vector2)boss.transform.position + new Vector2(0f, -boss.GroundedY + course.ArenaFloor) + introFrame;
            cam.Hold(king, true);
            GameEvents.RaiseTitleCardShown(title, subtitle);

            float t = 0f, nextGlint = 0.6f;
            bool skipped = false, volleyed = false;
            while (t < introHold && !skipped)
            {
                yield return null;
                t += Time.deltaTime;
                if (t >= nextGlint) { boss.Glint(); nextGlint += 0.7f; }
                skipped = SkipPressed(t);
            }

            for (float u = 0f; u < 1f && !skipped;)
            {
                yield return null;
                t += Time.deltaTime;
                u = Mathf.Min(1f, u + Time.deltaTime / Mathf.Max(0.1f, introPull));
                // A hard pull: slow off the king, fast across the course, easing into the player.
                float e = u * u * u * (u * (u * 6f - 15f) + 10f);
                var target = (Vector2)pc.transform.position + cam.Offset;
                cam.Hold(Vector2.Lerp(king, target, e), true);
                if (u > 0.55f) boss.IntroStance(false);
                if (!volleyed && u > 0.85f) { volleyed = true; StartCoroutine(OpeningVolley()); }
                skipped = SkipPressed(t);
            }

            boss.IntroStance(false);
            cam.Release();
            cam.SnapToPlayer();
            if (!volleyed) StartCoroutine(OpeningVolley());
            Game.Input.UnblockGameplay();
            inputBlocked = false;
        }

        bool SkipPressed(float elapsed) =>
            elapsed >= introSkipAfter && (Game.Time == null || !Game.Time.IsPaused) && Game.Input.Intent.confirmPressed;

        /// <summary>His first shot as the camera arrives: a few gold orbs from off-screen (punch them back, or dodge).</summary>
        IEnumerator OpeningVolley()
        {
            var pc = PlayerController.Instance;
            if (!pc || !boss) yield break;
            float y = pc.transform.position.y;
            VioletTelegraph.Edge(y - 0.6f, y + 0.6f, 0.6f, VioletShots.ReflectableColor);
            boss.FarGesture(VioletBoss.Far.Throw, 0.5f);
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < openingOrbs && pc; i++)
            {
                var from = new Vector2(CamRight + 1f, pc.transform.position.y + 0.3f);
                var dir = ((Vector2)pc.transform.position - from).normalized;
                VioletShots.Orb(from, dir, openingOrbSpeed, true, boss.transform, false);
                yield return new WaitForSeconds(0.45f);
            }
        }

        // ---------- The approach ----------

        void Update()
        {
            var pc = PlayerController.Instance;
            if (!pc || duelStarted) return;
            Vector2 p = pc.transform.position;

            // Checkpoints reached (the banner lights up).
            for (int i = VioletCheckpoint.Index + 1; i < course.Checkpoints.Count; i++)
            {
                if (p.x < course.Checkpoints[i].x - 0.5f) break;
                VioletCheckpoint.Reach(i);
                course.LightCheckpoint(i, true);
            }

            if (!approachRunning) return;
            if (p.x >= course.ArenaEntryX) { BeginDuel(false, false); return; }

            VioletCourse.Zone zone = null;
            foreach (var z in course.Zones)
                if (z.Contains(p) && !(z.type == VioletCourse.Encounter.Rain && z.done)) { zone = z; break; }
            // A sealed rain hall runs to the end whatever the player does.
            if (activeZone != null && activeZone.type == VioletCourse.Encounter.Rain && !activeZone.done) return;
            if (zone == activeZone) return;

            StopEncounter();
            activeZone = zone;
            if (zone == null) return;
            switch (zone.type)
            {
                case VioletCourse.Encounter.Waves: encounter = StartCoroutine(Waves(zone)); break;
                case VioletCourse.Encounter.Slams: encounter = StartCoroutine(Slams(zone)); break;
                case VioletCourse.Encounter.LowBeam: encounter = StartCoroutine(LowBeam(zone)); break;
                case VioletCourse.Encounter.Rain: encounter = StartCoroutine(Rain(zone)); break;
                case VioletCourse.Encounter.GatePressure: encounter = StartCoroutine(GatePressure(zone)); break;
                case VioletCourse.Encounter.Curtain: zone.curtain.Active = true; break;
            }
        }

        void StopEncounter()
        {
            if (encounter != null) StopCoroutine(encounter);
            encounter = null;
            if (activeZone?.curtain) activeZone.curtain.Active = false;
            foreach (var go in live)
                if (go) Destroy(go);
            live.Clear();
            activeZone = null;
        }

        VioletTelegraph Live(VioletTelegraph t)
        {
            live.RemoveAll(g => !g);
            live.Add(t.gameObject);
            return t;
        }

        float CamRight => Camera.main ? Camera.main.transform.position.x + Camera.main.orthographicSize * Camera.main.aspect : 0f;
        float CamTop => Camera.main ? Camera.main.transform.position.y + Camera.main.orthographicSize : 10f;

        IEnumerator Waves(VioletCourse.Zone z)
        {
            yield return new WaitForSeconds(z.first);
            while (true)
            {
                Live(VioletTelegraph.Edge(z.bottom, z.top, waveTelegraph, attackColor));
                boss.FarGesture(VioletBoss.Far.Slash, waveTelegraph);
                yield return new WaitForSeconds(waveTelegraph);
                float x = Mathf.Min(CamRight + 1.5f, z.spawnX);
                VioletWave.Spawn(x, z.bottom, z.top, -1, waveSpeed, z.endX, waveThickness, 1, attackColor);
                yield return new WaitForSeconds(z.interval);
            }
        }

        IEnumerator Slams(VioletCourse.Zone z)
        {
            yield return new WaitForSeconds(z.first);
            while (true)
            {
                boss.FarGesture(VioletBoss.Far.Slam, slamWindUp);
                foreach (var l in z.landings)
                    Live(VioletTelegraph.Spot(new Vector2(l.y - 0.4f, l.z + rippleHeight * 0.5f), new Vector2(0.35f, rippleHeight), slamWindUp, attackColor));
                yield return new WaitForSeconds(slamWindUp);
                VioletHits.ShakeCamera(0.2f, 0.25f);
                foreach (var l in z.landings)
                    VioletWave.Spawn(l.y - 0.4f, l.z, l.z + rippleHeight, -1, rippleSpeed, l.x + 0.2f, 0.5f, 1, attackColor);
                yield return new WaitForSeconds(z.interval);
            }
        }

        IEnumerator LowBeam(VioletCourse.Zone z)
        {
            if (z.first > 0f) yield return new WaitForSeconds(z.first);
            while (true)
            {
                var a = z.beamArea;
                Live(VioletTelegraph.Edge(a.yMin, Mathf.Min(a.yMax, CamTop), z.telegraph, attackColor));
                boss.FarGesture(VioletBoss.Far.Cast, z.telegraph);
                VioletBeam.Band(a, z.telegraph, z.fire, 1, attackColor);
                yield return new WaitForSeconds(z.telegraph + z.fire + z.interval);
            }
        }

        IEnumerator Rain(VioletCourse.Zone z)
        {
            foreach (var seal in z.seals) seal.Close(sealTime);
            boss.FarGesture(VioletBoss.Far.RaiseSword, z.telegraph);
            Live(VioletTelegraph.Top(z.fromX, z.toX, z.telegraph, attackColor));
            yield return new WaitForSeconds(z.telegraph);

            float fall = (rainHeight - 1f) / rainSpeed;
            float next = 0f;
            for (float t = 0f; t < z.duration; t += Time.deltaTime)
            {
                while (next <= t)
                {
                    next += 1f / Mathf.Max(1f, rainRate);
                    float x = Random.Range(z.fromX, z.toX);
                    VioletTelegraph.Spot(new Vector2(x, 0.06f), new Vector2(0.45f, 0.1f), fall, attackColor);
                    VioletShots.Sword(new Vector2(x, rainHeight), rainSpeed, new Color(0.85f, 0.75f, 1f, 0.85f));
                }
                yield return null;
            }
            yield return new WaitForSeconds(fall + 0.4f);
            foreach (var seal in z.seals) seal.Open(sealTime);
            z.done = true;
            activeZone = null;
            encounter = null;
        }

        IEnumerator GatePressure(VioletCourse.Zone z)
        {
            yield return new WaitForSeconds(z.first);
            int n = 0;
            while (z.crystal && !z.crystal.Shattered)
            {
                var pc = PlayerController.Instance;
                if (!pc) yield break;
                if (lobEvery > 0 && ++n % lobEvery == 0)
                {
                    // A gold orb lobbed over the crystal: dodge it or punch it away.
                    boss.FarGesture(VioletBoss.Far.Throw, 0.5f);
                    yield return new WaitForSeconds(0.5f);
                    var from = new Vector2(z.crystal.transform.position.x, CamTop + 2f);
                    var to = (Vector2)pc.transform.position;
                    const float time = 1.1f, gravityScale = 1.4f;
                    float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
                    var v = new Vector2((to.x - from.x) / time, (to.y - from.y + 0.5f * g * time * time) / time);
                    var orb = VioletShots.Orb(from, v, v.magnitude, true, boss.transform, false);
                    orb.Arc(v, gravityScale);
                }
                else
                {
                    boss.FarGesture(VioletBoss.Far.Cast, pressureSwordWarn);
                    float x = pc.transform.position.x;
                    float fall = (rainHeight - 1f) / rainSpeed;
                    Live(VioletTelegraph.Spot(new Vector2(x, 0.06f), new Vector2(0.6f, 0.12f), pressureSwordWarn, attackColor));
                    yield return new WaitForSeconds(Mathf.Max(0f, pressureSwordWarn - fall));
                    VioletShots.Sword(new Vector2(x, rainHeight), rainSpeed, new Color(0.85f, 0.75f, 1f, 0.85f));
                }
                yield return new WaitForSeconds(z.interval);
            }
        }

        // ---------- The arena ----------

        void BeginDuel(bool fromCheckpoint, bool twinBlades)
        {
            if (duelStarted) return;
            duelStarted = true;
            approachRunning = false;
            StopEncounter();
            foreach (var z in course.Zones) if (z.curtain) z.curtain.Active = false;

            VioletCheckpoint.Reach(VioletCheckpoint.ArenaIndex);
            if (course.ArenaDoor) course.ArenaDoor.Close(fromCheckpoint ? 0f : 0.6f);
            if (cam) cam.Hold(course.ArenaCamera, fromCheckpoint);

            if (!boss) { LevelController.Current?.StartBoss(); return; }
            if (fromCheckpoint)
            {
                if (course.Dais) course.Dais.Open(0f);
                boss.PrepareFloorStart(course.ArenaMaxX - 6f, twinBlades);
            }
            else StartCoroutine(SinkDais());
            LevelController.Current?.StartBoss();
        }

        IEnumerator SinkDais()
        {
            yield return new WaitForSeconds(1.3f); // he has leapt off it by then
            if (course.Dais) course.Dais.Open(1.2f);
        }

        void LateUpdate()
        {
            if (duelStarted && phaseThreeCheckpoint && boss && boss.TwinBlades && !boss.Health.IsDead)
                VioletCheckpoint.Reach(VioletCheckpoint.TwinBladesIndex);
        }
    }
}
