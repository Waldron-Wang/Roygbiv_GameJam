using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roygbiv
{
    /// <summary>
    /// Makes Level_Violet work even if ROYGBIV > Build Violet Level was never run (or its result never committed).
    /// The boss calls this from Awake: if the scene has no VioletApproach, it builds the same setup the menu would,
    /// at runtime: the skeleton's flat arena (Environment › Ground / Wall / Platform) is switched off, a "Violet"
    /// object with a VioletCourse (flat blocks: the tile art isn't loadable at runtime) and a VioletApproach is
    /// created, and the LevelController stops starting the boss on its own. The menu stays the nicer option
    /// (tile art, editable in the Inspector); this is the safety net. One warning in the editor says it happened.
    /// </summary>
    public static class VioletSetup
    {
        public static void EnsureLevel(VioletBoss boss)
        {
            if (Object.FindAnyObjectByType<VioletApproach>(FindObjectsInactive.Include)) return;
            var scene = boss.gameObject.scene;

            int disabled = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Environment") continue;
                foreach (Transform child in root.transform)
                {
                    if (child.name is "Ground" or "Wall" or "Platform")
                    {
                        if (child.gameObject.activeSelf) disabled++;
                        child.gameObject.SetActive(false);
                    }
                    else if (child.name == "KillZone") // under the whole course
                    {
                        child.position = new Vector3(250f, -20f, 0f);
                        child.localScale = new Vector3(800f, 2f, 1f);
                    }
                }
            }

            var level = Object.FindAnyObjectByType<LevelController>();
            if (level) level.StartBossImmediately = false; // walking into the arena starts the fight

            var go = new GameObject("Violet (built at runtime)");
            SceneManager.MoveGameObjectToScene(go, scene);

            // The block template: a white square that belongs to Violet (gray until Violet is restored).
            var template = new GameObject("BlockTemplate");
            template.SetActive(false); // so Recolorable wakes up only on the copies, with their color set
            template.transform.SetParent(go.transform, false);
            template.AddComponent<SpriteRenderer>().sprite = FlatSprite.Square;
            template.AddComponent<Recolorable>().ColorId = ColorId.Violet;

            var material = new PhysicsMaterial2D("VioletNoFriction") { friction = 0f, bounciness = 0f };
            go.AddComponent<VioletCourse>().Configure(template, material, null);
            go.AddComponent<VioletApproach>();

#if UNITY_EDITOR
            Debug.LogWarning($"[ROYGBIV] Level_Violet had no Violet setup: built it at runtime (disabled {disabled} old arena objects, " +
                "made the course + director, turned off the boss's immediate start). For tile art and Inspector tuning, run " +
                "ROYGBIV > Build Violet Level, then save the scene and commit it.", go);
#endif
        }
    }
}
