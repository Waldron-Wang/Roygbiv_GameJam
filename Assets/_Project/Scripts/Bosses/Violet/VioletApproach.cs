using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Runs Violet's level around the duel: the run (phase 1), the arrival cinematic, and the hand-off to the fight.
    ///   Intro:   first entry only. Input blocked, the camera on the king standing on his hill at the end of the course,
    ///            then a hard pull back along the whole course to the player.
    ///   The run: the player runs right through the course on their own (no auto-scroll); the camera looks ahead to the
    ///            right. The king STANDS ON HIS HILL at the far end (VioletFarKing marks the spot; his feet go on the tile
    ///            ground under it), a real figure at his real place, drawn in front of the tilemap like in the duel: off
    ///            screen for most of the run, in view as the player nears the end. While the player is inside a course
    ///            zone, that zone's long-range attack runs: it starts with HIS gesture (the sword raised for waves,
    ///            slammed down for ripples, the free hand for beams, rain and needles; seen once he's on screen) and is
    ///            announced at the right screen edge (VioletTelegraph).
    ///   Arrival: reaching the foot of his hill takes the controls: letterbox in, a violet wipe cuts to the arena, he
    ///            draws his planted greatsword, swings it overhead and points it at the player, the name card appears,
    ///            the letterbox slides out and the duel starts (HP bar). Skippable with confirm once it's been seen.
    ///            Respawning at the arena plays a short version; at Twin Blades the fight resumes straight away.
    /// Checkpoints: one per approach segment and the arena entrance. Duel retries always start at full boss health.
    /// Everything it runs is placed in the scene (ROYGBIV > Bake Violet Level Into Scene made the first version; move,
    /// add or retune them by hand): VioletCheckpointMarker (ordered by x), VioletZone (one per attack, checked left to
    /// right), VioletArrival, VioletFarKing and VioletArena. Missing any but the zones: one error, and nothing runs.
    /// Safety net: if the tilemap's merged collider was saved empty (nothing solid), Awake rebuilds it from the tiles.
    /// Runs after the boss (its Start places him).
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class VioletApproach : MonoBehaviour
    {
        enum Stage { Setup, Intro, Run, Arrival, Duel }

        [Header("Intro")]
        [Tooltip("Seconds the camera holds on the king before the pull.")]
        [SerializeField] float introHold = 1.5f;
        [Tooltip("Seconds of the pull back along the course to the player.")]
        [SerializeField] float introPull = 2.75f;
        [Tooltip("Confirm skips the intro after this many seconds.")]
        [SerializeField] float introSkipAfter = 1f;

        [Header("The run")]
        [Tooltip("Camera look-ahead to the right during the run, so incoming attacks (and the king) are in view.")]
        [SerializeField] float lookAhead = 5f;
        [Tooltip("Minimum visible width / height during the approach, independent of screen aspect ratio.")]
        [SerializeField] Vector2 approachViewSize = new(22f, 13f);
        [Tooltip("Seconds the camera takes to ease between approach and arena framing.")]
        [SerializeField] float cameraZoomTime = 0.4f;
        [SerializeField] float duelViewSize = 7f;
        [Tooltip("While the needle curtain is running, he raises his hand again every this many seconds.")]
        [SerializeField] float curtainGestureEvery = 1.6f;

        [Header("Arrival cinematic")]
        [Tooltip("Color of the wipe that cuts to the arena.")]
        [SerializeField] Color wipeColor = new(0.45f, 0.2f, 0.75f);
        [Tooltip("Seconds of the wipe (the cut happens halfway).")]
        [SerializeField] float wipeTime = 0.8f;
        [Tooltip("Seconds the letterbox bars take to slide in / out.")]
        [SerializeField] float letterboxTime = 0.35f;

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
        [Tooltip("Ripple speed.")]
        [SerializeField] float rippleSpeed = 9f;

        [Header("Royal Rain")]
        [Tooltip("Swords per second during the barrage: dense enough that 7 s in the open costs more than 5 HP.")]
        [SerializeField] float rainRate = 30f;
        [Tooltip("Height above the hall's floor the swords fall from.")]
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
        // The level, read from the scene.
        VioletCheckpointMarker[] checkpoints = { };
        VioletZone[] zones = { };
        VioletArrival arrival;
        VioletFarKing far;
        VioletArena arena;
        VioletBoss boss;
        CameraFollow cam;
        Camera viewCamera;
        float viewSize, zoomVelocity;
        VioletZone activeZone;
        Coroutine encounter;
        Stage stage = Stage.Setup;
        Vector2 kingFeet; // where he stands during the run: on the tile ground under VioletFarKing
        bool inputBlocked, skipped, skippable;
        float nextCurtainGesture;

        // A scene saved before its tile collider was generated has an EMPTY merged collider: the player falls through
        // everything. Rebuild it from the tiles before anything lands on it.
        void Awake()
        {
            foreach (var composite in VioletTerrain.Composites(gameObject.scene))
            {
                if (composite.pathCount > 0) continue;
                int paths = VioletTerrain.Rebuild(composite);
                Debug.LogWarning($"[Violet] {composite.name}'s collider was saved EMPTY (nothing was solid): rebuilt it from the tiles at runtime " +
                                 $"({paths} paths). In the editor run ROYGBIV > Fix Violet Colliders, then commit Level_Violet.", composite);
            }
        }

        void OnEnable() => GameEvents.LevelCompleted += OnLevelCompleted;
        void OnDisable() => GameEvents.LevelCompleted -= OnLevelCompleted;

        void OnDestroy()
        {
            // Reloaded mid-cinematic (pause menu restart): never leave the input blocked.
            if (inputBlocked && Game.Input != null) Game.Input.UnblockGameplay();
            inputBlocked = false;
        }

        void OnLevelCompleted(ColorId color)
        {
            if (color != ColorId.Violet) return;
            VioletCheckpoint.Clear();
            StopEncounter();
        }

        IEnumerator Start()
        {
            if (!ReadLevel())
            {
                enabled = false;
                yield break;
            }
            VioletCheckpoint.Bind(gameObject.scene.name);
            VioletCheckpoint.SegmentCount = checkpoints.Length;
            viewCamera = Camera.main;
            cam = viewCamera ? viewCamera.GetComponent<CameraFollow>() : null;
            if (viewCamera) viewSize = viewCamera.orthographicSize;

            boss = LevelController.Current ? LevelController.Current.Boss as VioletBoss : FindAnyObjectByType<VioletBoss>();
            kingFeet = KingFeet();
            if (boss)
            {
                boss.SetArena(arena.Floor, arena.MinX, arena.MaxX);
                boss.PlaceOnHill(kingFeet);
            }

            int cp = VioletCheckpoint.Index;
            for (int i = 0; i <= Mathf.Min(cp, checkpoints.Length - 1); i++) checkpoints[i].Light(false);

            if (cp >= VioletCheckpoint.ArenaIndex)
            {
                stage = Stage.Arrival;
                CutToArena(false);
                yield return null; // every loadout has synced with the save by now
                GrantAbilitiesForTesting();
                yield return ArriveFromCheckpoint();
                yield break;
            }

            PlacePlayer(checkpoints[Mathf.Clamp(cp, 0, checkpoints.Length - 1)].Feet);
            if (cam) cam.Lead = new Vector2(lookAhead, 0f);
            if (cam) cam.SnapToPlayer();
            yield return null;
            GrantAbilitiesForTesting();

            if (!VioletCheckpoint.IntroSeen)
            {
                VioletCheckpoint.IntroSeen = true;
                yield return Intro();
            }
            stage = Stage.Run;
        }

        /// <summary>Finds the level's pieces in the scene. False (and one error) if it was never baked.</summary>
        bool ReadLevel()
        {
            var scene = gameObject.scene;
            T[] InScene<T>() where T : Component => System.Array.FindAll(FindObjectsByType<T>(), c => c.gameObject.scene == scene);
            checkpoints = InScene<VioletCheckpointMarker>();
            System.Array.Sort(checkpoints, (a, b) => a.Feet.x.CompareTo(b.Feet.x));
            zones = InScene<VioletZone>();
            System.Array.Sort(zones, (a, b) => a.Area.xMin.CompareTo(b.Area.xMin));
            var arrivals = InScene<VioletArrival>();
            var fars = InScene<VioletFarKing>();
            var arenas = InScene<VioletArena>();
            arrival = arrivals.Length > 0 ? arrivals[0] : null;
            far = fars.Length > 0 ? fars[0] : null;
            arena = arenas.Length > 0 ? arenas[0] : null;

            var missing = new List<string>();
            if (checkpoints.Length == 0) missing.Add("checkpoints");
            if (!arrival) missing.Add("the arrival");
            if (!far) missing.Add("the far king");
            if (!arena) missing.Add("the arena");
            if (missing.Count == 0) return true;
            Debug.LogError($"[Violet] Level_Violet has no {string.Join(", ", missing)} (it isn't baked): nothing runs. In the editor, open Level_Violet, " +
                           "run ROYGBIV > Bake Violet Level Into Scene, then save the scene and commit it.", this);
            return false;
        }

        void PlacePlayer(Vector2 feet)
        {
            var pc = PlayerController.Instance;
            if (!pc) return;
            StopPlayer(pc);
            var col = pc.GetComponent<Collider2D>();
            float half = col ? col.bounds.extents.y : 0.6f;
            var p = new Vector2(feet.x, feet.y + half + 0.05f);
            pc.transform.position = p;
            if (pc.Body) { pc.Body.position = p; pc.Body.linearVelocity = Vector2.zero; }
        }

        /// <summary>Ends whatever move the player is in (a surf, a shove), so a teleport lands them standing still.</summary>
        static void StopPlayer(PlayerController pc)
        {
            foreach (var d in pc.GetComponentsInChildren<DownDashAbility>()) d.Cancel();
            if (pc.TryGetComponent<VioletShove>(out var shove)) Destroy(shove);
            pc.MovementLocked = false;
            if (pc.Body) pc.Body.linearVelocity = Vector2.zero;
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

        void BlockInput(bool block)
        {
            if (block == inputBlocked || Game.Input == null) return;
            inputBlocked = block;
            if (block) Game.Input.BlockGameplay();
            else Game.Input.UnblockGameplay();
        }

        // Keep Violet's presentation wide without changing other levels' follow behavior.
        Rect ArenaView
        {
            get
            {
                var a = arena.inner;
                var v = arena.cameraView;
                const float margin = 0.5f;
                var view = Rect.MinMaxRect(Mathf.Min(a.xMin, v.xMin) - margin, Mathf.Min(a.yMin, v.yMin) - margin,
                    Mathf.Max(a.xMax, v.xMax) + margin, Mathf.Max(a.yMax, v.yMax) + margin);
                view.position += (Vector2)arena.transform.position;
                return view;
            }
        }

        void LateUpdate()
        {
            if (!viewCamera || stage == Stage.Setup || Time.deltaTime <= 0f) return;
            bool inArena = arena && (stage == Stage.Arrival || stage == Stage.Duel);
            var frame = inArena ? ArenaView.size : approachViewSize;
            float target = stage == Stage.Duel ? duelViewSize
                : Mathf.Max(frame.y * 0.5f, frame.x * 0.5f / Mathf.Max(0.01f, viewCamera.aspect));
            var warp = viewCamera.GetComponent<ScreenWarp>();
            // Reserve enough space that the warp's breathing zoom cannot crop the fitted frame.
            if (stage != Stage.Duel && warp && warp.enabled) target /= Mathf.Max(0.01f, 1f - Mathf.Abs(warp.Current.zoomPulse));
            viewSize = Mathf.SmoothDamp(viewSize, target, ref zoomVelocity, cameraZoomTime);
            if (warp) warp.SetBaseSize(viewSize);
            else viewCamera.orthographicSize = viewSize;
        }

        // ---------- Intro ----------

        IEnumerator Intro()
        {
            var pc = PlayerController.Instance;
            if (!cam || !boss || !pc) yield break;
            stage = Stage.Intro;
            BlockInput(true);

            // The end of the course: the king standing on his hill, framed from his real position.
            var endView = kingFeet + far.introCamera;
            cam.Hold(endView, true);
            boss.IntroStance(true);
            boss.Glint();

            float t = 0f, nextGlint = 0.6f;
            bool skip = false, volleyed = false;
            while (t < introHold && !skip)
            {
                yield return null;
                t += Time.deltaTime;
                if (t >= nextGlint) { boss.Glint(); nextGlint += 0.7f; }
                skip = t >= introSkipAfter && ConfirmPressed;
            }

            for (float u = 0f; u < 1f && !skip;)
            {
                yield return null;
                t += Time.deltaTime;
                u = Mathf.Min(1f, u + Time.deltaTime / Mathf.Max(0.1f, introPull));
                // A hard pull: slow off the king, fast across the course, easing into the player. He stays on his hill.
                float e = u * u * u * (u * (u * 6f - 15f) + 10f);
                cam.Hold(Vector2.Lerp(endView, (Vector2)pc.transform.position + cam.Offset, e), true);
                if (u > 0.55f) boss.IntroStance(false);
                if (!volleyed && u > 0.85f) { volleyed = true; StartCoroutine(OpeningVolley()); }
                skip = ConfirmPressed;
            }

            boss.IntroStance(false);
            cam.Release();
            cam.SnapToPlayer();
            if (!volleyed) StartCoroutine(OpeningVolley());
            BlockInput(false);
        }

        static bool ConfirmPressed => (Game.Time == null || !Game.Time.IsPaused) && Game.Input.Intent.confirmPressed;

        /// <summary>His first shot as the camera arrives: a few gold orbs from his hand (punch them back, or dodge).</summary>
        IEnumerator OpeningVolley()
        {
            var pc = PlayerController.Instance;
            if (!pc || !boss) yield break;
            float y = pc.transform.position.y;
            VioletTelegraph.Edge(y - 0.6f, y + 0.6f, 0.6f, VioletShots.ReflectableColor);
            boss.FarGesture(VioletBoss.Far.Throw, 0.5f);
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < openingOrbs && pc && stage <= Stage.Run; i++)
            {
                var from = new Vector2(CamRight + 1f, pc.transform.position.y + 0.3f);
                var dir = ((Vector2)pc.transform.position - from).normalized;
                VioletShots.Orb(from, dir, openingOrbSpeed, true, boss.transform, false);
                yield return new WaitForSeconds(0.45f);
            }
        }

        // ---------- The king on his hill ----------

        /// <summary>Where he stands during the run: on the top of the tile ground right under VioletFarKing (his hill).</summary>
        Vector2 KingFeet()
        {
            var spot = far.Spot;
            if (VioletTerrain.GroundBelow(spot + Vector2.up * FarKingLift, FarKingLift + 30f, out float y)) return new Vector2(spot.x, y);
            Debug.LogError($"[Violet] No tile ground under the Far King at {spot}: he'd float. Paint his hill under it " +
                           "(ROYGBIV > Paint Violet King's Hill) or move the Far King over it.", far);
            return spot;
        }

        // How far above VioletFarKing the search for his ground starts, so a marker dropped a little into the hill still works.
        const float FarKingLift = 2f;

        // ---------- The run ----------

        void Update()
        {
            var pc = PlayerController.Instance;
            if (!pc || stage != Stage.Run) return;
            Vector2 p = pc.transform.position;

            // Checkpoints reached (the banner lights up).
            for (int i = VioletCheckpoint.Index + 1; i < checkpoints.Length; i++)
            {
                if (p.x < checkpoints[i].Feet.x - 0.5f) break;
                VioletCheckpoint.Reach(i);
                checkpoints[i].Light(true);
            }

            if (p.x >= arrival.X) { StartCoroutine(Arrival()); return; }

            // The curtain runs itself; he keeps his hand raised over it.
            if (activeZone && activeZone.curtain && activeZone.curtain.Active && p.x > activeZone.Area.xMin + 8f && Time.time >= nextCurtainGesture)
            {
                nextCurtainGesture = Time.time + curtainGestureEvery;
                boss.FarGesture(VioletBoss.Far.Cast, 0.5f);
            }

            VioletZone zone = null;
            foreach (var z in zones)
                if (z && z.isActiveAndEnabled && z.Contains(p) && !(z.kind == VioletZone.Kind.Rain && z.done)) { zone = z; break; }
            // A sealed rain hall runs to the end whatever the player does.
            if (activeZone && activeZone.kind == VioletZone.Kind.Rain && !activeZone.done) return;
            if (zone == activeZone) return;

            StopEncounter();
            activeZone = zone;
            if (!zone) return;
            switch (zone.kind)
            {
                case VioletZone.Kind.Waves: encounter = StartCoroutine(Waves(zone)); break;
                case VioletZone.Kind.Slams: encounter = StartCoroutine(Slams(zone)); break;
                case VioletZone.Kind.LowBeam: encounter = StartCoroutine(LowBeam(zone)); break;
                case VioletZone.Kind.Rain: encounter = StartCoroutine(Rain(zone)); break;
                case VioletZone.Kind.GatePressure: encounter = StartCoroutine(GatePressure(zone)); break;
                case VioletZone.Kind.Curtain:
                    if (zone.curtain) zone.curtain.Active = true;
                    else Debug.LogError($"[Violet] Zone '{zone.label}' is a Curtain zone with no curtain set.", zone);
                    break;
            }
        }

        void StopEncounter()
        {
            if (encounter != null) StopCoroutine(encounter);
            encounter = null;
            if (activeZone && activeZone.curtain) activeZone.curtain.Active = false;
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

        IEnumerator Waves(VioletZone z)
        {
            yield return new WaitForSeconds(z.first);
            while (true)
            {
                Live(VioletTelegraph.Edge(z.WaveBottom, z.WaveTop, waveTelegraph, attackColor));
                boss.FarGesture(VioletBoss.Far.Slash, waveTelegraph);
                yield return new WaitForSeconds(waveTelegraph);
                float x = Mathf.Min(CamRight + 1.5f, z.WaveFrom);
                VioletWave.Spawn(x, z.WaveBottom, z.WaveTop, -1, waveSpeed, z.WaveTo, waveThickness, 1, attackColor);
                yield return new WaitForSeconds(z.interval);
            }
        }

        IEnumerator Slams(VioletZone z)
        {
            yield return new WaitForSeconds(z.first);
            float h = z.rippleHeight;
            while (true)
            {
                boss.FarGesture(VioletBoss.Far.Slam, slamWindUp);
                for (int i = 0; i < z.landings.Length; i++)
                {
                    var l = z.Landing(i);
                    Live(VioletTelegraph.Spot(new Vector2(l.y - 0.4f, l.z + h * 0.5f), new Vector2(0.35f, h), slamWindUp, attackColor));
                }
                yield return new WaitForSeconds(slamWindUp);
                VioletHits.ShakeCamera(0.2f, 0.25f);
                for (int i = 0; i < z.landings.Length; i++)
                {
                    var l = z.Landing(i);
                    VioletWave.Spawn(l.y - 0.4f, l.z, l.z + h, -1, rippleSpeed, l.x + 0.2f, 0.5f, 1, attackColor);
                }
                yield return new WaitForSeconds(z.interval);
            }
        }

        IEnumerator LowBeam(VioletZone z)
        {
            if (z.first > 0f) yield return new WaitForSeconds(z.first);
            while (true)
            {
                var a = z.BeamArea;
                Live(VioletTelegraph.Edge(a.yMin, Mathf.Min(a.yMax, CamTop), z.telegraph, attackColor));
                boss.FarGesture(VioletBoss.Far.Cast, z.telegraph);
                VioletBeam.Band(a, z.telegraph, z.fire, 1, attackColor);
                yield return new WaitForSeconds(z.telegraph + z.fire + z.interval);
            }
        }

        IEnumerator Rain(VioletZone z)
        {
            var hall = z.rainHall;
            if (!hall)
            {
                Debug.LogError($"[Violet] Zone '{z.label}' is a Rain zone with no rain hall set.", z);
                z.done = true;
                activeZone = null;
                encounter = null;
                yield break;
            }
            foreach (var seal in hall.seals) if (seal) seal.Close(sealTime);
            boss.FarGesture(VioletBoss.Far.RaiseSword, hall.telegraph);
            Live(VioletTelegraph.Top(hall.FromX, hall.ToX, hall.telegraph, attackColor));
            yield return new WaitForSeconds(hall.telegraph);

            float floor = hall.transform.position.y;
            float fall = (rainHeight - 1f) / rainSpeed;
            float next = 0f;
            for (float t = 0f; t < hall.duration; t += Time.deltaTime)
            {
                while (next <= t)
                {
                    next += 1f / Mathf.Max(1f, rainRate);
                    float x = Random.Range(hall.FromX, hall.ToX);
                    VioletTelegraph.Spot(new Vector2(x, floor + 0.06f), new Vector2(0.45f, 0.1f), fall, attackColor);
                    VioletShots.Sword(new Vector2(x, floor + rainHeight), rainSpeed, new Color(0.85f, 0.75f, 1f, 0.85f), shelterRain: true);
                }
                yield return null;
            }
            yield return new WaitForSeconds(fall + 0.4f);
            foreach (var seal in hall.seals) if (seal) seal.Open(sealTime);
            z.done = true;
            activeZone = null;
            encounter = null;
        }

        IEnumerator GatePressure(VioletZone z)
        {
            yield return new WaitForSeconds(z.first);
            float floor = z.transform.position.y;
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
                    Live(VioletTelegraph.Spot(new Vector2(x, floor + 0.06f), new Vector2(0.6f, 0.12f), pressureSwordWarn, attackColor));
                    yield return new WaitForSeconds(Mathf.Max(0f, pressureSwordWarn - fall));
                    VioletShots.Sword(new Vector2(x, floor + rainHeight), rainSpeed, new Color(0.85f, 0.75f, 1f, 0.85f));
                }
                yield return new WaitForSeconds(z.interval);
            }
        }

        // ---------- The arrival ----------

        /// <summary>The player reached the foot of his hill: the cinematic cut to the arena, then the duel.</summary>
        IEnumerator Arrival()
        {
            stage = Stage.Arrival;
            StopEncounter();
            foreach (var z in zones) if (z && z.curtain) z.curtain.Active = false;
            BlockInput(true);
            var pc = PlayerController.Instance;
            if (pc) StopPlayer(pc);
            GameEvents.RaiseCinematicChanged(true); // letterbox in; Serenity ends

            skippable = VioletCheckpoint.ArrivalSeen; // the first viewing plays in full
            VioletCheckpoint.ArrivalSeen = true;
            skipped = false;

            yield return WaitOrSkip(letterboxTime);
            if (!skipped)
            {
                GameEvents.RaiseScreenWipe(wipeColor, wipeTime);
                yield return WaitOrSkip(wipeTime * 0.5f); // covered: cut
            }
            CutToArena(true);
            if (!skipped) yield return WaitOrSkip(wipeTime * 0.5f);

            if (!skipped && boss)
            {
                bool done = false;
                boss.StartCoroutine(Beat(true, () => done = true)); // on the boss: FinishArrival can stop it
                while (!done && !skipped)
                {
                    if (skippable && ConfirmPressed) skipped = true;
                    yield return null;
                }
            }
            if (skipped && boss) boss.FinishArrival();

            GameEvents.RaiseCinematicChanged(false); // letterbox out
            yield return new WaitForSeconds(letterboxTime * 0.5f);
            BlockInput(false);
            StartDuel();
        }

        IEnumerator Beat(bool full, System.Action finished)
        {
            yield return boss.ArrivalBeat(full);
            finished();
        }

        IEnumerator WaitOrSkip(float seconds)
        {
            for (float t = 0f; t < seconds && !skipped; t += Time.deltaTime)
            {
                if (skippable && ConfirmPressed) skipped = true;
                yield return null;
            }
        }

        /// <summary>The scene change: the player to the arena's left, the camera locked on the arena, the king on its floor.</summary>
        void CutToArena(bool plantedSword)
        {
            stage = Stage.Arrival;
            PlacePlayer(arena.PlayerFeet);
            if (cam)
            {
                cam.Lead = Vector2.zero;
                cam.Hold(ArenaView.center, true);
            }
            if (!boss) return;
            boss.PlaceInArena(arena.BossX);
            if (plantedSword) boss.PlantSword();
        }

        /// <summary>Respawned at the arena: a short beat, then the whole duel from the beginning.</summary>
        IEnumerator ArriveFromCheckpoint()
        {
            if (!boss)
            {
                StartDuel();
                yield break;
            }
            BlockInput(true);
            bool done = false;
            boss.StartCoroutine(Beat(false, () => done = true));
            for (float t = 0f; !done && t < 2f; t += Time.deltaTime) yield return null;
            BlockInput(false);
            StartDuel();
        }

        void StartDuel()
        {
            stage = Stage.Duel;
            VioletCheckpoint.Reach(VioletCheckpoint.ArenaIndex);
            if (cam)
            {
                cam.Lead = Vector2.zero;
                cam.Release();
            }
            if (boss) boss.PrepareFloorStart(arena.BossX, false);
            LevelController.Current?.StartBoss();
        }
    }
}
