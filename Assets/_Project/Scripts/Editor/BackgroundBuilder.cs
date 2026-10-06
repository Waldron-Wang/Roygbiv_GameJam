using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Placeholder Background (Level_Yellow).
    /// Generates one placeholder image per parallax layer (Far_yellow / Mid_yellow / Near_yellow: scattered
    /// yellow squares, a different brightness per layer) and adds this to Level_Yellow:
    ///
    ///   Background
    ///   ├── Far   ParallaxLayer   └── Far_yellow   SpriteRenderer · Recolorable(Yellow, Desaturate)
    ///   ├── Mid   ParallaxLayer   └── Mid_yellow   ...
    ///   └── Near  ParallaxLayer   └── Near_yellow  ...
    ///
    /// Real art: drop Far_red.png etc. in as more children of the same layer (same components), or just
    /// swap the sprite on the placeholders. Safe to re-run: skips a scene that already has a Background.
    /// </summary>
    public static class BackgroundBuilder
    {
        const string ArtDir = "Assets/_Project/Art/Background/Placeholder";
        const string ScenePath = "Assets/_Project/Scenes/Level_Yellow.unity";
        const ColorId LevelColor = ColorId.Yellow;
        const int Ppu = 4;

        struct LayerSpec
        {
            public string name;
            public float follow, verticalFollow;
            public int sortingOrder;
            public float y;            // image center, relative to the camera's authored position
            public Vector2 size;       // image size in world units
            public Vector2 squareSize; // min / max square edge
            public Vector2 gap;        // min / max gap between squares
            public Color tint;
        }

        // Far is brightest and near is darkest, so the layers stay apart once grayed out.
        static readonly LayerSpec[] Layers =
        {
            new() { name = "Far", follow = 0.85f, verticalFollow = 0.9f, sortingOrder = -30, y = 0f,
                size = new Vector2(90f, 30f), squareSize = new Vector2(8f, 14f), gap = new Vector2(2f, 6f), tint = new Color(1f, .96f, .7f) },
            new() { name = "Mid", follow = 0.55f, verticalFollow = 0.7f, sortingOrder = -20, y = -2f,
                size = new Vector2(100f, 20f), squareSize = new Vector2(5f, 9f), gap = new Vector2(2f, 5f), tint = new Color(.95f, .75f, .1f) },
            new() { name = "Near", follow = 0.25f, verticalFollow = 0.5f, sortingOrder = -10, y = -4f,
                size = new Vector2(110f, 12f), squareSize = new Vector2(3f, 5f), gap = new Vector2(2f, 5f), tint = new Color(.7f, .5f, .05f) },
        };

        [MenuItem("ROYGBIV/Build Placeholder Background (Level_Yellow)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(ArtDir);
            AssetDatabase.Refresh();

            string colorName = LevelColor.ToString().ToLowerInvariant();
            var sprites = new Sprite[Layers.Length];
            for (int i = 0; i < Layers.Length; i++)
                sprites[i] = MakeSquaresSprite($"{ArtDir}/{Layers[i].name}_{colorName}.png", Layers[i], seed: i + 1);

            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (GameObject.Find("Background"))
            {
                Debug.Log("[ROYGBIV] Level_Yellow already has a Background — left untouched. Delete it and re-run to rebuild.");
                return;
            }

            var cam = Camera.main;
            var root = new GameObject("Background");
            root.transform.position = cam ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f) : Vector3.zero;

            for (int i = 0; i < Layers.Length; i++)
            {
                var spec = Layers[i];
                var layer = new GameObject(spec.name);
                layer.transform.SetParent(root.transform, false);
                var parallax = layer.AddComponent<ParallaxLayer>();
                Set(parallax, "follow", spec.follow);
                Set(parallax, "verticalFollow", spec.verticalFollow);

                var image = new GameObject($"{spec.name}_{colorName}");
                image.transform.SetParent(layer.transform, false);
                image.transform.localPosition = new Vector3(0f, spec.y, 0f);
                var sr = image.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[i];
                sr.sortingOrder = spec.sortingOrder;
                var recolor = image.AddComponent<Recolorable>();
                Set(recolor, "color", (int)LevelColor);
                Set(recolor, "desaturate", true);
                Set(recolor, "grayBrightness", 0.5f); // 0.5 = keep the art's own brightness when gray
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ROYGBIV] Placeholder background added to Level_Yellow.");
        }

        /// <summary>A row of randomly sized, randomly raised squares on a transparent image.</summary>
        static Sprite MakeSquaresSprite(string path, LayerSpec spec, int seed)
        {
            if (!File.Exists(path))
            {
                int w = Mathf.RoundToInt(spec.size.x * Ppu), h = Mathf.RoundToInt(spec.size.y * Ppu);
                var pixels = new Color32[w * h]; // all transparent
                Color32 fill = spec.tint;
                var rng = new System.Random(seed);
                float Range(Vector2 r) => r.x + (float)rng.NextDouble() * (r.y - r.x);

                float x = Range(new Vector2(0f, spec.gap.y));
                while (x < spec.size.x)
                {
                    float size = Range(spec.squareSize);
                    float y = Range(new Vector2(0f, Mathf.Max(0f, spec.size.y - size)));
                    int x0 = Mathf.RoundToInt(x * Ppu), y0 = Mathf.RoundToInt(y * Ppu), s = Mathf.RoundToInt(size * Ppu);
                    for (int py = y0; py < Mathf.Min(h, y0 + s); py++)
                    for (int px = x0; px < Mathf.Min(w, x0 + s); px++)
                        pixels[py * w + px] = fill;
                    x += size + Range(spec.gap);
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'"); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
