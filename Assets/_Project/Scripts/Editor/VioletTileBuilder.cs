using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Make Violet Tiles. Violet's tile set, made exactly like Waldron's Yellow one:
    ///   1. Slices Art/tiles/violetTile.png into a 3x3 grid of 256 px cells (violetTile_0..8: _0 _1 _2 top row,
    ///      _3 _4 _5 middle, _6 _7 _8 bottom) and violetInnerTile.png into 2x2 (violetInnerTile_0..3: the inner
    ///      corners, _0 top-left ... _3 bottom-right). 256 px per unit, so one cell = one tile.
    ///   2. Makes a Tile asset per slice in Art/tiles/VioletTiles/ (same settings as YellowTiles: white, Lock Color,
    ///      collider from the sprite).
    ///   3. Makes the palette Art/tiles/Palettes/Violet.prefab with the tiles laid out like Yellow.prefab:
    ///      the 9-slice block at x -1..1, y 0..-2 and the inner corners at x -4..-3, y -1..-2.
    /// Safe to re-run: slicing, tiles and palette cells are only (re)set, nothing else is touched.
    /// </summary>
    public static class VioletTileBuilder
    {
        const string ArtDir = "Assets/_Project/Art/tiles";
        const string TileDir = ArtDir + "/VioletTiles";
        const string PaletteDir = ArtDir + "/Palettes";
        const string PalettePath = PaletteDir + "/Violet.prefab";
        const string EdgeTexture = ArtDir + "/violetTile.png";
        const string InnerTexture = ArtDir + "/violetInnerTile.png";
        const int Cell = 256;

        /// <summary>The 9-slice tiles: index 0..8 = top-left, top, top-right, left, middle, right, bottom-left, bottom, bottom-right.</summary>
        public static Tile[] EdgeTiles => Load("violetTile", 9);
        /// <summary>Inner corners: 0 = notch bottom-right, 1 = notch bottom-left, 2 = notch top-right, 3 = notch top-left.</summary>
        public static Tile[] InnerTiles => Load("violetInnerTile", 4);

        [MenuItem("ROYGBIV/Make Violet Tiles")]
        public static void Make()
        {
            Directory.CreateDirectory(TileDir);
            Directory.CreateDirectory(PaletteDir);
            AssetDatabase.Refresh();

            bool sliced = Slice(EdgeTexture, "violetTile", 3, 3) | Slice(InnerTexture, "violetInnerTile", 2, 2);
            int made = MakeTiles(EdgeTexture, "violetTile", 9) + MakeTiles(InnerTexture, "violetInnerTile", 4);
            MakePalette();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ROYGBIV] Violet tiles ready: {(sliced ? "sliced the textures, " : "")}{made} new Tile assets in {TileDir}, palette {PalettePath}. " +
                      "Save and commit Art/tiles (the .png.meta slicing, VioletTiles/, Palettes/Violet.prefab).");
        }

        /// <summary>True when all the tiles exist (the bake needs them).</summary>
        public static bool Ready => EdgeTiles.All(t => t) && InnerTiles.All(t => t);

        static Tile[] Load(string baseName, int count)
        {
            var tiles = new Tile[count];
            for (int i = 0; i < count; i++) tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/{baseName}_{i}.asset");
            return tiles;
        }

        /// <summary>Slices `path` into columns x rows cells named baseName_0.. (row by row from the TOP, like Yellow). False if it already was.</summary>
        static bool Slice(string path, string baseName, int columns, int rows)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) { Debug.LogError($"[ROYGBIV] No texture at {path}."); return false; }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();

            var wanted = new List<SpriteRect>();
            int i = 0;
            for (int r = rows - 1; r >= 0; r--)
            for (int c = 0; c < columns; c++, i++)
            {
                string name = $"{baseName}_{i}";
                var rect = new Rect(c * Cell, r * Cell, Cell, Cell);
                var old = existing.FirstOrDefault(e => e.name == name);
                wanted.Add(new SpriteRect
                {
                    name = name,
                    rect = rect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = old != null ? old.spriteID : GUID.Generate(),
                });
            }

            bool same = existing.Length == wanted.Count && wanted.All(w => existing.Any(e => e.name == w.name && e.rect == w.rect));
            if (same && importer.spriteImportMode == SpriteImportMode.Multiple && Mathf.Approximately(importer.spritePixelsPerUnit, Cell)) return false;

            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Cell;
            provider.SetSpriteRects(wanted.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(wanted.Select(w => new SpriteNameFileIdPair(w.name, w.spriteID)).ToList());
            provider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        static int MakeTiles(string texturePath, string baseName, int count)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToDictionary(s => s.name);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                string name = $"{baseName}_{i}";
                if (!sprites.TryGetValue(name, out var sprite)) { Debug.LogError($"[ROYGBIV] {texturePath} has no sprite {name}."); continue; }
                string path = $"{TileDir}/{name}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (!tile)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.name = name;
                    AssetDatabase.CreateAsset(tile, path);
                    made++;
                }
                // Same as YellowTiles: white, Lock Color, collider shaped by the sprite.
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.flags = TileFlags.LockColor;
                tile.colliderType = Tile.ColliderType.Sprite;
                EditorUtility.SetDirty(tile);
            }
            return made;
        }

        static void MakePalette()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath))
                GridPaletteUtility.CreateNewPalette(PaletteDir, "Violet", GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);

            var root = PrefabUtility.LoadPrefabContents(PalettePath);
            try
            {
                var map = root.GetComponentInChildren<Tilemap>();
                if (!map) { Debug.LogError($"[ROYGBIV] {PalettePath} has no Tilemap layer."); return; }
                var edge = EdgeTiles;
                var inner = InnerTiles;
                // Yellow.prefab's layout: the 3x3 block at x -1..1 / y 0..-2, the inner corners at x -4..-3 / y -1..-2.
                for (int i = 0; i < 9; i++) map.SetTile(new Vector3Int(-1 + i % 3, -(i / 3), 0), edge[i]);
                map.SetTile(new Vector3Int(-4, -1, 0), inner[0]);
                map.SetTile(new Vector3Int(-3, -1, 0), inner[1]);
                map.SetTile(new Vector3Int(-4, -2, 0), inner[2]);
                map.SetTile(new Vector3Int(-3, -2, 0), inner[3]);
                PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
