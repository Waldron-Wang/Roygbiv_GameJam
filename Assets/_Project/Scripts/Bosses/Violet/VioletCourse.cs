using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Builds Violet's level at runtime from data (like ChaseCourse builds Orange's track), so it's tuned in the
    /// Inspector instead of hand-placed: the long APPROACH, one segment per ability, then the ARENA.
    ///
    /// The `pieces` list is laid out left to right from x = 0 on a floor at y = 0. A piece with `checkpoint` on starts
    /// a segment: a checkpoint (and a banner) at its start. Each piece that has an attack registers a Zone that
    /// VioletApproach runs while the player is inside it. The default list:
    ///   1 CRESCENT WAVES  -> Dash          a corridor with a ceiling; waves fill it floor to ceiling
    ///   2 RISING STEPS    -> Double Jump   cliff steps taller than a single jump; slams send ripples along them
    ///   3 LOW SWEEP       -> Down Dash     a long low beam over a thin gap; a tunnel at surf height; a beam that
    ///                                      catches you on a high ledge (dive down)
    ///   4 ROYAL RAIN      -> Light Shot    a sealed hall under a sword barrage; shoot the chain, hide under the slab
    ///   5 CRYSTAL GATE    -> Blaze Strike  a crystal wall only a charged Blaze Strike breaks
    ///   6 THE CURTAIN     -> Serenity      a needle curtain with a moving gap; a safe spot before it
    ///   7 THE GAUNTLET    -> mixed         waves, a wall then a beam as you land, rain + shelter, another curtain
    ///   THE HILL          the course ends at the foot of the king's hill: reaching it plays the arrival cinematic.
    ///   ARENA             a separate area further on (the cinematic cuts to it): one screen, walls both sides,
    ///                     two floating platforms.
    /// Blocks are copies of `blockTemplate` (an inactive sprite with Recolorable(Violet), gray until Violet is
    /// restored), drawn with the violet tile art when `tileSprite` is set. Hazards keep their own colors.
    /// </summary>
    public class VioletCourse : MonoBehaviour
    {
        public enum PieceKind { Floor, WaveCorridor, Steps, LowBeamLane, LowTunnel, LedgeBeam, RainHall, CrystalGate, Curtain, Wall, WallBeam }

        public enum Encounter { Waves, Slams, LowBeam, Rain, GatePressure, Curtain }

        [Serializable]
        public class Piece
        {
            public PieceKind kind;
            [Tooltip("Length along the course. Steps, LedgeBeam and Curtain have their own fixed shapes; Wall ignores it.")]
            public float length = 10f;
            [Tooltip("Starts a segment: a checkpoint at its start.")]
            public bool checkpoint;
            [Tooltip("Segment name, for the Inspector and the log.")]
            public string label;
            [Tooltip("RainHall: seconds of rain (0 = the default). WaveCorridor: seconds between waves (0 = default).")]
            public float amount;

            public Piece(PieceKind kind, float length, bool checkpoint = false, string label = "", float amount = 0f)
            {
                this.kind = kind;
                this.length = length;
                this.checkpoint = checkpoint;
                this.label = label;
                this.amount = amount;
            }
        }

        /// <summary>A stretch of the course with an attack in it. VioletApproach runs it while the player is inside.</summary>
        public class Zone
        {
            public Encounter type;
            public string label;
            public float x0, x1;
            /// <summary>The player must also be at least this high (a ledge).</summary>
            public float minY = float.NegativeInfinity;
            // Waves / ripples
            public float bottom, top, spawnX, endX, interval, first;
            public readonly List<Vector3> landings = new(); // (fromX, toX, floorY)
            // Beams
            public Rect beamArea;
            public float telegraph, fire;
            // Rain: it falls over [fromX, toX]
            public float fromX, toX;
            public VioletGate[] seals;
            public VioletSlab slab;
            public float duration;
            public bool done;
            // Gate / curtain
            public VioletCrystal crystal;
            public VioletCurtain curtain;

            public bool Contains(Vector2 p) => p.x >= x0 && p.x <= x1 && p.y >= minY;
        }

        [Header("Building blocks")]
        [Tooltip("Inactive square sprite with a Recolorable (Violet). Copies of it make up the course.")]
        [SerializeField] GameObject blockTemplate;
        [SerializeField] PhysicsMaterial2D solidMaterial;
        [Tooltip("Optional tile art (Art/tiles/violetTile): drawn tiled on every block. Empty = flat blocks.")]
        [SerializeField] Sprite tileSprite;
        [Tooltip("World size of one tile.")]
        [SerializeField] float tileSize = 1f;
        [Tooltip("How far below the floor the ground blocks reach.")]
        [SerializeField] float groundDepth = 6f;

        [Header("Layout")]
        [SerializeField] Piece[] pieces = DefaultPieces();

        [Header("Shapes")]
        [Tooltip("Corridor ceiling height (Crescent Waves fill everything under it).")]
        [SerializeField] float corridorCeiling = 3.2f;
        [Tooltip("Rising Steps: the step heights (each taller than a single jump, ~2.9).")]
        [SerializeField] float[] stepHeights = { 3.6f, 3.6f, 4.2f };
        [Tooltip("Rising Steps: length of each landing.")]
        [SerializeField] float stepLanding = 7f;
        [Tooltip("Rising Steps: length of the top.")]
        [SerializeField] float stepTop = 6f;
        [Tooltip("Low beams leave this gap over the floor (a Down Dash surf body is ~0.55 tall).")]
        [SerializeField] float lowBeamGap = 0.62f;
        [Tooltip("Low beams reach up to this height.")]
        [SerializeField] float lowBeamTop = 10f;
        [Tooltip("Low tunnel: height of the opening (a standing player is 1.2 tall, a surfing one ~0.55).")]
        [SerializeField] float tunnelHeight = 0.75f;
        [Tooltip("Ledge beam: the high ledge's height (reached by a step at ~half of it).")]
        [SerializeField] float ledgeHeight = 4.6f;
        [Tooltip("Ledge beam: ledge length and the lower lane's length after it.")]
        [SerializeField] Vector2 ledgeAndLane = new(10f, 16f);
        [Tooltip("Royal Rain: pillar height under the dropped slab (the roof you hide under).")]
        [SerializeField] float shelterHeight = 1.8f;
        [Tooltip("Royal Rain: space between the pillars.")]
        [SerializeField] float shelterWidth = 4.2f;
        [Tooltip("Royal Rain: bottom of the hanging slab, and the anchor's height (out of reach: a double jump + swing tops out ~6.5).")]
        [SerializeField] Vector2 slabHangAndAnchor = new(6.4f, 8.5f);
        [Tooltip("Royal Rain: yellow anchor's horizontal distance to the right of the slab center. Keeps an upper-right shot from inside clear of the slab.")]
        [SerializeField, Min(0f)] float shelterAnchorOffset = 6f;
        [Tooltip("Seal height (taller than any jump).")]
        [SerializeField] float sealHeight = 14f;
        [Tooltip("Crystal gate height (taller than any jump; lobbed orbs come over it).")]
        [SerializeField] float crystalHeight = 9f;
        [Tooltip("Curtain: safe spot length, curtain length, exit length.")]
        [SerializeField] Vector3 curtainLayout = new(10f, 7f, 6f);
        [Tooltip("Curtain: ceiling height (needles fall from it).")]
        [SerializeField] float curtainCeiling = 4.4f;
        [SerializeField] VioletCurtain.Settings curtain = new();

        [Header("The end of the course")]
        [Tooltip("Floor between the last piece and the foot of the hill; the arrival triggers halfway along it.")]
        [SerializeField] float hillApproach = 8f;
        [Tooltip("The foot of his hill: a low rise (width, height) with an invisible stop behind it. Low, so it never hides the distant king.")]
        [SerializeField] Vector2 hill = new(8f, 1.4f);
        [SerializeField] Color hillColor = new(0.2f, 0.12f, 0.3f);

        [Header("Arena")]
        [Tooltip("How far past the hill the arena is built (out of sight of the course).")]
        [SerializeField] float arenaGap = 60f;
        [Tooltip("Inner width (one screen at 16:9 is ~24.9).")]
        [SerializeField] float arenaWidth = 24f;
        [Tooltip("Height of the arena walls.")]
        [SerializeField] float arenaWallHeight = 16f;
        [Tooltip("Floating platforms: distance in from each wall, height and length.")]
        [SerializeField] Vector3 platforms = new(5.5f, 3.2f, 4f);
        [Tooltip("Where the king stands in the arena: this far in from the right wall.")]
        [SerializeField] float arenaKingInset = 5f;
        [Tooltip("Camera height above the arena floor while the duel is locked.")]
        [SerializeField] float arenaCameraHeight = 5f;

        [Header("Colors")]
        [SerializeField] Color blockColor = Color.white;
        [SerializeField] Color hazardColor = new(0.76f, 0.4f, 1f);
        [SerializeField] Color crystalColor = new(0.8f, 0.5f, 1f, 0.92f);
        [SerializeField] Color slabColor = new(0.55f, 0.48f, 0.62f);
        [SerializeField] Color bannerColor = new(0.45f, 0.3f, 0.65f);

        readonly List<SpriteRenderer> bannerFlags = new();
        Transform root;
        float groundStart = float.NaN, groundEnd, groundTop;

        public readonly List<Zone> Zones = new();
        /// <summary>Feet position of each approach checkpoint.</summary>
        public readonly List<Vector2> Checkpoints = new();
        public bool Built { get; private set; }
        /// <summary>Where the run starts (the first checkpoint).</summary>
        public float StartX => Checkpoints.Count > 0 ? Checkpoints[0].x : 0f;
        /// <summary>Reaching this x (the foot of the hill) plays the arrival cinematic.</summary>
        public float EndX { get; private set; }
        public float ArenaMinX { get; private set; }
        public float ArenaMaxX { get; private set; }
        public float ArenaFloor => 0f;
        /// <summary>Feet position of the player when the duel starts.</summary>
        public Vector2 ArenaSpawn { get; private set; }
        /// <summary>Where the king stands when the duel starts (x).</summary>
        public float ArenaKingX { get; private set; }
        public Vector2 ArenaCamera { get; private set; }

        /// <summary>Sets the building blocks from code (VioletSetup builds the level at runtime when the menu wasn't run).</summary>
        public void Configure(GameObject template, PhysicsMaterial2D material, Sprite tile)
        {
            blockTemplate = template;
            solidMaterial = material;
            tileSprite = tile;
        }

        public void Build()
        {
            if (Built) return;
            Built = true;
            if (!blockTemplate) { Debug.LogError("VioletCourse needs a blockTemplate. Run ROYGBIV > Build Violet Level."); return; }
            root = new GameObject("Course").transform;
            root.SetParent(transform, false);

            // A back wall so the start can't be walked off.
            Block("BackWall", Rect.MinMaxRect(-3f, 0f, -1.5f, arenaWallHeight));
            float x = -3f;
            LayGround(x, 0f, 0f);

            foreach (var p in pieces)
            {
                if (p.checkpoint) AddCheckpoint(x);
                x = BuildPiece(p, x);
            }
            x = BuildHill(x);
            FlushGround();
            BuildArena(x + arenaGap);
            FlushGround();
        }

        float BuildPiece(Piece p, float x)
        {
            float len = Mathf.Max(1f, p.length);
            switch (p.kind)
            {
                case PieceKind.Floor:
                    LayGround(x, x + len, 0f);
                    return x + len;

                case PieceKind.WaveCorridor:
                {
                    LayGround(x, x + len, 0f);
                    Block("CorridorCeiling", Rect.MinMaxRect(x, corridorCeiling, x + len, corridorCeiling + 10f));
                    var z = AddZone(Encounter.Waves, p, x - 3f, x + len);
                    z.bottom = 0f;
                    z.top = corridorCeiling - 0.02f;
                    z.spawnX = x + len;
                    z.endX = x - 0.5f;
                    z.interval = p.amount > 0f ? p.amount : 1.6f;
                    z.first = 0.8f;
                    return x + len;
                }

                case PieceKind.Steps:
                {
                    var z = AddZone(Encounter.Slams, p, x - 1f, x);
                    float top = 0f, cx = x;
                    LayGround(cx, cx + stepLanding, 0f);
                    z.landings.Add(new Vector3(cx, cx + stepLanding, 0f));
                    cx += stepLanding;
                    for (int i = 0; i < stepHeights.Length; i++)
                    {
                        FlushGround();
                        top += stepHeights[i];
                        bool last = i == stepHeights.Length - 1;
                        float w = last ? stepTop : stepLanding;
                        Block("Step", Rect.MinMaxRect(cx, -groundDepth, cx + w, top));
                        if (!last) z.landings.Add(new Vector3(cx, cx + w, top));
                        cx += w;
                    }
                    z.x1 = cx;
                    z.interval = 2.4f;
                    z.first = 1f;
                    return cx;
                }

                case PieceKind.LowBeamLane:
                {
                    LayGround(x, x + len, 0f);
                    var z = AddZone(Encounter.LowBeam, p, x, x + len);
                    z.beamArea = Rect.MinMaxRect(x - 1f, lowBeamGap, x + len + 1f, lowBeamTop);
                    z.telegraph = 0.9f;
                    z.fire = 1.2f;
                    // Between two beams (interval + telegraph = 1.4 s) the lane can't be crossed on foot (2.4 s): you have to go under one.
                    z.interval = 0.5f;
                    z.first = 0.6f;
                    SafeLine(x - 1f, x + len + 1f);
                    return x + len;
                }

                case PieceKind.LowTunnel:
                    LayGround(x, x + len + 2f, 0f);
                    Block("Tunnel", Rect.MinMaxRect(x + 1f, tunnelHeight, x + 1f + len, arenaWallHeight));
                    SafeLine(x + 1f, x + 1f + len);
                    return x + len + 2f;

                case PieceKind.LedgeBeam:
                {
                    FlushGround();
                    float stepW = 3f, ledge = ledgeAndLane.x, lane = ledgeAndLane.y;
                    Block("LedgeStep", Rect.MinMaxRect(x, -groundDepth, x + stepW, ledgeHeight * 0.52f));
                    Block("Ledge", Rect.MinMaxRect(x + stepW, -groundDepth, x + stepW + ledge, ledgeHeight));
                    float laneStart = x + stepW + ledge;
                    LayGround(laneStart, laneStart + lane, 0f);
                    // Catches the player on the ledge: the first beam warns the moment they get up there.
                    var z = AddZone(Encounter.LowBeam, p, x + stepW, laneStart + lane);
                    z.beamArea = Rect.MinMaxRect(x - 0.5f, lowBeamGap, laneStart + lane + 1f, lowBeamTop + ledgeHeight * 0.5f);
                    z.telegraph = 0.85f;
                    z.fire = 1.2f;
                    z.interval = 1.8f;
                    z.first = 0f;
                    SafeLine(laneStart, laneStart + lane);
                    return laneStart + lane;
                }

                case PieceKind.RainHall:
                {
                    LayGround(x, x + len, 0f);
                    var z = AddZone(Encounter.Rain, p, x + 4f, x + len - 2f);
                    z.seals = new[] { Seal(x + 0.5f), Seal(x + len - 1.5f) };
                    z.slab = Shelter(x + len * 0.5f);
                    z.duration = p.amount > 0f ? p.amount : 7f;
                    z.fromX = x + 1.6f;
                    z.toX = x + len - 1.6f;
                    z.telegraph = 3f;
                    return x + len;
                }

                case PieceKind.CrystalGate:
                {
                    LayGround(x, x + len, 0f);
                    float cx = x + len - 4f;
                    var crystal = Crystal(cx);
                    var z = AddZone(Encounter.GatePressure, p, x, cx);
                    z.crystal = crystal;
                    z.interval = 1.9f;
                    z.first = 1.2f;
                    return x + len;
                }

                case PieceKind.Curtain:
                {
                    float safe = curtainLayout.x, width = curtainLayout.y, exit = curtainLayout.z;
                    float c0 = x + safe, c1 = c0 + width;
                    LayGround(x, c1 + exit, 0f);
                    Block("CurtainCeiling", Rect.MinMaxRect(c0 - 0.1f, curtainCeiling, c1 + 0.1f, curtainCeiling + 10f));
                    var go = new GameObject("Curtain (Serenity)");
                    go.transform.SetParent(root, false);
                    var c = go.AddComponent<VioletCurtain>();
                    c.Setup(c0, c1, 0f, curtainCeiling, curtain, VioletNeedle.RimColor, p.label);
                    var z = AddZone(Encounter.Curtain, p, x, c1 + 2f);
                    z.curtain = c;
                    return c1 + exit;
                }

                case PieceKind.Wall:
                    LayGround(x, x + 1f, 0f);
                    Block("Wall", Rect.MinMaxRect(x, 0f, x + 1f, stepHeights.Length > 0 ? stepHeights[stepHeights.Length - 1] : 4.2f));
                    return x + 1f;

                case PieceKind.WallBeam:
                {
                    float wallTop = stepHeights.Length > 0 ? stepHeights[stepHeights.Length - 1] : 4.2f;
                    LayGround(x, x + 1f + len, 0f);
                    Block("Wall", Rect.MinMaxRect(x, 0f, x + 1f, wallTop));
                    // Fires as the player comes over the wall: they land into it.
                    var z = AddZone(Encounter.LowBeam, p, x + 0.3f, x + 1f + len);
                    z.beamArea = Rect.MinMaxRect(x + 1.05f, lowBeamGap, x + 1f + len + 1f, lowBeamTop);
                    z.telegraph = 0.75f;
                    z.fire = 1.2f;
                    z.interval = 2f;
                    z.first = 0f;
                    SafeLine(x + 1f, x + 1f + len);
                    return x + 1f + len;
                }
            }
            return x + len;
        }

        /// <summary>The foot of the king's hill: a last stretch of floor, then a mound that blocks the way. Returns its far end.</summary>
        float BuildHill(float x)
        {
            LayGround(x, x + hillApproach + hill.x, 0f);
            EndX = x + hillApproach * 0.5f;
            float h0 = x + hillApproach;
            // The solid part is invisible: the hill sprite in front of it is what you see.
            var stop = Block("Hill (stop)", Rect.MinMaxRect(h0 + 1f, 0f, h0 + hill.x, arenaWallHeight));
            stop.GetComponent<SpriteRenderer>().enabled = false;
            var shape = VioletShapes.Polygon($"Hill_{hill.x:0}_{hill.y:0}", Vector2.zero, 0.35f,
                new(0f, 0f), new(hill.x * 0.08f, hill.y * 0.3f), new(hill.x * 0.2f, hill.y * 0.65f), new(hill.x * 0.36f, hill.y * 0.9f),
                new(hill.x * 0.55f, hill.y), new(hill.x * 0.8f, hill.y * 0.96f), new(hill.x, hill.y * 0.85f), new(hill.x, 0f));
            VioletShapes.Create("Hill", shape, root, new Vector2(h0 - root.position.x, -root.position.y), hillColor, 1);
            return h0 + hill.x;
        }

        /// <summary>The duel's arena, a sealed area of its own: floor, a wall on each side, two floating platforms.</summary>
        void BuildArena(float x)
        {
            float inner0 = x + 1.5f, inner1 = inner0 + arenaWidth;
            LayGround(x, inner1 + 1.5f, 0f);
            ArenaMinX = inner0;
            ArenaMaxX = inner1;
            ArenaSpawn = new Vector2(inner0 + 3f, 0f);
            ArenaKingX = inner1 - arenaKingInset;
            ArenaCamera = new Vector2((inner0 + inner1) * 0.5f, arenaCameraHeight);

            Block("ArenaWall", Rect.MinMaxRect(x, 0f, inner0, arenaWallHeight));
            Block("ArenaWall", Rect.MinMaxRect(inner1, 0f, inner1 + 1.5f, arenaWallHeight));
            Block("Platform", Rect.MinMaxRect(inner0 + platforms.x - platforms.z * 0.5f, platforms.y - 0.5f, inner0 + platforms.x + platforms.z * 0.5f, platforms.y));
            Block("Platform", Rect.MinMaxRect(inner1 - platforms.x - platforms.z * 0.5f, platforms.y - 0.5f, inner1 - platforms.x + platforms.z * 0.5f, platforms.y));
        }

        // ---------- Pieces ----------

        Zone AddZone(Encounter type, Piece p, float x0, float x1)
        {
            var z = new Zone { type = type, label = p.label, x0 = x0, x1 = x1 };
            Zones.Add(z);
            return z;
        }

        void AddCheckpoint(float x)
        {
            Checkpoints.Add(new Vector2(x + 2f, 0f));
            FlatSprite.Create("BannerPole", root, new Vector2(x + 0.8f, 1.2f), new Vector2(0.1f, 2.4f), bannerColor * 0.7f, 2);
            var flag = FlatSprite.Create("BannerFlag", root, new Vector2(x + 1.25f, 2.15f), new Vector2(0.8f, 0.45f), bannerColor, 2);
            bannerFlags.Add(flag);
        }

        /// <summary>A banner lights up when its checkpoint is reached.</summary>
        public void LightCheckpoint(int index, bool burst)
        {
            if (index < 0 || index >= bannerFlags.Count || !bannerFlags[index]) return;
            var flag = bannerFlags[index];
            flag.color = VioletShapes.Glow;
            if (burst) VioletHits.Burst(flag.transform.position, VioletShapes.Glow, 10, 4f, 0.3f);
        }

        /// <summary>A thin glowing line just over the floor: "the beam stops here: fit under it".</summary>
        void SafeLine(float x0, float x1)
        {
            FlatSprite.Create("SafeLine", root, new Vector2((x0 + x1) * 0.5f, lowBeamGap), new Vector2(x1 - x0, 0.03f), new Color(hazardColor.r, hazardColor.g, hazardColor.b, 0.35f), 4);
        }

        VioletGate Seal(float x)
        {
            var seal = Block("Seal", Rect.MinMaxRect(x, 0f, x + 1f, sealHeight));
            Lower(seal);
            var gate = seal.AddComponent<VioletGate>();
            gate.Setup(-sealHeight * 0.5f - 0.2f, sealHeight * 0.5f, false);
            return gate;
        }

        VioletSlab Shelter(float cx)
        {
            float half = shelterWidth * 0.5f;
            Block("ShelterPillar", Rect.MinMaxRect(cx - half - 0.6f, 0f, cx - half, shelterHeight));
            Block("ShelterPillar", Rect.MinMaxRect(cx + half, 0f, cx + half + 0.6f, shelterHeight));

            float slabW = shelterWidth + 1.6f, slabH = 0.7f, hang = slabHangAndAnchor.x, anchorY = slabHangAndAnchor.y;
            float anchorX = cx + shelterAnchorOffset;
            var slab = Block("Slab", Rect.MinMaxRect(cx - slabW * 0.5f, hang, cx + slabW * 0.5f, hang + slabH), slabColor, true);

            var links = new List<Transform>();
            Vector2 chainStart = new(cx, hang + slabH), chainEnd = new(anchorX, anchorY - 0.3f);
            Vector2 connection = chainEnd - chainStart;
            int linkCount = Mathf.Max(1, Mathf.CeilToInt(connection.magnitude / 0.26f));
            float linkAngle = Mathf.Atan2(connection.y, connection.x) * Mathf.Rad2Deg - 90f;
            for (int i = 0; i < linkCount; i++)
            {
                var link = FlatSprite.Create("ChainLink", root, Vector2.Lerp(chainStart, chainEnd, (i + 0.5f) / linkCount),
                    new Vector2(0.14f, 0.26f), new Color(0.7f, 0.65f, 0.75f), 5).transform;
                link.rotation = Quaternion.Euler(0f, 0f, linkAngle);
                links.Add(link);
            }
            Block("AnchorBeam", Rect.MinMaxRect(anchorX - 1.6f, anchorY + 0.35f, anchorX + 1.6f, anchorY + 0.85f)).AddComponent<VioletRainPassThrough>();

            var anchor = Block("ChainAnchor (shoot it)", Rect.MinMaxRect(anchorX - 0.45f, anchorY - 0.3f, anchorX + 0.45f, anchorY + 0.35f), new Color(1f, 0.88f, 0.55f), true);
            anchor.AddComponent<VioletRainPassThrough>();
            var glow = IndigoShapes.Create("AnchorGlow", IndigoShapes.Ring, null, new Vector2(anchorX, anchorY), 1.4f, new Color(1f, 0.9f, 0.6f, 0.8f), 8);
            glow.transform.SetParent(root, true);
            glow.gameObject.AddComponent<VioletPulse>();

            var s = slab.AddComponent<VioletSlab>();
            s.Setup(slab, shelterHeight + slabH * 0.5f, links.ToArray(), glow);
            slab.AddComponent<VioletDestructibleSlab>().Setup(s);
            anchor.AddComponent<VioletShelterWeakPoint>().Setup(s);
            return s;
        }

        VioletCrystal Crystal(float x)
        {
            var go = new GameObject("CrystalGate (Blaze Strike)");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, 0f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.4f, crystalHeight);
            box.offset = new Vector2(0f, crystalHeight * 0.5f);
            box.sharedMaterial = solidMaterial;

            var facets = new GameObject("Facets").transform;
            facets.SetParent(go.transform, false);
            float[] heights = { 1f, 0.82f, 0.95f, 0.7f };
            float[] offsets = { 0f, -0.35f, 0.3f, 0.05f };
            float[] widths = { 1.3f, 1f, 0.9f, 0.7f };
            for (int i = 0; i < heights.Length; i++)
            {
                var c = Color.Lerp(crystalColor, Color.white, i * 0.12f);
                var f = VioletShapes.Create("Facet", VioletShapes.Crystal, facets, new Vector2(offsets[i], 0f), c, 6 + i);
                f.transform.localScale = new Vector3(widths[i], crystalHeight * heights[i], 1f);
            }
            return go.AddComponent<VioletCrystal>();
        }

        // ---------- Blocks ----------

        /// <summary>Ground from x0 to x1 with its top at `top`: extends the current run when it's contiguous, so there are few seams.</summary>
        void LayGround(float x0, float x1, float top)
        {
            if (!float.IsNaN(groundStart) && Mathf.Abs(groundEnd - x0) < 0.02f && Mathf.Abs(groundTop - top) < 0.001f)
            {
                groundEnd = Mathf.Max(groundEnd, x1);
                return;
            }
            FlushGround();
            groundStart = x0;
            groundEnd = x1;
            groundTop = top;
        }

        void FlushGround()
        {
            if (float.IsNaN(groundStart)) return;
            if (groundEnd - groundStart > 0.01f) Block("Ground", Rect.MinMaxRect(groundStart, groundTop - groundDepth, groundEnd, groundTop));
            groundStart = float.NaN;
        }

        /// <summary>A solid block covering `r`, copied from the template (tiled art if set).</summary>
        GameObject Block(string name, Rect r, Color? color = null, bool keepColor = false)
        {
            var go = Instantiate(blockTemplate, root); // the template is inactive, so is the copy
            go.name = name;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = color ?? blockColor;
            if (keepColor && go.TryGetComponent<Recolorable>(out var recolor)) DestroyImmediate(recolor); // before it wakes up
            go.transform.position = new Vector3(r.center.x, r.center.y, 0f);

            Vector2 local;
            if (tileSprite)
            {
                // Tiled: scale the tile down to tileSize world units, and size the renderer (and collider) in its units.
                float scale = tileSize / Mathf.Max(0.01f, tileSprite.bounds.size.x);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                local = r.size / scale;
                sr.sprite = tileSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = local;
            }
            else
            {
                go.transform.localScale = new Vector3(r.width, r.height, 1f);
                local = Vector2.one;
            }
            var box = go.AddComponent<BoxCollider2D>();
            box.size = local;
            box.sharedMaterial = solidMaterial;
            go.SetActive(true);
            return go;
        }

        static void Lower(GameObject block)
        {
            // Sunk gates draw behind the ground they hide in.
            if (block.TryGetComponent<SpriteRenderer>(out var sr)) sr.sortingOrder -= 1;
        }

        static Piece[] DefaultPieces() => new[]
        {
            new Piece(PieceKind.Floor, 12f, true, "1 Crescent Waves (Dash)"),
            new Piece(PieceKind.WaveCorridor, 32f),
            new Piece(PieceKind.Floor, 8f),

            new Piece(PieceKind.Steps, 0f, true, "2 Rising Steps (Double Jump)"),
            new Piece(PieceKind.Floor, 8f),

            new Piece(PieceKind.LowBeamLane, 24f, true, "3 Low Sweep (Down Dash)"),
            new Piece(PieceKind.Floor, 4f),
            new Piece(PieceKind.LowTunnel, 14f),
            new Piece(PieceKind.Floor, 4f),
            new Piece(PieceKind.LedgeBeam, 0f),
            new Piece(PieceKind.Floor, 6f),

            new Piece(PieceKind.RainHall, 36f, true, "4 Royal Rain (Light Shot)"),
            new Piece(PieceKind.Floor, 6f),

            new Piece(PieceKind.CrystalGate, 16f, true, "5 Crystal Gate (Blaze Strike)"),
            new Piece(PieceKind.Floor, 6f),

            new Piece(PieceKind.Curtain, 0f, true, "6 The Curtain (Serenity)"),
            new Piece(PieceKind.Floor, 6f),

            new Piece(PieceKind.Floor, 6f, true, "7 The Gauntlet (everything)"),
            new Piece(PieceKind.WaveCorridor, 16f, false, "", 1.4f),
            new Piece(PieceKind.Floor, 3f),
            new Piece(PieceKind.WallBeam, 14f),
            new Piece(PieceKind.Floor, 4f),
            new Piece(PieceKind.RainHall, 28f, false, "", 4.5f),
            new Piece(PieceKind.Floor, 3f),
            new Piece(PieceKind.Curtain, 0f),
            new Piece(PieceKind.Floor, 4f),
        };
    }
}
