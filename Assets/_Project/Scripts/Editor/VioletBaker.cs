using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Bake Violet Level Into Scene. A one-time migration of Violet's course (which used to be built at
    /// runtime) into Level_Violet as a normal, hand-editable level:
    ///   Grid > Tilemap (set up like Level_Yellow: TilemapCollider2D merged into a CompositeCollider2D, static
    ///   Rigidbody2D, Recolorable(Violet) with Desaturate), with every piece of static terrain PAINTED from the layout
    ///   below: floor, walls, corridor ceilings, steps, the tunnel roof, the ledge, the shelter pillars, the curtain
    ///   ceilings, the king's hill and the arena. Each cell gets its 9-slice tile from its neighbours, with inner-corner
    ///   tiles at concave corners.
    ///   A "Violet" object holding the director (VioletApproach) and every gameplay element as a real scene object:
    ///   checkpoints, one VioletZone per attack, the Royal Rain halls (seals, shelter), the Crystal Gate, the curtains,
    ///   the arrival at the foot of the hill, the king's spot on top of it (Far King) and the arena. Repeatable pieces
    ///   are prefabs in Prefabs/Violet/ (made here if missing, kept if they exist).
    /// It also removes the skeleton's Environment ground/walls/platforms, stretches the KillZone under everything and turns
    /// the boss's immediate start off. Then it builds the tilemap's collider (only once the tiles are processed: built any
    /// earlier it merges nothing, and an EMPTY collider gets saved), checks the level is solid where it must be, and saves
    /// the scene only if every check passes. Every clearance is checked against the real Player.prefab.
    /// Running it again ASKS before replacing an existing bake, so hand edits aren't lost by accident.
    /// Without re-baking: ROYGBIV > Fix Violet Colliders (rebuild + check + save) and ROYGBIV > Paint Violet King's Hill
    /// (paints the hill at the arrival into the existing tilemap, moves the Far King onto it, then the same).
    /// </summary>
    public static class VioletBaker
    {
        const string ScenePath = "Assets/_Project/Scenes/Level_Violet.unity";
        const string PrefabDir = "Assets/_Project/Prefabs/Violet";
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        const string SquarePath = "Assets/_Project/Art/Placeholder/Square.png";
        const string MaterialPath = "Assets/_Project/Art/Placeholder/NoFriction.physicsMaterial2D";
        const string RootName = "Violet", GridName = "Grid";

        // ---------- The layout, snapped to the 1-unit tile grid (floor top at y = 0) ----------
        const int GroundDepth = 6;
        const int CorridorCeiling = 3, CorridorCeilingTop = 13;           // waves fill 0..3, too thick to stand on
        static readonly int[] StepHeights = { 4, 4, 4 };                  // each over a single jump, under a double
        const int StepLanding = 7, StepTop = 6;
        const float LowBeamGap = 0.62f, LowBeamTop = 10f;                 // a surf (~0.54) fits under, standing (1.2) doesn't
        const int TunnelHeight = 1, TallTop = 16;                         // the same, in tiles
        const int LedgeStepWidth = 3, LedgeStepHeight = 2, LedgeLength = 10, LedgeHeight = 4, LedgeLane = 16;
        const int PillarHeight = 2, ShelterInner = 4;
        const float SlabHang = 6.4f, SlabThickness = 0.7f, AnchorY = 8.5f, AnchorOffset = 6f;
        const int SealHeight = 14;
        const float CrystalHeight = 9f;
        const int CurtainSafe = 10, CurtainWidth = 7, CurtainExit = 6, CurtainCeiling = 4, CurtainCeilingTop = 14;
        const int WallHeight = 4;
        // The end: a flat run-up (the arrival triggers at its end, one tile before the hill), then the king's hill. It
        // rises one tile per step to its top, which runs on past him so it fills the view behind him. He stands
        // KingOnTop into the top: in view about a second before the arrival, his resting blade well clear of its edge.
        const int HillApproach = 8, HillStepWidth = 2, HillTop = 3, HillTopLength = 16, KingOnTop = 2;
        const int HillWidth = (HillTop - 1) * HillStepWidth + HillTopLength;
        const int ArenaGap = 60, ArenaWall = 2, ArenaWidth = 24, ArenaHeight = 16, DoorHeight = 3;
        const int PlatformInset = 4, PlatformLength = 4, PlatformTop = 3;
        const int BossInset = 5, PlayerInset = 3;

        // ---------- Sorting orders (Default layer; the tilemap is 0, the king 6 and up, the player 10) ----------
        const int TilemapOrder = 0;
        const int SealOrder = -1; // behind the tilemap: the part sunk into the floor hides behind the floor tiles
        const int SlabOrder = 2;  // in front of the tilemap, never tied with the tiles it rests on

        enum P { Floor, WaveCorridor, Steps, LowBeamLane, LowTunnel, LedgeBeam, RainHall, CrystalGate, Curtain, Wall, WallBeam }

        // kind, length, checkpoint label (null = none), amount (rain seconds / wave interval; 0 = default)
        static readonly (P kind, int length, string checkpoint, float amount)[] Layout =
        {
            (P.Floor, 12, "1 Crescent Waves (Dash)", 0f),
            (P.WaveCorridor, 32, null, 0f),
            (P.Floor, 8, null, 0f),
            (P.Steps, 0, "2 Rising Steps (Double Jump)", 0f),
            (P.Floor, 8, null, 0f),
            (P.LowBeamLane, 24, "3 Low Sweep (Down Dash)", 0f),
            (P.Floor, 4, null, 0f),
            (P.LowTunnel, 14, null, 0f),
            (P.Floor, 4, null, 0f),
            (P.LedgeBeam, 0, null, 0f),
            (P.Floor, 6, null, 0f),
            (P.RainHall, 36, "4 Royal Rain (Light Shot)", 0f),
            (P.Floor, 6, null, 0f),
            (P.CrystalGate, 16, "5 Crystal Gate (Blaze Strike)", 0f),
            (P.Floor, 6, null, 0f),
            (P.Curtain, 0, "6 The Curtain (Serenity)", 0f),
            (P.Floor, 6, null, 0f),
            (P.Floor, 6, "7 The Gauntlet (everything)", 0f),
            (P.WaveCorridor, 16, null, 1.4f),
            (P.Floor, 3, null, 0f),
            (P.WallBeam, 14, null, 0f),
            (P.Floor, 4, null, 0f),
            (P.RainHall, 28, null, 4.5f),
            (P.Floor, 3, null, 0f),
            (P.Curtain, 0, null, 0f),
            (P.Floor, 4, null, 0f),
        };

        static readonly Color BannerColor = new(0.45f, 0.3f, 0.65f);
        static readonly Color SlabColor = new(0.72f, 0.66f, 0.8f);

        // Bake state.
        static HashSet<Vector2Int> solid;
        static Transform root, checkpoints, zones, hazards;
        static Sprite square;
        static PhysicsMaterial2D noFriction;
        static int segment;

        [MenuItem("ROYGBIV/Bake Violet Level Into Scene")]
        public static void Bake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!VioletTileBuilder.Ready) VioletTileBuilder.Make();
            if (!VioletTileBuilder.Ready) { Debug.LogError("[ROYGBIV] The Violet tiles couldn't be made: run ROYGBIV > Make Violet Tiles and check the console."); return; }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var oldRoot = roots.FirstOrDefault(r => r.name == RootName);
            var oldGrid = roots.FirstOrDefault(r => r.name == GridName);
            // A "Violet" object without zones is the old runtime-course director (Build Violet Level): replaced without asking.
            bool baked = oldGrid || (oldRoot && oldRoot.GetComponentInChildren<VioletZone>(true));
            if (baked && !EditorUtility.DisplayDialog("Level_Violet is already baked",
                    "Bake it again from the original layout?\n\nThis DELETES the current Violet tilemap and objects, including any hand edits to them.",
                    "Replace", "Cancel")) return;
            if (oldRoot) Object.DestroyImmediate(oldRoot);
            if (oldGrid) Object.DestroyImmediate(oldGrid);

            square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();
            var prefabs = MakePrefabs();

            solid = new HashSet<Vector2Int>();
            segment = 0;
            root = new GameObject(RootName).transform;
            root.gameObject.AddComponent<VioletApproach>();
            checkpoints = Group("Checkpoints");
            zones = Group("Zones");
            hazards = Group("Hazards");

            float end = LayOut(prefabs, out float firstCheckpointX);
            Paint();
            int removed = CleanSkeleton(end);
            SetUpLevel(firstCheckpointX);
            int painted = solid.Count;
            solid = null;

            AssetDatabase.SaveAssets();
            FinishTerrain(scene, $"Violet baked into Level_Violet: {painted} tiles painted, {removed} skeleton objects removed",
                "Level_Violet.unity, Prefabs/Violet/ and Art/tiles/ (VioletTiles, Palettes/Violet.prefab, the .png.meta slicing)");
            ValidateClearances();
        }

        [MenuItem("ROYGBIV/Fix Violet Colliders")]
        public static void FixColliders()
        {
            if (!OpenLevel(out var scene)) return;
            FinishTerrain(scene, "Violet's tilemap collider rebuilt from its tiles (nothing else in Level_Violet changed)", "Level_Violet.unity");
        }

        /// <summary>
        /// Paints the king's hill into the EXISTING Level_Violet (no re-bake: hand edits are kept), right after the
        /// arrival: the hill's cells and the edges of the tiles around them. Moves the arrival's Stop to the hill's first
        /// step and the Far King onto its top, then rebuilds, checks and saves like Fix Violet Colliders.
        /// </summary>
        [MenuItem("ROYGBIV/Paint Violet King's Hill")]
        public static void PaintKingsHill()
        {
            if (!OpenLevel(out var scene)) return;
            if (!VioletTileBuilder.Ready) { Debug.LogError("[ROYGBIV] The Violet tiles are missing: run ROYGBIV > Make Violet Tiles first."); return; }
            var map = VioletTerrain.Composites(scene).Select(c => c.GetComponent<Tilemap>()).FirstOrDefault(m => m);
            var arrival = FindInScene<VioletArrival>(scene);
            var far = FindInScene<VioletFarKing>(scene);
            if (!map || !arrival || !far)
            {
                Debug.LogError("[ROYGBIV] Level_Violet has no Grid/Tilemap, Arrival or Far King: bake it first (ROYGBIV > Bake Violet Level Into Scene).");
                return;
            }

            int hill0 = Mathf.RoundToInt(arrival.X) + 1;
            var hill = new HashSet<Vector2Int>(HillCells(hill0));
            var edge = VioletTileBuilder.EdgeTiles;
            var inner = VioletTileBuilder.InnerTiles;
            var violet = new HashSet<TileBase>(edge.Concat<TileBase>(inner));
            bool SolidAt(Vector2Int c) => hill.Contains(c) || map.HasTile(new Vector3Int(c.x, c.y, 0));
            var repaint = new HashSet<Vector2Int>(hill);
            foreach (var c in hill)
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (violet.Contains(map.GetTile(new Vector3Int(n.x, n.y, 0)))) repaint.Add(n);
                }
            var cells = repaint.ToArray();
            map.SetTiles(cells.Select(c => new Vector3Int(c.x, c.y, 0)).ToArray(), cells.Select(c => Pick(c, SolidAt, edge, inner)).ToArray());
            EditorUtility.SetDirty(map);

            var stop = arrival.GetComponentsInChildren<BoxCollider2D>(true).FirstOrDefault(b => !b.isTrigger);
            if (stop) PlaceStop(stop, hill0);
            far.transform.position = KingSpot(hill0);
            EditorUtility.SetDirty(far.transform);
            FinishTerrain(scene, $"The king's hill painted into Level_Violet from x {hill0} to {hill0 + HillWidth} ({hill.Count} cells), " +
                                 "the arrival's Stop on its first step and the Far King on its top", "Level_Violet.unity");
        }

        /// <summary>Level_Violet: the open one if it's loaded (its unsaved edits are kept), else opened (asking to save the current scene).</summary>
        static bool OpenLevel(out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return scene.IsValid();
        }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        // ---------- The layout ----------

        /// <summary>Walks the layout left to right: fills terrain cells and places every gameplay object. Returns the right end.</summary>
        static float LayOut(Prefabs prefabs, out float firstCheckpointX)
        {
            firstCheckpointX = float.NaN;
            int x = -3;
            Fill(x - 2, -GroundDepth, x, TallTop); // a back wall: the start can't be walked off

            foreach (var (kind, length, checkpoint, amount) in Layout)
            {
                if (checkpoint != null)
                {
                    segment++;
                    var cp = Place(prefabs.checkpoint, checkpoints, new Vector2(x + 2, 0f), $"Checkpoint {checkpoint}");
                    cp.GetComponent<VioletCheckpointMarker>().label = checkpoint;
                    Record(cp.GetComponent<VioletCheckpointMarker>());
                    if (float.IsNaN(firstCheckpointX)) firstCheckpointX = x + 2;
                }
                x = Piece(prefabs, kind, length, amount, x);
            }

            // The end: the run-up, then the king's hill (painted, solid). The arrival triggers one tile before the hill;
            // an invisible stop on its first step keeps anyone from climbing to him. He stands on its top.
            Fill(x, -GroundDepth, x + HillApproach, 0);
            int hill0 = x + HillApproach;
            foreach (var c in HillCells(hill0)) solid.Add(c);
            var arrival = new GameObject("Arrival (foot of the hill)").AddComponent<VioletArrival>();
            arrival.transform.SetParent(root, false);
            arrival.transform.position = new Vector3(hill0 - 1, 0f, 0f);
            var stop = new GameObject("Stop").AddComponent<BoxCollider2D>();
            stop.transform.SetParent(arrival.transform, false);
            PlaceStop(stop, hill0);

            var far = new GameObject("Far King (on his hill)").AddComponent<VioletFarKing>();
            far.transform.SetParent(root, false);
            far.transform.position = KingSpot(hill0);

            x = hill0 + HillWidth;
            return BuildArena(prefabs, x + ArenaGap);
        }

        /// <summary>The king's hill with its foot at x = hill0: steps one tile up each, then the top, all down to the ground's bottom.</summary>
        static IEnumerable<Vector2Int> HillCells(int hill0)
        {
            int x = hill0;
            for (int top = 1; top <= HillTop; top++)
            {
                int width = top < HillTop ? HillStepWidth : HillTopLength;
                for (int cx = x; cx < x + width; cx++)
                for (int y = -GroundDepth; y < top; y++)
                    yield return new Vector2Int(cx, y);
                x += width;
            }
        }

        /// <summary>His feet on the top of the hill whose foot is at hill0.</summary>
        static Vector2 KingSpot(int hill0) => new(hill0 + (HillTop - 1) * HillStepWidth + KingOnTop, HillTop);

        /// <summary>The arrival's invisible wall: the hill's first step, full height.</summary>
        static void PlaceStop(BoxCollider2D stop, int hill0)
        {
            if (!noFriction) noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            stop.transform.position = new Vector3(hill0 + 0.5f, TallTop * 0.5f, 0f);
            stop.offset = Vector2.zero;
            stop.size = new Vector2(1f, TallTop);
            stop.sharedMaterial = noFriction;
            EditorUtility.SetDirty(stop);
            EditorUtility.SetDirty(stop.transform);
        }

        static int Piece(Prefabs prefabs, P kind, int len, float amount, int x)
        {
            string label = $"{segment} {kind}";
            switch (kind)
            {
                case P.Floor:
                    Fill(x, -GroundDepth, x + len, 0);
                    return x + len;

                case P.WaveCorridor:
                {
                    Fill(x, -GroundDepth, x + len, 0);
                    Fill(x, CorridorCeiling, x + len, CorridorCeilingTop);
                    var z = Zone(prefabs, VioletZone.Kind.Waves, label, x - 3, x + len);
                    z.waveBand = new Vector2(0f, CorridorCeiling - 0.02f);
                    z.waveFromX = len + 3;
                    z.waveToX = 2.5f;
                    z.interval = amount > 0f ? amount : 1.6f;
                    z.first = 0.8f;
                    Record(z);
                    return x + len;
                }

                case P.Steps:
                {
                    int cx = x, top = 0;
                    var landings = new List<Vector3>();
                    Fill(cx, -GroundDepth, cx + StepLanding, 0);
                    landings.Add(new Vector3(cx, cx + StepLanding, 0f));
                    cx += StepLanding;
                    for (int i = 0; i < StepHeights.Length; i++)
                    {
                        top += StepHeights[i];
                        bool last = i == StepHeights.Length - 1;
                        int w = last ? StepTop : StepLanding;
                        Fill(cx, -GroundDepth, cx + w, top);
                        if (!last) landings.Add(new Vector3(cx, cx + w, top));
                        cx += w;
                    }
                    var z = Zone(prefabs, VioletZone.Kind.Slams, label, x - 1, cx);
                    z.landings = landings.Select(l => new Vector3(l.x - (x - 1), l.y - (x - 1), l.z)).ToArray();
                    z.interval = 2.4f;
                    z.first = 1f;
                    Record(z);
                    return cx;
                }

                case P.LowBeamLane:
                {
                    Fill(x, -GroundDepth, x + len, 0);
                    var z = Zone(prefabs, VioletZone.Kind.LowBeam, label, x, x + len);
                    z.beamArea = Rect.MinMaxRect(-1f, LowBeamGap, len + 1f, LowBeamTop);
                    z.telegraph = 0.9f;
                    z.fire = 1.2f;
                    z.interval = 0.5f; // between beams (1.4 s) the lane can't be crossed on foot: you must go under one
                    z.first = 0.6f;
                    Record(z);
                    return x + len;
                }

                case P.LowTunnel:
                    Fill(x, -GroundDepth, x + len + 2, 0);
                    Fill(x + 1, TunnelHeight, x + 1 + len, TallTop);
                    return x + len + 2;

                case P.LedgeBeam:
                {
                    int ledge0 = x + LedgeStepWidth, ledge1 = ledge0 + LedgeLength, lane1 = ledge1 + LedgeLane;
                    Fill(x, -GroundDepth, ledge0, LedgeStepHeight);
                    Fill(ledge0, -GroundDepth, ledge1, LedgeHeight);
                    Fill(ledge1, -GroundDepth, lane1, 0);
                    // Catches the player at the END of the ledge, where a dive (or a quick drop) gets them under it.
                    int zx = ledge1 - 2;
                    var z = Zone(prefabs, VioletZone.Kind.LowBeam, label, zx, lane1);
                    z.beamArea = Rect.MinMaxRect(x - 0.5f - zx, LowBeamGap, lane1 + 1f - zx, LowBeamTop + LedgeHeight * 0.5f);
                    z.telegraph = 0.75f;
                    z.fire = 1.2f;
                    z.interval = 1.8f;
                    z.first = 0f;
                    Record(z);
                    return lane1;
                }

                case P.RainHall:
                    Fill(x, -GroundDepth, x + len, 0);
                    RainHall(prefabs, x, len, amount > 0f ? amount : 7f, label);
                    return x + len;

                case P.CrystalGate:
                {
                    Fill(x, -GroundDepth, x + len, 0);
                    int cx = x + len - 4;
                    var crystal = Place(prefabs.crystal, hazards, new Vector2(cx, 0f), $"Crystal Gate {label}").GetComponent<VioletCrystal>();
                    var z = Zone(prefabs, VioletZone.Kind.GatePressure, label, x, cx);
                    z.crystal = crystal;
                    z.interval = 1.9f;
                    z.first = 1.2f;
                    Record(z);
                    return x + len;
                }

                case P.Curtain:
                {
                    int c0 = x + CurtainSafe, c1 = c0 + CurtainWidth;
                    Fill(x, -GroundDepth, c1 + CurtainExit, 0);
                    Fill(c0, CurtainCeiling, c1, CurtainCeilingTop);
                    var curtain = Place(prefabs.curtain, hazards, new Vector2(c0, 0f), $"Curtain {label}").GetComponent<VioletCurtain>();
                    Set(curtain, "curtainLabel", $"Curtain {label}");
                    var z = Zone(prefabs, VioletZone.Kind.Curtain, label, x, c1 + 2);
                    z.curtain = curtain;
                    Record(z);
                    return c1 + CurtainExit;
                }

                case P.Wall:
                    Fill(x, -GroundDepth, x + 1, WallHeight);
                    return x + 1;

                case P.WallBeam:
                {
                    Fill(x, -GroundDepth, x + 1 + len, 0);
                    Fill(x, 0, x + 1, WallHeight);
                    // Fires as the player comes over the wall: they land into it.
                    float zx = x + 0.3f;
                    var z = Zone(prefabs, VioletZone.Kind.LowBeam, label, zx, x + 1 + len);
                    z.beamArea = Rect.MinMaxRect(x + 1.05f - zx, LowBeamGap, x + 2 + len - zx, LowBeamTop);
                    z.telegraph = 0.75f;
                    z.fire = 1.2f;
                    z.interval = 2f;
                    z.first = 0f;
                    Record(z);
                    return x + 1 + len;
                }
            }
            return x + len;
        }

        static void RainHall(Prefabs prefabs, int x, int len, float duration, string label)
        {
            var hall = new GameObject($"Royal Rain {label}").AddComponent<VioletRainHall>();
            hall.transform.SetParent(hazards, false);
            hall.transform.position = new Vector3(x, 0f, 0f);

            var left = Place(prefabs.seal, hall.transform, new Vector2(x + 1.5f, SealHeight * 0.5f), "Seal (left)").GetComponent<VioletGate>();
            var right = Place(prefabs.seal, hall.transform, new Vector2(x + len - 1.5f, SealHeight * 0.5f), "Seal (right)").GetComponent<VioletGate>();

            int cx = x + len / 2, half = ShelterInner / 2;
            Fill(cx - half - 1, 0, cx - half, PillarHeight); // the pillars the slab drops onto
            Fill(cx + half, 0, cx + half + 1, PillarHeight);
            var shelter = Place(prefabs.shelter, hall.transform, new Vector2(cx, 0f), "Shelter");

            hall.seals = new[] { left, right };
            hall.slab = shelter.GetComponentInChildren<VioletSlab>();
            hall.rainRange = new Vector2(1.6f, len - 1.6f);
            hall.duration = duration;
            hall.telegraph = 3f;
            Record(hall);

            var z = Zone(prefabs, VioletZone.Kind.Rain, label, x + 4, x + len - 2);
            z.rainHall = hall;
            Record(z);
        }

        static float BuildArena(Prefabs prefabs, int x)
        {
            int inner0 = x + ArenaWall, inner1 = inner0 + ArenaWidth;
            Fill(x, -GroundDepth, inner1 + ArenaWall, 0);
            Fill(x, DoorHeight, inner0, ArenaHeight); // the left wall, over a doorway the door fills
            Fill(inner1, 0, inner1 + ArenaWall, ArenaHeight);
            Fill(inner0 + PlatformInset, PlatformTop - 1, inner0 + PlatformInset + PlatformLength, PlatformTop);
            Fill(inner1 - PlatformInset - PlatformLength, PlatformTop - 1, inner1 - PlatformInset, PlatformTop);

            var arena = new GameObject("Arena").AddComponent<VioletArena>();
            arena.transform.SetParent(root, false);
            arena.transform.position = new Vector3(inner0, 0f, 0f);
            arena.inner = new Rect(0f, 0f, ArenaWidth, ArenaHeight);
            arena.cameraView = new Rect(ArenaWidth * 0.5f - 12.45f, -2f, 24.9f, 14f);
            arena.playerSpawn = Marker("PlayerSpawn", arena.transform, new Vector2(PlayerInset, 0f));
            arena.bossSpawn = Marker("BossSpawn", arena.transform, new Vector2(ArenaWidth - BossInset, 0f));

            var door = Place(prefabs.seal, arena.transform, new Vector2(x + ArenaWall * 0.5f, DoorHeight * 0.5f), "Door").GetComponent<VioletGate>();
            var sr = door.GetComponent<SpriteRenderer>();
            sr.size = new Vector2(ArenaWall, DoorHeight);
            door.GetComponent<BoxCollider2D>().size = new Vector2(ArenaWall, DoorHeight);
            Set(door, "sinkDepth", DoorHeight + 0.2f);
            Set(door, "startClosed", true);
            Record(sr);
            Record(door.GetComponent<BoxCollider2D>());
            arena.door = door;
            Record(arena);
            return inner1 + ArenaWall;
        }

        static Transform Marker(string name, Transform parent, Vector2 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static VioletZone Zone(Prefabs prefabs, VioletZone.Kind kind, string label, float x0, float x1)
        {
            var go = Place(prefabs.zone, zones, new Vector2(x0, 0f), $"Zone {label} ({kind})");
            var z = go.GetComponent<VioletZone>();
            z.kind = kind;
            z.label = label;
            var box = go.GetComponent<BoxCollider2D>();
            box.size = new Vector2(x1 - x0, 50f);
            box.offset = new Vector2((x1 - x0) * 0.5f, 15f);
            Record(box);
            return z;
        }

        static void Fill(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x < x1; x++)
            for (int y = y0; y < y1; y++)
                solid.Add(new Vector2Int(x, y));
        }

        // ---------- Painting ----------

        static void Paint()
        {
            var grid = new GameObject(GridName).AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var go = new GameObject("Tilemap");
            go.transform.SetParent(grid.transform, false);
            var map = go.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = TilemapOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = go.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            var recolor = go.AddComponent<Recolorable>();
            Set(recolor, "color", ColorId.Violet);
            Set(recolor, "desaturate", true);

            var edge = VioletTileBuilder.EdgeTiles;
            var inner = VioletTileBuilder.InnerTiles;
            var cells = solid.ToArray();
            var tiles = new TileBase[cells.Length];
            for (int i = 0; i < cells.Length; i++) tiles[i] = Pick(cells[i], solid.Contains, edge, inner);
            map.SetTiles(cells.Select(c => new Vector3Int(c.x, c.y, 0)).ToArray(), tiles);
            // No collider yet: the TilemapCollider2D takes tile changes lazily, so generating the merged collider here
            // merges nothing. FinishTerrain builds it once the tiles are processed, checks it, then saves.
        }

        /// <summary>
        /// The 9-slice piece for a cell from its four neighbours (edges and corners), or for a cell inside the mass,
        /// an inner corner where one diagonal is open (a concave corner) or the plain middle. solidAt = is that cell ground.
        /// </summary>
        static TileBase Pick(Vector2Int c, System.Func<Vector2Int, bool> solidAt, Tile[] edge, Tile[] inner)
        {
            bool Has(int dx, int dy) => solidAt(new Vector2Int(c.x + dx, c.y + dy));
            bool up = Has(0, 1), down = Has(0, -1), left = Has(-1, 0), right = Has(1, 0);
            if (!up) return !left ? edge[0] : !right ? edge[2] : edge[1];
            if (!down) return !left ? edge[6] : !right ? edge[8] : edge[7];
            if (!left) return edge[3];
            if (!right) return edge[5];
            if (!Has(1, -1)) return inner[0];  // notch bottom-right
            if (!Has(-1, -1)) return inner[1]; // notch bottom-left
            if (!Has(1, 1)) return inner[2];   // notch top-right
            if (!Has(-1, 1)) return inner[3];  // notch top-left
            return edge[4];
        }

        // ---------- The terrain's collider ----------

        /// <summary>
        /// Builds the tilemap's merged collider from its tiles (after they're processed), checks the level is solid where
        /// it must be, and saves the scene only if every check passes. Otherwise: an error listing what failed, and the
        /// scene is left open, unsaved, with the rebuilt collider.
        /// </summary>
        static bool FinishTerrain(Scene scene, string done, string commit)
        {
            var composites = VioletTerrain.Composites(scene);
            int paths = 0;
            foreach (var composite in composites)
            {
                paths += VioletTerrain.Rebuild(composite);
                EditorUtility.SetDirty(composite);
            }
            EditorSceneManager.MarkSceneDirty(scene);

            var report = new StringBuilder();
            var errors = VerifyTerrain(scene, composites, paths, report);
            if (errors.Count > 0)
            {
                Debug.LogError($"[ROYGBIV] {done}, but Level_Violet FAILED its checks, so it was NOT saved (it's open with the rebuilt " +
                               "collider). Fix what's listed and run this again:\n  - " + string.Join("\n  - ", errors) + "\n" + report);
                return false;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ROYGBIV] {done}. Every check passed and Level_Violet is saved: commit {commit}.\n{report}");
            return true;
        }

        /// <summary>
        /// The merged collider has paths; a ray straight down lands on the tiles right under the start, every checkpoint
        /// (each segment), the arrival, the king's spot on his hill and the arena's spawns; rays across the arena hit both
        /// walls; and every solid piece (seals, the door, slabs, crystals, the arrival's Stop) has a solid collider.
        /// </summary>
        static List<string> VerifyTerrain(Scene scene, List<CompositeCollider2D> composites, int paths, StringBuilder report)
        {
            var errors = new List<string>();
            if (composites.Count == 0) { errors.Add("no Grid/Tilemap with a TilemapCollider2D merged into a CompositeCollider2D"); return errors; }
            if (paths == 0) { errors.Add("the tilemap's collider is still EMPTY after rebuilding it: are the Violet tiles' Collider Type set to Sprite?"); return errors; }
            report.AppendLine($"  tilemap collider: {paths} paths");
            Physics2D.SyncTransforms();

            void Floor(string what, Vector2 feet)
            {
                if (!VioletTerrain.GroundBelow(feet + Vector2.up * 0.5f, 3f, out float y)) errors.Add($"{what}: no tile ground under ({feet.x:0.##}, {feet.y:0.##})");
                else if (Mathf.Abs(y - feet.y) > 0.2f) errors.Add($"{what}: the tile ground is at y {y:0.##}, expected {feet.y:0.##}");
                else report.AppendLine($"  {what}: solid ground at ({feet.x:0.##}, {y:0.##})");
            }

            var markers = FindAllInScene<VioletCheckpointMarker>(scene).OrderBy(m => m.Feet.x).ToArray();
            if (markers.Length == 0) errors.Add("no checkpoints");
            for (int i = 0; i < markers.Length; i++) Floor(i == 0 ? $"start ({markers[i].label})" : $"checkpoint {markers[i].label}", markers[i].Feet);

            var arrival = FindInScene<VioletArrival>(scene);
            if (arrival) Floor("arrival (foot of the hill)", arrival.transform.position);
            else errors.Add("no Arrival");

            var far = FindInScene<VioletFarKing>(scene);
            if (!far) errors.Add("no Far King");
            else if (!VioletTerrain.GroundBelow(far.Spot + Vector2.up * 2f, 32f, out float hillY)) errors.Add($"the king's hill: no tile ground under the Far King at {far.Spot}");
            else report.AppendLine($"  the king's hill: he stands at ({far.Spot.x:0.##}, {hillY:0.##})");

            var arena = FindInScene<VioletArena>(scene);
            if (!arena) errors.Add("no Arena");
            else
            {
                Floor("arena, player spawn", arena.PlayerFeet);
                Floor("arena, boss spawn", new Vector2(arena.BossX, arena.Floor));
                var middle = new Vector2((arena.MinX + arena.MaxX) * 0.5f, arena.Floor + DoorHeight + 3f); // above the door
                foreach (var (what, dir, wallX) in new[] { ("left", Vector2.left, arena.MinX), ("right", Vector2.right, arena.MaxX) })
                {
                    if (!VioletTerrain.Raycast(middle, dir, arena.inner.width, out var hit)) errors.Add($"arena, {what} wall: no tiles across the arena");
                    else if (Mathf.Abs(hit.x - wallX) > 0.2f) errors.Add($"arena, {what} wall: tiles at x {hit.x:0.##}, expected {wallX:0.##}");
                    else report.AppendLine($"  arena, {what} wall: solid at x {hit.x:0.##}");
                }
            }

            var pieces = FindAllInScene<VioletGate>(scene).Cast<Component>()
                .Concat(FindAllInScene<VioletSlab>(scene)).Concat(FindAllInScene<VioletCrystal>(scene)).ToList();
            var stop = arrival ? arrival.GetComponentsInChildren<BoxCollider2D>(true).FirstOrDefault(b => !b.isTrigger) : null;
            if (stop) pieces.Add(stop);
            else if (arrival) errors.Add("the arrival has no solid Stop");
            foreach (var piece in pieces)
            {
                var c = piece.GetComponent<Collider2D>();
                if (!c || !c.enabled || c.isTrigger || c.bounds.size.x < 0.01f || c.bounds.size.y < 0.01f)
                    errors.Add($"{piece.name} ({piece.GetType().Name}): no enabled, solid (non-trigger) collider");
            }
            report.AppendLine($"  solid pieces (seals, door, slabs, crystals, stop): {pieces.Count} checked");
            return errors;
        }

        static T FindInScene<T>(Scene scene) where T : Component => FindAllInScene<T>(scene).FirstOrDefault();

        static IEnumerable<T> FindAllInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

        // ---------- The rest of the scene ----------

        static int CleanSkeleton(float end)
        {
            int removed = 0;
            var env = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Environment" && !t.parent);
            if (!env) return 0;
            foreach (Transform child in env.Cast<Transform>().ToArray())
            {
                if (child.name is "Ground" or "Wall" or "Platform") { Object.DestroyImmediate(child.gameObject); removed++; }
                else if (child.name == "KillZone")
                {
                    float x0 = -14f, x1 = end + 10f;
                    child.position = new Vector3((x0 + x1) * 0.5f, -GroundDepth - 14f, 0f);
                    child.localScale = new Vector3(x1 - x0, 2f, 1f);
                }
            }
            return removed;
        }

        static void SetUpLevel(float firstCheckpointX)
        {
            var level = Object.FindAnyObjectByType<LevelController>(FindObjectsInactive.Include);
            if (level) Set(level, "startBossImmediately", false); // the arrival cinematic starts the duel
            var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player) player.transform.position = new Vector3(firstCheckpointX, 0.65f, 0f);
        }

        // ---------- Prefabs ----------

        sealed class Prefabs
        {
            public GameObject checkpoint, zone, seal, shelter, crystal, curtain;
        }

        static Prefabs MakePrefabs() => new()
        {
            checkpoint = Prefab("Violet Checkpoint", BuildCheckpoint),
            zone = Prefab("Violet Zone", BuildZone),
            seal = Prefab("Violet Seal", BuildSeal),
            shelter = Prefab("Violet Shelter", BuildShelter),
            crystal = Prefab("Violet Crystal Gate", BuildCrystal),
            curtain = Prefab("Violet Curtain", BuildCurtain),
        };

        /// <summary>The prefab at Prefabs/Violet/name, made with `build` if it doesn't exist (an existing one is kept as is).</summary>
        static GameObject Prefab(string name, System.Func<GameObject> build)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var go = build();
            go.name = name;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector2 position, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
            go.name = name;
            Record(go);
            Record(go.transform);
            return go;
        }

        static GameObject BuildCheckpoint()
        {
            var go = new GameObject();
            var marker = go.AddComponent<VioletCheckpointMarker>();
            Sprite("Pole", go.transform, new Vector2(-1.2f, 1.2f), new Vector2(0.1f, 2.4f), BannerColor * 0.7f, 2);
            var flag = Sprite("Flag", go.transform, new Vector2(-0.75f, 2.15f), new Vector2(0.8f, 0.45f), BannerColor, 2);
            Set(marker, "flag", flag);
            return go;
        }

        static GameObject BuildZone()
        {
            var go = new GameObject();
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.enabled = false;
            go.AddComponent<VioletZone>();
            return go;
        }

        /// <summary>A stone block that sinks into the floor: Royal Rain's seals and, resized, the arena door.</summary>
        static GameObject BuildSeal()
        {
            var go = new GameObject();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(1f, SealHeight);
            sr.sortingOrder = SealOrder;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, SealHeight);
            box.sharedMaterial = noFriction;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var recolor = go.AddComponent<Recolorable>();
            Set(recolor, "color", ColorId.Violet);
            Set(recolor, "desaturate", true);
            var gate = go.AddComponent<VioletGate>();
            Set(gate, "sinkDepth", SealHeight + 0.2f);
            return go;
        }

        /// <summary>The shelter's slab, the anchor it hangs from (shoot it) and the chain. Its origin is the floor under the slab's center.</summary>
        static GameObject BuildShelter()
        {
            var go = new GameObject();
            float slabWidth = ShelterInner + 2f, slabY = SlabHang + SlabThickness * 0.5f;
            float landY = PillarHeight + SlabThickness * 0.5f;

            var slab = new GameObject("Slab");
            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = new Vector2(0f, slabY);
            var sr = slab.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(slabWidth, SlabThickness);
            sr.color = SlabColor;
            sr.sortingOrder = SlabOrder;
            var box = slab.AddComponent<BoxCollider2D>();
            box.size = new Vector2(slabWidth, SlabThickness);
            box.sharedMaterial = noFriction;
            var body = slab.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            slab.AddComponent<VioletRainPassThrough>();
            var slabLogic = slab.AddComponent<VioletSlab>();
            var destructible = slab.AddComponent<VioletDestructibleSlab>();
            Set(destructible, "placedInScene", true);

            Vector2 anchorPos = new(AnchorOffset, AnchorY + 0.025f);
            var anchor = Sprite("Anchor (shoot it)", go.transform, anchorPos, new Vector2(0.9f, 0.65f), new Color(1f, 0.88f, 0.55f), 3);
            anchor.gameObject.AddComponent<BoxCollider2D>().sharedMaterial = noFriction;
            anchor.gameObject.AddComponent<VioletRainPassThrough>();
            var weakPoint = anchor.gameObject.AddComponent<VioletShelterWeakPoint>();
            Set(weakPoint, "slab", slabLogic);
            Set(weakPoint, "placedInScene", true);

            var beam = Sprite("AnchorBeam", go.transform, new Vector2(AnchorOffset, AnchorY + 0.6f), new Vector2(3.2f, 0.5f), new Color(0.55f, 0.5f, 0.62f), 3);
            beam.gameObject.AddComponent<BoxCollider2D>().sharedMaterial = noFriction;
            beam.gameObject.AddComponent<VioletRainPassThrough>();

            var chain = new GameObject("Chain").transform;
            chain.SetParent(go.transform, false);
            Vector2 from = new(0f, SlabHang + SlabThickness), to = new(AnchorOffset, AnchorY - 0.3f), along = to - from;
            int count = Mathf.Max(1, Mathf.CeilToInt(along.magnitude / 0.26f));
            float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg - 90f;
            var links = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                links[i] = Sprite("Link", chain, Vector2.Lerp(from, to, (i + 0.5f) / count), new Vector2(0.14f, 0.26f), new Color(0.7f, 0.65f, 0.75f), 5).transform;
                links[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            Set(slabLogic, "dropDistance", slabY - landY);
            SetArray(slabLogic, "chainLinks", links);
            Set(slabLogic, "anchor", anchor.transform);
            Set(slabLogic, "placedInScene", true);
            return go;
        }

        static GameObject BuildCrystal()
        {
            var go = new GameObject();
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.4f, CrystalHeight);
            box.offset = new Vector2(0f, CrystalHeight * 0.5f);
            box.sharedMaterial = noFriction;
            go.AddComponent<VioletCrystal>();
            return go;
        }

        static GameObject BuildCurtain()
        {
            var go = new GameObject();
            var curtain = go.AddComponent<VioletCurtain>();
            Set(curtain, "placedInScene", true);
            Set(curtain, "width", (float)CurtainWidth);
            Set(curtain, "ceilingHeight", (float)CurtainCeiling);
            // The run's tuning (from the play-tested runtime course): a narrow, quick gap.
            Set(curtain, "settings.gapWidth", 2f);
            Set(curtain, "settings.speedFraction", 0.9f);
            Set(curtain, "settings.accelerationFraction", 0.9f);
            Set(curtain, "settings.burstTime", new Vector2(0.10f, 0.16f));
            Set(curtain, "settings.stopTime", new Vector2(0.08f, 0.14f));
            Set(curtain, "settings.parkOverlap", 0.9f);
            return go;
        }

        static SpriteRenderer Sprite(string name, Transform parent, Vector2 local, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- Clearances ----------

        /// <summary>Checks every clearance of the snapped layout against the real Player.prefab and logs the table.</summary>
        static void ValidateClearances()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var motor = player ? player.GetComponent<PlayerMotor>() : null;
            var body = player ? player.GetComponent<Rigidbody2D>() : null;
            var col = player ? player.GetComponent<BoxCollider2D>() : null;
            float g = Mathf.Abs(Physics2D.gravity.y) * (body ? body.gravityScale : 3f);
            float jump = motor ? motor.jumpVelocity : 13f;
            float stand = col ? col.size.y : 1.2f, wide = col ? col.size.x : 0.8f;
            float second = Read<DoubleJumpAbility>(player, "jumpVelocity", 12f);
            float dash = Read<DashAbility>(player, "speed", 18f) * Read<DashAbility>(player, "duration", 0.18f);
            float surf = stand * Read<DownDashAbility>(player, "lowHeight", 0.45f);
            float single = jump * jump / (2f * g), twice = single + second * second / (2f * g);

            var errors = new List<string>();
            void Need(bool ok, string what) { if (!ok) errors.Add(what); }
            foreach (int h in StepHeights) Need(h > single + 0.05f && h <= twice - 0.5f, $"step {h}: must be over a single jump ({single:0.00}) and at most a double jump - 0.5 ({twice - 0.5f:0.00})");
            Need(WallHeight > single + 0.05f && WallHeight <= twice - 0.5f, $"wall {WallHeight}: same rule as the steps");
            Need(TunnelHeight >= surf + 0.1f && TunnelHeight < stand, $"tunnel {TunnelHeight}: must fit a surf ({surf:0.00}) + 0.1 but not a standing player ({stand:0.00})");
            Need(LowBeamGap >= surf + 0.05f && LowBeamGap + 0.1f < stand, $"low beam gap {LowBeamGap}: must clear a surf ({surf:0.00}) but not a standing player");
            Need(LedgeStepHeight <= single - 0.3f && LedgeHeight - LedgeStepHeight <= single - 0.3f, $"ledge {LedgeStepHeight}/{LedgeHeight}: each rise must be a comfortable single jump ({single:0.00})");
            Need(CorridorCeiling >= stand + 0.5f, $"corridor ceiling {CorridorCeiling}: room to stand and dash");
            Need(CurtainWidth >= dash * 1.5f, $"curtain {CurtainWidth}: must be well over one dash ({dash:0.00})");
            Need(PillarHeight >= stand + 0.2f && ShelterInner >= wide + 2f, $"shelter {ShelterInner}x{PillarHeight}: room under the slab");
            Need(PlatformTop <= twice - 0.5f, $"arena platforms {PlatformTop}: reachable with a double jump");

            var table = new StringBuilder();
            table.AppendLine($"[Violet bake] Player: {wide}x{stand}, surf height {surf:0.00}, single jump {single:0.00}, double jump {twice:0.00}, dash {dash:0.00}, run {(motor ? motor.runSpeed : 0f)}.");
            table.AppendLine($"  corridor ceiling {CorridorCeiling} (waves fill it) | steps {string.Join("/", StepHeights)} | wall {WallHeight} | low beam gap {LowBeamGap} | tunnel {TunnelHeight}");
            table.AppendLine($"  ledge step {LedgeStepHeight}, ledge {LedgeHeight} | shelter {ShelterInner} wide x {PillarHeight} high | curtain {CurtainWidth} wide, ceiling {CurtainCeiling} | king's hill {HillTop} high | arena {ArenaWidth}x{ArenaHeight}, platforms at {PlatformTop}");
            if (errors.Count == 0) Debug.Log(table + "  All clearances OK.");
            else Debug.LogError(table + "  Clearance problems:\n  - " + string.Join("\n  - ", errors));
        }

        static float Read<T>(GameObject player, string field, float fallback) where T : Component
        {
            var c = player ? player.GetComponentInChildren<T>(true) : null;
            if (!c) return fallback;
            var p = new SerializedObject(c).FindProperty(field);
            return p != null ? p.floatValue : fallback;
        }

        // ---------- Serialization helpers ----------

        static void Record(Object o)
        {
            if (o && PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'"); return; }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case bool b: p.boolValue = b; break;
                case System.Enum e: p.intValue = System.Convert.ToInt32(e); break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                case Vector2 v: p.vector2Value = v; break;
                default: throw new System.ArgumentException($"Unsupported type {value?.GetType()}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"{target.GetType().Name} has no serialized array '{field}'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
