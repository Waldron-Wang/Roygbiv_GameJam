using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Runs Violet's level around the duel: the run (phase 1), the arrival cinematic, and the hand-off to the fight.
    ///   Intro:   first entry only. Input blocked, the camera on the end of the course with the king big on his hill,
    ///            then a hard pull back along the whole course to the player (he recedes into the distance).
    ///   The run: the player runs right through the course on their own (no auto-scroll); the camera looks ahead to the
    ///            right. The king is ALWAYS on screen: a big distant figure in the background (behind the level, no
    ///            collision, hazy), feet on a far hill pinned near the right of the view, growing bigger and clearer
    ///            as the player nears the end. While the player is inside a course zone, that zone's long-range attack
    ///            runs: it starts with HIS gesture (the sword raised for waves, slammed down for ripples, the free hand
    ///            for beams, rain and needles) and is announced at the right screen edge (VioletTelegraph).
    ///   Arrival: reaching the foot of his hill takes the controls: letterbox in, a violet wipe cuts to the arena, he
    ///            draws his planted greatsword, swings it overhead and points it at the player, the name card appears,
    ///            the letterbox slides out and the duel starts (HP bar). Skippable with confirm once it's been seen.
    ///            Respawning at the arena plays a short version; at Twin Blades the fight resumes straight away.
    /// Checkpoints: one per segment, the arena, and Twin Blades (phaseThreeCheckpoint), in VioletCheckpoint.
    /// Runs late, so the distant king sticks to the view after the camera has moved.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class VioletApproach : MonoBehaviour
    {
        enum Stage { Setup, Intro, Run, Arrival, Duel }

        [SerializeField] VioletCourse course;

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
        [Tooltip("Size of the distant king at the start and at the end of the course (1 = life size).")]
        [SerializeField] Vector2 farScale = new(1.25f, 2.1f);
        [Tooltip("How hazy (faded toward the distance) he is at the start and at the end of the course.")]
        [SerializeField] Vector2 farHaze = new(0.6f, 0.12f);
        [Tooltip("How far in from the right screen edge he stands, in his own (scaled) units.")]
        [SerializeField] float farInset = 2.4f;
        [Tooltip("Height of his feet (the far hill's top) relative to the camera center.")]
        [SerializeField] float hillLine = -1.9f;
        [Tooltip("The far hill under his feet.")]
        [SerializeField] Color hillColor = new(0.15f, 0.1f, 0.22f);
        [Tooltip("While the needle curtain is running, he raises his hand again every this many seconds.")]
        [SerializeField] float curtainGestureEvery = 1.6f;

        [Header("Arrival cinematic")]
        [Tooltip("Color of the wipe that cuts to the arena.")]
        [SerializeField] Color wipeColor = new(0.45f, 0.2f, 0.75f);
        [Tooltip("Seconds of the wipe (the cut happens halfway).")]
        [SerializeField] float wipeTime = 0.8f;
        [Tooltip("Seconds the letterbox bars take to slide in / out.")]
        [SerializeField] float letterboxTime = 0.35f;

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
        Stage stage = Stage.Setup;
        SpriteRenderer farHill;
        bool inputBlocked, skipped, skippable;
        float introBlend = 1f, nextCurtainGesture;

        void Awake()
        {
            if (!course) course = GetComponent<VioletCourse>();
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
            course.Build();
            VioletCheckpoint.Bind(gameObject.scene.name);
            VioletCheckpoint.SegmentCount = course.Checkpoints.Count;
            cam = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;

            boss = LevelController.Current ? LevelController.Current.Boss as VioletBoss : FindAnyObjectByType<VioletBoss>();
            if (boss)
            {
                boss.SetArena(course.ArenaFloor, course.ArenaMinX, course.ArenaMaxX);
                boss.SetDistant(true);
            }
            BuildFarHill();

            int cp = VioletCheckpoint.Index;
            for (int i = 0; i <= Mathf.Min(cp, course.Checkpoints.Count - 1); i++) course.LightCheckpoint(i, false);

            if (cp >= VioletCheckpoint.ArenaIndex)
            {
                stage = Stage.Arrival;
                CutToArena(false);
                yield return null; // every loadout has synced with the save by now
                GrantAbilitiesForTesting();
                yield return ArriveFromCheckpoint(cp >= VioletCheckpoint.TwinBladesIndex);
                yield break;
            }

            PlacePlayer(course.Checkpoints.Count > 0 ? course.Checkpoints[Mathf.Clamp(cp, 0, course.Checkpoints.Count - 1)] : Vector2.zero);
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

        // ---------- Intro ----------

        IEnumerator Intro()
        {
            var pc = PlayerController.Instance;
            if (!cam || !boss || !pc) yield break;
            stage = Stage.Intro;
            BlockInput(true);

            // The end of the course, as if the player stood there: the king is close and big on his hill.
            var endView = new Vector2(course.EndX, course.ArenaFloor + 0.6f) + cam.Offset;
            cam.Hold(endView, true);
            introBlend = 0f;
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
                // A hard pull: slow off the king, fast across the course, easing into the player. He recedes as it goes.
                float e = u * u * u * (u * (u * 6f - 15f) + 10f);
                cam.Hold(Vector2.Lerp(endView, (Vector2)pc.transform.position + cam.Offset, e), true);
                introBlend = e;
                if (u > 0.55f) boss.IntroStance(false);
                if (!volleyed && u > 0.85f) { volleyed = true; StartCoroutine(OpeningVolley()); }
                skip = ConfirmPressed;
            }

            introBlend = 1f;
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

        // ---------- The distant king ----------

        void BuildFarHill()
        {
            var shape = VioletShapes.Polygon("FarHill", Vector2.zero, 0.45f,
                new(-8f, -0.6f), new(-5f, -0.2f), new(-2.2f, 0f), new(1.2f, 0f), new(3.8f, -0.25f), new(6.5f, -0.9f), new(9f, -2f),
                new(9f, -14f), new(-8f, -14f));
            farHill = VioletShapes.Create("FarHill (his hill)", shape, transform, Vector2.zero, hillColor, -86);
        }

        void LateUpdate()
        {
            if (stage is Stage.Setup or Stage.Intro or Stage.Run) PlaceFarKing();
            if (stage == Stage.Duel && phaseThreeCheckpoint && boss && boss.TwinBlades && !boss.Health.IsDead)
                VioletCheckpoint.Reach(VioletCheckpoint.TwinBladesIndex);
        }

        /// <summary>Pins him near the right of the view on his far hill, bigger and clearer the closer the player is to the end.</summary>
        void PlaceFarKing()
        {
            var c = Camera.main;
            var pc = PlayerController.Instance;
            if (!c || !boss || !pc) return;
            float half = c.orthographicSize * c.aspect;
            Vector2 center = c.transform.position;
            float progress = Mathf.InverseLerp(course.StartX, course.EndX, pc.transform.position.x);
            progress = progress * progress * (3f - 2f * progress);
            float scale = Mathf.Lerp(farScale.x, farScale.y, progress);
            float haze = Mathf.Lerp(farHaze.x, farHaze.y, progress);
            // The intro starts on him close up, then he recedes as the camera pulls away.
            scale = Mathf.Lerp(farScale.y, scale, introBlend);
            haze = Mathf.Lerp(farHaze.y, haze, introBlend);

            var feet = new Vector2(center.x + half - farInset * scale, center.y + hillLine);
            boss.PlaceDistant(feet, scale, haze);
            if (farHill)
            {
                farHill.enabled = true;
                farHill.transform.position = feet;
                farHill.transform.localScale = Vector3.one * scale;
                farHill.color = Color.Lerp(hillColor, new Color(0.3f, 0.24f, 0.42f), haze * 0.5f);
            }
        }

        // ---------- The run ----------

        void Update()
        {
            var pc = PlayerController.Instance;
            if (!pc || stage != Stage.Run) return;
            Vector2 p = pc.transform.position;

            // Checkpoints reached (the banner lights up).
            for (int i = VioletCheckpoint.Index + 1; i < course.Checkpoints.Count; i++)
            {
                if (p.x < course.Checkpoints[i].x - 0.5f) break;
                VioletCheckpoint.Reach(i);
                course.LightCheckpoint(i, true);
            }

            if (p.x >= course.EndX) { StartCoroutine(Arrival()); return; }

            // The curtain runs itself; he keeps his hand raised over it.
            if (activeZone?.curtain && activeZone.curtain.Active && p.x > activeZone.x0 + 8f && Time.time >= nextCurtainGesture)
            {
                nextCurtainGesture = Time.time + curtainGestureEvery;
                boss.FarGesture(VioletBoss.Far.Cast, 0.5f);
            }

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

            // Spawn below this hall's hanging slab/anchor, but above the eventual landed roof.
            // Other Violet attacks continue using rainHeight.
            float spawnY = z.slab ? Mathf.Min(rainHeight, z.slab.RainSpawnY) : rainHeight;
            float fall = (spawnY - 1f) / rainSpeed;
            float next = 0f;
            for (float t = 0f; t < z.duration; t += Time.deltaTime)
            {
                while (next <= t)
                {
                    next += 1f / Mathf.Max(1f, rainRate);
                    float x = Random.Range(z.fromX, z.toX);
                    VioletTelegraph.Spot(new Vector2(x, 0.06f), new Vector2(0.45f, 0.1f), fall, attackColor);
                    VioletShots.Sword(new Vector2(x, spawnY), rainSpeed, new Color(0.85f, 0.75f, 1f, 0.85f));
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

        // ---------- The arrival ----------

        /// <summary>The player reached the foot of his hill: the cinematic cut to the arena, then the duel.</summary>
        IEnumerator Arrival()
        {
            stage = Stage.Arrival;
            StopEncounter();
            foreach (var z in course.Zones) if (z.curtain) z.curtain.Active = false;
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
            StartDuel(false);
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
            if (farHill) farHill.enabled = false;
            PlacePlayer(course.ArenaSpawn);
            if (cam)
            {
                cam.Lead = Vector2.zero;
                cam.Hold(course.ArenaCamera, true);
            }
            if (!boss) return;
            boss.PlaceInArena(course.ArenaKingX);
            if (plantedSword) boss.PlantSword();
        }

        /// <summary>Respawned at the arena: a short beat (he raises the sword, points it), or straight in at Twin Blades.</summary>
        IEnumerator ArriveFromCheckpoint(bool twinBlades)
        {
            if (twinBlades || !boss)
            {
                StartDuel(twinBlades);
                yield break;
            }
            BlockInput(true);
            bool done = false;
            boss.StartCoroutine(Beat(false, () => done = true));
            for (float t = 0f; !done && t < 2f; t += Time.deltaTime) yield return null;
            BlockInput(false);
            StartDuel(false);
        }

        void StartDuel(bool twinBlades)
        {
            stage = Stage.Duel;
            VioletCheckpoint.Reach(VioletCheckpoint.ArenaIndex);
            if (cam) cam.Hold(course.ArenaCamera);
            if (boss) boss.PrepareFloorStart(course.ArenaKingX, twinBlades);
            LevelController.Current?.StartBoss();
        }
    }
}
