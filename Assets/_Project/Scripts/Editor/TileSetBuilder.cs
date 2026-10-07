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
    /// Makes one color's tile set the way Yellow's and Violet's were made, from Art/tiles/&lt;color&gt;Tile.png and
    /// &lt;color&gt;InnerTile.png (e.g. "blue"):
    ///   1. Slices &lt;color&gt;Tile.png into a 3x3 grid of 256 px cells (_0 _1 _2 top row, _3 _4 _5 middle, _6 _7 _8 bottom)
    ///      and &lt;color&gt;InnerTile.png into 2x2 (the inner corners). 256 px per unit, so one cell = one tile.
    ///   2. Makes a Tile asset per slice in Art/tiles/&lt;Color&gt;Tiles/ (white, Lock Color, collider from the sprite).
    ///   3. Makes the palette Art/tiles/Palettes/&lt;Color&gt;.prefab, laid out like Yellow.prefab.
    /// Safe to re-run: slicing, tiles and palette cells are only (re)set, nothing else is touched.
    /// </summary>
    public class TileSetBuilder
    {
        const string ArtDir = "Assets/_Project/Art/tiles";
        const string PaletteDir = ArtDir + "/Palettes";
        const int Cell = 256;

        readonly string edgeName, innerName, tileDir, palettePath, paletteName;

        /// <param name="color">Lower-case color word used in the file names ("blue").</param>
        public TileSetBuilder(string color)
        {
            paletteName = char.ToUpperInvariant(color[0]) + color.Substring(1);
            edgeName = color + "Tile";
            innerName = color + "InnerTile";
            tileDir = $"{ArtDir}/{paletteName}Tiles";
            palettePath = $"{PaletteDir}/{paletteName}.prefab";
        }

        /// <summary>The 9-slice tiles: index 0..8 = top-left, top, top-right, left, middle, right, bottom-left, bottom, bottom-right.</summary>
        public Tile[] EdgeTiles => Load(edgeName, 9);
        /// <summary>Inner corners: 0 = notch bottom-right, 1 = notch bottom-left, 2 = notch top-right, 3 = notch top-left.</summary>
        public Tile[] InnerTiles => Load(innerName, 4);
        /// <summary>True when all the tiles exist.</summary>
        public bool Ready => EdgeTiles.All(t => t) && InnerTiles.All(t => t);

        public void Make()
        {
            Directory.CreateDirectory(tileDir);
            Directory.CreateDirectory(PaletteDir);
            AssetDatabase.Refresh();

            string edgeTexture = $"{ArtDir}/{edgeName}.png", innerTexture = $"{ArtDir}/{innerName}.png";
            bool sliced = Slice(edgeTexture, edgeName, 3, 3) | Slice(innerTexture, innerName, 2, 2);
            int made = MakeTiles(edgeTexture, edgeName, 9) + MakeTiles(innerTexture, innerName, 4);
            MakePalette();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ROYGBIV] {paletteName} tiles ready: {(sliced ? "sliced the textures, " : "")}{made} new Tile assets in {tileDir}, palette {palettePath}. " +
                      $"Save and commit Art/tiles (the .png.meta slicing, {paletteName}Tiles/, Palettes/{paletteName}.prefab).");
        }

        Tile[] Load(string baseName, int count)
        {
            var tiles = new Tile[count];
            for (int i = 0; i < count; i++) tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>($"{tileDir}/{baseName}_{i}.asset");
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
                var old = existing.FirstOrDefault(e => e.name == name);
                wanted.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(c * Cell, r * Cell, Cell, Cell),
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

        int MakeTiles(string texturePath, string baseName, int count)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToDictionary(s => s.name);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                string name = $"{baseName}_{i}";
                if (!sprites.TryGetValue(name, out var sprite)) { Debug.LogError($"[ROYGBIV] {texturePath} has no sprite {name}."); continue; }
                string path = $"{tileDir}/{name}.asset";
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

        void MakePalette()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(palettePath))
                GridPaletteUtility.CreateNewPalette(PaletteDir, paletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);

            var root = PrefabUtility.LoadPrefabContents(palettePath);
            try
            {
                var map = root.GetComponentInChildren<Tilemap>();
                if (!map) { Debug.LogError($"[ROYGBIV] {palettePath} has no Tilemap layer."); return; }
                var edge = EdgeTiles;
                var inner = InnerTiles;
                // Yellow.prefab's layout: the 3x3 block at x -1..1 / y 0..-2, the inner corners at x -4..-3 / y -1..-2.
                for (int i = 0; i < 9; i++) map.SetTile(new Vector3Int(-1 + i % 3, -(i / 3), 0), edge[i]);
                map.SetTile(new Vector3Int(-4, -1, 0), inner[0]);
                map.SetTile(new Vector3Int(-3, -1, 0), inner[1]);
                map.SetTile(new Vector3Int(-4, -2, 0), inner[2]);
                map.SetTile(new Vector3Int(-3, -2, 0), inner[3]);
                PrefabUtility.SaveAsPrefabAsset(root, palettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The 9-slice piece for a cell from its four neighbours (edges and corners), or for a cell inside the mass,
        /// an inner corner where one diagonal is open (a concave corner) or the plain middle. Same rule as Violet's bake.
        /// </summary>
        public TileBase Pick(HashSet<Vector2Int> solid, Vector2Int c, Tile[] edge, Tile[] inner)
        {
            bool Has(int dx, int dy) => solid.Contains(new Vector2Int(c.x + dx, c.y + dy));
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
    }

    /// <summary>Menu: ROYGBIV > Make Blue Tiles. Blue's tile set: BlueTiles/ and Palettes/Blue.prefab (see TileSetBuilder).</summary>
    public static class BlueTileBuilder
    {
        public static readonly TileSetBuilder Tiles = new("blue");

        [MenuItem("ROYGBIV/Make Blue Tiles")]
        public static void Make() => Tiles.Make();
    }
}
