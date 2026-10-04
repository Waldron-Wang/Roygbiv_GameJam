using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE: an endless track built just past the right screen edge and cleared once it's behind the
    /// left one. Flat runs alternate with obstacles picked at random from `obstacles`, and every
    /// `cageEverySeconds` a CageTrap gets a clear stretch of its own.
    ///
    /// Gaps are measured in SECONDS of running (× the current speed), so the rhythm holds as the chase
    /// speeds up. Each boss phase unlocks more obstacle types and tightens the gaps.
    ///
    /// Everything is built from `blockTemplate` (an inactive sprite with Recolorable, so the track grays /
    /// recolors with the rest of the level). Hazards, springs and latches keep their real colors so they
    /// read even while the world is gray.
    /// </summary>
    public class ChaseCourse : MonoBehaviour
    {
        public enum Obstacle { Hurdle, TallHurdle, Pit, HotBeam, CrateWall, SpringWall }

        [Serializable]
        public struct ObstacleOption
        {
            public Obstacle type;
            [Tooltip("Boss phase (= catches so far) from which it can appear.")]
            public int minPhase;
            public float weight;
        }

        [Tooltip("Inactive square sprite with a Recolorable. Copies of it make up the track.")]
        [SerializeField] GameObject blockTemplate;
        [SerializeField] PhysicsMaterial2D solidMaterial;

        [Header("Layout")]
        [Tooltip("Flat ground before the first obstacle.")]
        [SerializeField] float runway = 30f;
        [Tooltip("How far past the right screen edge the track is built.")]
        [SerializeField] float buildAhead = 25f;
        [Tooltip("Ground is one long collider per stretch (seams can snag the player); a new one starts after this length.")]
        [SerializeField] float maxGroundLength = 120f;
        [Tooltip("Seconds of flat running between obstacles, per phase (random in [x, y]).")]
        [SerializeField] Vector2[] gapSecondsPerPhase = { new(1.4f, 2.2f), new(1.1f, 1.8f), new(0.85f, 1.5f) };
        [SerializeField] ObstacleOption[] obstacles =
        {
            new() { type = Obstacle.Hurdle, minPhase = 0, weight = 3f },
            new() { type = Obstacle.Pit, minPhase = 0, weight = 2f },
            new() { type = Obstacle.CrateWall, minPhase = 0, weight = 1.5f },
            new() { type = Obstacle.TallHurdle, minPhase = 1, weight = 2f },
            new() { type = Obstacle.HotBeam, minPhase = 1, weight = 1.5f },
            new() { type = Obstacle.SpringWall, minPhase = 1, weight = 1f },
        };

        [Header("Obstacle shapes")]
        [SerializeField] Vector2 hurdleSize = new(1f, 1f);
        [SerializeField] Vector2 tallHurdleSize = new(1f, 1.8f);
        [Tooltip("Pit width at phase 0; grows per phase.")]
        [SerializeField] float pitWidth = 3f;
        [SerializeField] float pitWidthPerPhase = 0.75f;
        [Tooltip("Hot beam: a low hurdle under a beam that hurts. Bottom of the beam above ground: a full jump hits it, a short hop doesn't.")]
        [SerializeField] float hotBeamHeight = 3.2f;
        [SerializeField] Vector2 hotBeamHurdleSize = new(0.6f, 0.5f);
        [Tooltip("Too tall to jump: shoot it or melee it.")]
        [SerializeField] Vector2 crateSize = new(1.2f, 3.4f);
        [Tooltip("Too tall to jump: the spring before it throws you over. Breakable as a fallback.")]
        [SerializeField] float springWallHeight = 3.5f;
        [SerializeField] int springWallHits = 3;
        [SerializeField] Color crateColor = new(0.75f, 0.45f, 0.2f);
        [SerializeField] Color hazardColor = new(1f, 0.3f, 0.1f);
        [SerializeField] Color springColor = new(0.3f, 0.9f, 0.5f);

        [Header("Cage traps")]
        [SerializeField] float firstCageAfterSeconds = 4f;
        [SerializeField] float cageEverySeconds = 9f;
        [Tooltip("Cage center height above ground while hanging (its bottom edge).")]
        [SerializeField] float cageHangHeight = 5.2f;
        [SerializeField] float latchHeight = 3.4f;
        [SerializeField] Color cageColor = new(1f, 0.85f, 0.4f);

        class Piece { public GameObject go; public float startX, endX; }

        readonly List<Piece> pieces = new();
        readonly List<CageTrap> cages = new();
        ChaseDirector chase;
        OrangeBoss boss;
        Transform track;
        Piece ground; // the stretch currently being extended
        Color groundColor;
        float cursor, groundStart, nextCageX;

        public static ChaseCourse Current { get; private set; }
        public PhysicsMaterial2D SolidMaterial => solidMaterial;

        /// <summary>A hanging cage is on screen: the boss holds its attacks so the player can focus on the shot.</summary>
        public bool CageInPlay
        {
            get
            {
                if (!chase) return false;
                foreach (var c in cages)
                    if (c && c.IsArmed && c.LatchX < chase.RightEdgeX && c.X > chase.LeftEdgeX) return true;
                return false;
            }
        }

        int Phase => boss ? boss.Phase : 0;

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            chase = ChaseDirector.Current;
            boss = LevelController.Current ? LevelController.Current.Boss as OrangeBoss : null;
            groundColor = blockTemplate ? blockTemplate.GetComponent<SpriteRenderer>().color : Color.white;
            track = new GameObject("Track").transform;
            if (!chase || !blockTemplate) { Debug.LogError("ChaseCourse needs a ChaseDirector and a blockTemplate."); enabled = false; return; }

            float startX = PlayerController.Instance ? PlayerController.Instance.transform.position.x : chase.ScrollX;
            groundStart = Mathf.Min(startX, chase.LeftEdgeX) - 10f;
            cursor = startX + runway;
            nextCageX = cursor + chase.Speed * firstCageAfterSeconds;
            Update();
        }

        void Update()
        {
            while (cursor < chase.RightEdgeX + buildAhead) PlanNext();
            // Lay the ground up to the plan as soon as the open stretch nears the screen.
            if (groundStart < chase.RightEdgeX + 5f) LayGround(cursor);
            Despawn();
        }

        void PlanNext()
        {
            float speed = chase.Speed;
            var gap = PerPhase(gapSecondsPerPhase);
            cursor += speed * Random.Range(gap.x, gap.y);

            if (cursor >= nextCageX)
            {
                PlaceCage(speed);
                return;
            }

            float x = cursor;
            switch (Pick())
            {
                case Obstacle.Hurdle:
                    Block("Hurdle", x, 0f, hurdleSize, groundColor);
                    cursor += hurdleSize.x;
                    break;
                case Obstacle.TallHurdle:
                    Block("TallHurdle", x, 0f, tallHurdleSize, groundColor);
                    cursor += tallHurdleSize.x;
                    break;
                case Obstacle.Pit:
                    float width = pitWidth + pitWidthPerPhase * Phase;
                    LayGround(x);
                    groundStart = x + width;
                    cursor += width;
                    break;
                case Obstacle.HotBeam:
                    // The beam starts well before the hurdle, so a full jump taken early still meets it.
                    const float beamLength = 5f;
                    Block("HotBeamHurdle", x + 3f, 0f, hotBeamHurdleSize, groundColor);
                    var beam = Block("HotBeam", x, hotBeamHeight, new Vector2(beamLength, 0.6f), hazardColor, solid: false, keepColor: true);
                    beam.AddComponent<BoxCollider2D>();
                    beam.AddComponent<Hazard>();
                    cursor += beamLength;
                    break;
                case Obstacle.CrateWall:
                    Block("CrateWall (break it)", x, 0f, crateSize, crateColor, keepColor: true).AddComponent<Breakable>();
                    cursor += crateSize.x;
                    break;
                case Obstacle.SpringWall:
                    const float springToWall = 4f;
                    var spring = Block("Spring", x, 0f, new Vector2(1.2f, 0.3f), springColor, solid: false, keepColor: true);
                    spring.AddComponent<BoxCollider2D>();
                    spring.AddComponent<SpringPad>();
                    // Missed the spring? It's breakable, just slowly: a mistake costs ground, not the run.
                    Block("SpringWall", x + springToWall, 0f, new Vector2(1f, springWallHeight), groundColor)
                        .AddComponent<Breakable>().Hits = springWallHits;
                    cursor += springToWall + 1f;
                    break;
            }
        }

        /// <summary>
        /// A clear stretch: latch first, cage one boss-gap further on, so when the boss passes under the cage
        /// the player is about under the latch. Shooting it then is too late: the shot has to lead.
        /// </summary>
        void PlaceCage(float speed)
        {
            float gap = boss ? boss.Lead + (chase.ScrollX - chase.HomeX) : 8f;
            float latchX = cursor + 2f;
            float cageX = latchX + gap;
            var cage = CageTrap.Build(this, cageX, latchX, chase.GroundY, cageHangHeight, latchHeight, cageColor);
            cages.Add(cage);
            pieces.Add(new Piece { go = cage.gameObject, endX = cageX + 3f });
            cursor = cageX + 3f + speed; // an extra second of calm after it
            nextCageX = cageX + speed * cageEverySeconds;
        }

        /// <summary>Ground from groundStart to endX: stretches the current piece when it's contiguous, else starts one.</summary>
        void LayGround(float endX)
        {
            if (endX - groundStart < 0.01f) return;
            bool contiguous = ground != null && ground.go && Mathf.Abs(ground.endX - groundStart) < 0.01f;
            if (!contiguous || endX - ground.startX > maxGroundLength)
            {
                ground = new Piece { go = Make("Ground", Vector2.zero, Vector2.one, groundColor, true), startX = groundStart };
                pieces.Add(ground);
            }
            ground.endX = endX;
            ground.go.transform.position = new Vector3((ground.startX + endX) * 0.5f, chase.GroundY - 0.5f, 0f);
            ground.go.transform.localScale = new Vector3(endX - ground.startX, 1f, 1f);
            groundStart = endX;
        }

        /// <summary>A block standing `height` above the ground with its left edge at x.</summary>
        GameObject Block(string name, float x, float height, Vector2 size, Color color, bool solid = true, bool keepColor = false)
        {
            var center = new Vector2(x + size.x * 0.5f, chase.GroundY + height + size.y * 0.5f);
            var go = Make(name, center, size, color, solid, null, keepColor);
            pieces.Add(new Piece { go = go, endX = x + size.x });
            return go;
        }

        /// <summary>
        /// Copies the template: `size` world units at local `position` under `parent` (the track by default).
        /// keepColor = skip the gray-until-restored look (hazards, targets).
        /// </summary>
        public GameObject Make(string name, Vector2 position, Vector2 size, Color color, bool solid, Transform parent = null, bool keepColor = false)
        {
            var go = Instantiate(blockTemplate, parent ? parent : track); // the template is inactive, so is the copy
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<SpriteRenderer>().color = color;
            if (keepColor && go.TryGetComponent<Recolorable>(out var recolor)) DestroyImmediate(recolor); // before it wakes up
            if (solid) go.AddComponent<BoxCollider2D>().sharedMaterial = solidMaterial;
            go.SetActive(true);
            return go;
        }

        void Despawn()
        {
            float behind = chase.LeftEdgeX - 5f;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var go = pieces[i].go;
                if (go && pieces[i].endX >= behind) continue;
                if (go && go.TryGetComponent<CageTrap>(out var cage) && cage.IsHoldingBoss) continue;
                if (go) Destroy(go);
                pieces.RemoveAt(i);
            }
            cages.RemoveAll(c => !c);
        }

        Obstacle Pick()
        {
            float total = 0f;
            foreach (var o in obstacles) if (o.minPhase <= Phase) total += o.weight;
            float roll = Random.value * total;
            foreach (var o in obstacles)
            {
                if (o.minPhase > Phase) continue;
                roll -= o.weight;
                if (roll <= 0f) return o.type;
            }
            return Obstacle.Hurdle;
        }

        T PerPhase<T>(T[] values) => values[Mathf.Clamp(Phase, 0, values.Length - 1)];
    }
}
