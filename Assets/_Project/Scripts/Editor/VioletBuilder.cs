using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Violet Level. Sets up the final level; everything else (the course, the king, his
    /// attacks) is built at runtime from code, so this only wires what has to live in assets:
    ///   Boss_Violet.prefab  24 HP, one threshold at 0.5 (duel / Twin Blades), a tall body collider.
    ///   Level_Violet        removes the skeleton's flat arena (Ground / Wall / Platform under Environment),
    ///                       adds a "Violet" object (VioletCourse + VioletApproach + an inactive BlockTemplate with
    ///                       Recolorable(Violet) and the violet tile art), turns off startBossImmediately (the arena
    ///                       entrance starts the fight), and stretches the KillZone under the whole course.
    /// Safe to re-run: existing objects are reused and only these fields are (re)set. Touches no other scene or prefab.
    /// </summary>
    public static class VioletBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Level_Violet.unity";
        const string BossPath = "Assets/_Project/Prefabs/Bosses/Boss_Violet.prefab";
        const string SquarePath = "Assets/_Project/Art/Placeholder/Square.png";
        const string MaterialPath = "Assets/_Project/Art/Placeholder/NoFriction.physicsMaterial2D";
        const string TilePath = "Assets/_Project/Art/tiles/violetTile.png";
        const string RootName = "Violet";
        const string TemplateName = "BlockTemplate (copied by VioletCourse)";

        [MenuItem("ROYGBIV/Build Violet Level")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetUpBoss();
            SetUpScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ROYGBIV] Violet level built. Open Level_Violet and press Play (abilities are granted automatically in the editor).");
        }

        static void SetUpBoss()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            if (!root) { Debug.LogError($"[ROYGBIV] No prefab at {BossPath}."); return; }
            try
            {
                if (root.TryGetComponent<Health>(out var health))
                {
                    Set(health, "maxHealth", 24);
                    Set(health, "hitInvulnerability", 0.2f);
                }
                if (root.TryGetComponent<VioletBoss>(out var boss))
                {
                    Set(boss, "displayName", "Violet Sovereign");
                    SetFloats(boss, "phaseThresholds", 0.5f);
                }
                else Debug.LogError("[ROYGBIV] Boss_Violet has no VioletBoss component.");
                if (root.TryGetComponent<BoxCollider2D>(out var box))
                {
                    box.size = new Vector2(1.6f, 3.2f);
                    box.offset = Vector2.zero;
                }
                PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetUpScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            // The skeleton's flat arena: the course replaces it.
            int removed = 0;
            var environment = roots.FirstOrDefault(r => r.name == "Environment");
            if (environment)
            {
                foreach (Transform child in environment.transform.Cast<Transform>().ToArray())
                {
                    if (child.name is "Ground" or "Wall" or "Platform") { Object.DestroyImmediate(child.gameObject); removed++; }
                    else if (child.name == "KillZone")
                    {
                        child.position = new Vector3(190f, -20f, 0f);
                        child.localScale = new Vector3(600f, 2f, 1f);
                    }
                }
            }

            var level = Object.FindAnyObjectByType<LevelController>(FindObjectsInactive.Include);
            if (level) Set(level, "startBossImmediately", false);
            else Debug.LogError("[ROYGBIV] Level_Violet has no LevelController.");

            var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player) player.transform.position = new Vector3(2f, 0.7f, 0f); // VioletApproach moves it to the checkpoint anyway

            var violet = roots.FirstOrDefault(r => r.name == RootName) ?? new GameObject(RootName);
            violet.transform.position = Vector3.zero;
            // TryGetComponent, not GetComponent() ?? AddComponent(): in the editor a missing component is a fake null that ?? lets through.
            if (!violet.TryGetComponent<VioletCourse>(out var course)) course = violet.AddComponent<VioletCourse>();
            if (!violet.TryGetComponent<VioletApproach>(out var approach)) approach = violet.AddComponent<VioletApproach>();

            var templateT = violet.transform.Find(TemplateName);
            var template = templateT ? templateT.gameObject : null;
            if (!template)
            {
                template = new GameObject(TemplateName);
                template.transform.SetParent(violet.transform, false);
            }
            if (!template.TryGetComponent<SpriteRenderer>(out var sr)) sr = template.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            sr.color = Color.white;
            if (!template.TryGetComponent<Recolorable>(out var recolor)) recolor = template.AddComponent<Recolorable>();
            Set(recolor, "color", ColorId.Violet);
            template.SetActive(false);

            Set(course, "blockTemplate", template);
            Set(course, "solidMaterial", AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath));
            var tile = AssetDatabase.LoadAllAssetsAtPath(TilePath).OfType<Sprite>().FirstOrDefault();
            if (tile) Set(course, "tileSprite", tile);
            else Debug.LogWarning($"[ROYGBIV] No sprite in {TilePath}: the course will use flat blocks.");
            Set(approach, "course", course);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ROYGBIV] Level_Violet: removed {removed} old arena objects, Violet course + director ready, boss starts at the arena entrance.");
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
                default: throw new System.ArgumentException($"Unsupported type {value?.GetType()}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloats(Object target, string field, params float[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"{target.GetType().Name} has no serialized array '{field}'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
