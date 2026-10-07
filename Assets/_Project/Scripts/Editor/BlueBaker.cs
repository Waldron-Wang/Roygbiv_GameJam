using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Bake Blue Level Into Scene. Paints Blue's vertical climb into Level_Blue from the map below:
    ///   Grid > Tilemap set up like Level_Yellow (TilemapCollider2D merged into a CompositeCollider2D, static Rigidbody2D,
    ///   Recolorable(Blue) with Desaturate), each cell given its 9-slice tile from its neighbours (inner corners at concave
    ///   corners), drawn with Art/tiles/blueTile (made by ROYGBIV > Make Blue Tiles if missing).
    /// It also removes the old placeholder Environment pieces (Ground, Walls, Ledges, the Goal trigger), moves the player
    /// to P, and points the Blue boss at the summit row (its `summit`, `summitHalfWidth`, and its start just above).
    /// The summit floor itself is NOT painted: the boss builds it at runtime as a one-way platform that catches every fall.
    /// Running it again ASKS before replacing an existing Grid, so hand edits aren't lost by accident.
    ///
    /// Every rise was checked against the player's real movement (run 10, jump 13, double jump 12, dash 18 x 0.18 s,
    /// 0.8 x 1.2 body): sections 1-2 need only single jumps, 3-6 need the double jump, dash is never required.
    /// Keep 2+ empty rows between a ledge and anything overhanging it (the player is 1.2 tall), and check the head
    /// room on every jump path when you edit.
    /// </summary>
    public static class BlueBaker
    {
        const string ScenePath = "Assets/_Project/Scenes/Level_Blue.unity";
        const string GridName = "Grid";
        /// <summary>World x of the map's first column. The row holding P is y 0: the top of the ground.</summary>
        const int Left = -14;
        /// <summary>How far over the summit floor the boss waits before the fight starts.</summary>
        const float BossAboveSummit = 4f;

        // Legend: # solid tile   . empty   P the player's start (stands on the cell below)
        //         = the summit row: the boss's one-way floor spans it (top at the row's top edge). Not painted.
        // Top of the file is the top of the level. Row comments give each row's world y (the cell spans y..y+1).
        static readonly string[] Map =
        {
            "##........................##", // 113
            "##........................##", // 112
            "##........................##", // 111
            "##........................##", // 110
            "##........................##", // 109
            "##........................##", // 108
            "##........................##", // 107
            "##........................##", // 106
            "##........................##", // 105
            "##........................##", // 104
            "##........................##", // 103
            "##........................##", // 102
            "##........................##", // 101
            "##........................##", // 100
            "##========================##", // 99  = the summit: the boss's one-way floor (top at y 100)
            "##........................##", // 98
            "##...........####.........##", // 97
            "##........................##", // 96
            "##.................####...##", // 95
            "##........................##", // 94
            "##...........####.........##", // 93
            "##........................##", // 92
            "##........................##", // 91
            "##....####................##", // 90
            "##........................##", // 89  6 last ascent
            "##...........####.........##", // 88
            "##........................##", // 87
            "##........................##", // 86
            "##.................###....##", // 85
            "##........................##", // 84
            "##.........###............##", // 83
            "##........................##", // 82
            "##........................##", // 81
            "##..###...................##", // 80
            "##........................##", // 79
            "##...........###..........##", // 78
            "##........................##", // 77
            "##....................###.##", // 76
            "##........................##", // 75
            "##........................##", // 74  5 stepping stones
            "##################........##", // 73
            "##################........##", // 72
            "##........................##", // 71
            "##........................##", // 70
            "##.............#############", // 69
            "##.............#############", // 68
            "##........................##", // 67
            "##........................##", // 66
            "#####################.....##", // 65
            "#####################.....##", // 64
            "##........................##", // 63
            "##........................##", // 62
            "##................##########", // 61
            "##................##########", // 60
            "##........................##", // 59
            "##........................##", // 58
            "###################.......##", // 57
            "##........................##", // 56
            "##........................##", // 55
            "##........................##", // 54  4 overhangs
            "##..................##....##", // 53
            "##..................##....##", // 52
            "##..................##....##", // 51
            "##..................##....##", // 50
            "##.............##...##....##", // 49
            "##.............##...##....##", // 48
            "##.............##...##....##", // 47
            "##..................##....##", // 46
            "##......##..........##....##", // 45
            "##......##..........##....##", // 44
            "##......##..........##....##", // 43
            "##..................##....##", // 42
            "####................##....##", // 41
            "####................##....##", // 40
            "####................##....##", // 39
            "####................##....##", // 38
            "####...##...........##....##", // 37
            "####...##...........##....##", // 36
            "####...##...........##....##", // 35
            "####...##...........##....##", // 34  3 towers (rest floor)
            "###########....#############", // 33
            "###########......###########", // 32
            "#############....###########", // 31
            "###########......###########", // 30
            "###########....#############", // 29
            "###########......###########", // 28
            "#############....###########", // 27
            "###########......###########", // 26
            "###########....#############", // 25
            "###########......###########", // 24
            "#############....###########", // 23
            "###########......###########", // 22
            "###########....#############", // 21
            "###########......###########", // 20
            "#############....###########", // 19
            "###########......###########", // 18
            "###########....#############", // 17
            "###########......###########", // 16
            "##.........##.............##", // 15
            "##........................##", // 14  2 the chimney
            "##.........#########......##", // 13
            "##........................##", // 12
            "##....................######", // 11
            "##........................##", // 10
            "##..............####......##", // 9
            "##........................##", // 8
            "##........####............##", // 7
            "##........................##", // 6
            "##..#####.................##", // 5
            "##........................##", // 4
            "##.........####...........##", // 3
            "##........................##", // 2
            "##...............#####....##", // 1
            "##............P...........##", // 0  1 first steps (P = the player)
            "############################", // -1  ground
            "############################", // -2
            "############################", // -3
            "############################", // -4
        };

        [MenuItem("ROYGBIV/Bake Blue Level Into Scene")]
        public static void Bake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var tiles = BlueTileBuilder.Tiles;
            if (!tiles.Ready) tiles.Make();
            if (!tiles.Ready) { Debug.LogError("[ROYGBIV] The Blue tiles couldn't be made: run ROYGBIV > Make Blue Tiles and check the console."); return; }
            if (!Parse(out var solid, out var spawn, out var summit, out float summitHalfWidth, out string problem))
            {
                Debug.LogError("[ROYGBIV] Blue map: " + problem);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var oldGrid = scene.GetRootGameObjects().FirstOrDefault(r => r.name == GridName);
            if (oldGrid && !EditorUtility.DisplayDialog("Level_Blue is already baked",
                    "Bake it again from the map in BlueBaker.cs?\n\nThis DELETES the current Blue tilemap, including any hand edits to it.",
                    "Replace", "Cancel")) return;
            if (oldGrid) Object.DestroyImmediate(oldGrid);

            Paint(tiles, solid);
            int removed = CleanSkeleton();
            SetUpLevel(spawn, summit, summitHalfWidth);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ROYGBIV] Blue baked into Level_Blue: {solid.Count} tiles painted, {removed} placeholder objects removed, " +
                      $"climb {summit.y - spawn.y:0} units tall, summit at {summit} (half width {summitHalfWidth}). Scene saved. " +
                      "Commit Level_Blue.unity and Art/tiles/ (BlueTiles, Palettes/Blue.prefab, the .png.meta slicing).");
        }

        /// <summary>Reads the map: solid cells, the player's start, and the summit row (center of its top edge, half width).</summary>
        static bool Parse(out HashSet<Vector2Int> solid, out Vector2 spawn, out Vector2 summit, out float summitHalfWidth, out string problem)
        {
            solid = new HashSet<Vector2Int>();
            spawn = summit = Vector2.zero;
            summitHalfWidth = 0f;
            problem = null;

            int width = Map[0].Length;
            int spawnRow = System.Array.FindIndex(Map, r => r.Contains('P'));
            if (spawnRow < 0) { problem = "no P (the player's start)."; return false; }

            int summitX0 = int.MaxValue, summitX1 = int.MinValue, summitY = int.MinValue;
            for (int r = 0; r < Map.Length; r++)
            {
                string row = Map[r];
                if (row.Length != width) { problem = $"row {r} is {row.Length} wide, the first row is {width}."; return false; }
                int y = spawnRow - r;
                for (int c = 0; c < row.Length; c++)
                {
                    int x = Left + c;
                    switch (row[c])
                    {
                        case '#': solid.Add(new Vector2Int(x, y)); break;
                        case 'P': spawn = new Vector2(x + 0.5f, y); break;
                        case '=':
                            if (summitY != int.MinValue && summitY != y) { problem = "the summit (=) must be a single row."; return false; }
                            summitY = y;
                            summitX0 = Mathf.Min(summitX0, x);
                            summitX1 = Mathf.Max(summitX1, x);
                            break;
                        case '.': break;
                        default: problem = $"unknown character '{row[c]}' in row {r}."; return false;
                    }
                }
            }
            if (summitY == int.MinValue) { problem = "no summit row (=)."; return false; }
            summit = new Vector2((summitX0 + summitX1 + 1) * 0.5f, summitY + 1);
            summitHalfWidth = (summitX1 + 1 - summitX0) * 0.5f;
            return true;
        }

        static void Paint(TileSetBuilder tiles, HashSet<Vector2Int> solid)
        {
            var grid = new GameObject(GridName).AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var go = new GameObject("Tilemap");
            go.transform.SetParent(grid.transform, false);
            var map = go.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 0;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = go.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            var recolor = go.AddComponent<Recolorable>();
            Set(recolor, "color", ColorId.Blue);
            Set(recolor, "desaturate", true);

            var edge = tiles.EdgeTiles;
            var inner = tiles.InnerTiles;
            var cells = solid.ToArray();
            var painted = new TileBase[cells.Length];
            for (int i = 0; i < cells.Length; i++) painted[i] = tiles.Pick(solid, cells[i], edge, inner);
            map.SetTiles(cells.Select(c => new Vector3Int(c.x, c.y, 0)).ToArray(), painted);
            map.CompressBounds();
            composite.GenerateGeometry();
        }

        /// <summary>Removes the skeleton's placeholder geometry (the KillZone stays).</summary>
        static int CleanSkeleton()
        {
            var env = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Environment" && !t.parent);
            if (!env) return 0;
            int removed = 0;
            foreach (Transform child in env.Cast<Transform>().ToArray())
                if (child.name is "Ground" or "Wall" or "Ledge" or "Platform" or "Goal (CompleteLevel)")
                {
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
            return removed;
        }

        static void SetUpLevel(Vector2 spawn, Vector2 summit, float summitHalfWidth)
        {
            var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player)
            {
                player.transform.position = new Vector3(spawn.x, spawn.y + 0.65f, 0f);
                Record(player.transform);
            }

            var boss = Object.FindAnyObjectByType<BlueBoss>(FindObjectsInactive.Include);
            if (!boss) { Debug.LogError("[ROYGBIV] Level_Blue has no BlueBoss: the summit wasn't set."); return; }
            Set(boss, "summit", summit);
            Set(boss, "summitHalfWidth", summitHalfWidth);
            boss.transform.position = new Vector3(summit.x, summit.y + BossAboveSummit, 0f);
            Record(boss.transform);
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
                case bool b: p.boolValue = b; break;
                case System.Enum e: p.intValue = System.Convert.ToInt32(e); break;
                case float f: p.floatValue = f; break;
                case Vector2 v: p.vector2Value = v; break;
                default: throw new System.ArgumentException($"Unsupported type {value?.GetType()}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
