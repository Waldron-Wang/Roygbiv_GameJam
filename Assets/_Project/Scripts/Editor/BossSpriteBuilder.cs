using UnityEditor;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Boss Sprites.
    /// Imports the boss art in Art/Boss as single sprites (size + pivot below) and wires each boss prefab:
    /// real sprite on Visual (scale 1, untinted — the art carries its color) and a BossSprites component
    /// that swaps idle / attack / hurt / move by the boss's Pose. Safe to re-run.
    ///
    /// Sizes fit each boss's 2x2 collider (~2.4 units tall); pivots put the collider center on the body
    /// (and for the ground-runners, the feet on the collider's bottom). Every frame of a boss shares one
    /// pivot, since they're drawn on the same 800x600 canvas.
    /// </summary>
    public static class BossSpriteBuilder
    {
        const string Art = "Assets/_Project/Art/Boss";
        const string Prefabs = "Assets/_Project/Prefabs/Bosses";

        struct BossArt
        {
            public string prefab, idle, attack, hurt;
            public string[] move;
            public float pixelsPerUnit, moveFps;
            public Vector2 pivot;
        }

        static readonly BossArt[] Bosses =
        {
            // Hovers: pivot at the middle of the body.
            new() { prefab = "Boss_Yellow", idle = "yellowIdle", attack = "yellowAttack", hurt = "yellowHurt",
                    pixelsPerUnit = 190f, pivot = new Vector2(0.49f, 0.44f) },
            // Runs on the ground: feet 1 unit (= 180 px) below the pivot. No attack art: it keeps running.
            new() { prefab = "Boss_Orange", idle = "orangeIdle", hurt = "orangeHurt", move = new[] { "orangeRun1", "orangeRun2" },
                    moveFps = 8f, pixelsPerUnit = 180f, pivot = new Vector2(0.52f, 0.31f) },
            // Flies, but lands to charge / collapse: feet 1 unit (= 200 px) below the pivot.
            new() { prefab = "Boss_Red", idle = "redIdle", attack = "redAttack", hurt = "redHurt",
                    pixelsPerUnit = 200f, pivot = new Vector2(0.53f, 0.335f) },
        };

        [MenuItem("ROYGBIV/Build Boss Sprites")]
        public static void Build()
        {
            int built = 0;
            foreach (var art in Bosses)
                if (BuildBoss(art)) built++;
            Debug.Log($"[BossSpriteBuilder] Wired {built}/{Bosses.Length} bosses.");
        }

        static bool BuildBoss(BossArt art)
        {
            var idle = ImportSprite(art.idle, art);
            if (!idle) return false;
            var attack = ImportSprite(art.attack, art);
            var hurt = ImportSprite(art.hurt, art);
            var move = new Sprite[art.move?.Length ?? 0];
            for (int i = 0; i < move.Length; i++) move[i] = ImportSprite(art.move[i], art);

            string path = $"{Prefabs}/{art.prefab}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer target = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Visual" && t.TryGetComponent(out target)) break;
                if (!target)
                {
                    Debug.LogError($"[BossSpriteBuilder] {path} has no Visual child with a SpriteRenderer.");
                    return false;
                }

                // Size now comes from Pixels Per Unit, not the placeholder square's stretch.
                target.transform.localScale = Vector3.one;
                target.sprite = idle;
                target.color = Color.white;

                if (!root.TryGetComponent<BossSprites>(out var sprites)) sprites = root.AddComponent<BossSprites>();
                var so = new SerializedObject(sprites);
                so.FindProperty("target").objectReferenceValue = target;
                so.FindProperty("idle").objectReferenceValue = idle;
                so.FindProperty("attack").objectReferenceValue = attack;
                so.FindProperty("hurt").objectReferenceValue = hurt;
                var moveProp = so.FindProperty("move");
                moveProp.arraySize = move.Length;
                for (int i = 0; i < move.Length; i++) moveProp.GetArrayElementAtIndex(i).objectReferenceValue = move[i];
                if (art.moveFps > 0f) so.FindProperty("moveFps").floatValue = art.moveFps;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Single sprite, the boss's size + pivot, smooth filtering. Null name = no such pose.</summary>
        static Sprite ImportSprite(string name, BossArt art)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string path = $"{Art}/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"[BossSpriteBuilder] Missing {path}.");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = art.pivot;
            settings.spritePixelsPerUnit = art.pixelsPerUnit;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
