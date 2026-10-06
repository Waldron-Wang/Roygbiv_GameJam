using UnityEditor;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Add New Abilities To Player.
    /// Adds the abilities designed after the skeleton was built (Down Dash, Serenity) to Player.prefab › Abilities.
    /// Safe to re-run: an ability that's already on the prefab is left exactly as it is.
    /// </summary>
    public static class AbilitySetup
    {
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";

        [MenuItem("ROYGBIV/Add New Abilities To Player")]
        public static void AddNewAbilities()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            if (!root) { Debug.LogError($"[ROYGBIV] No prefab at {PlayerPath}."); return; }
            try
            {
                var abilities = root.transform.Find("Abilities");
                if (!abilities)
                {
                    abilities = new GameObject("Abilities").transform;
                    abilities.SetParent(root.transform, false);
                }

                var added = new System.Collections.Generic.List<string>();
                if (!root.GetComponentInChildren<DownDashAbility>(true))
                {
                    var downDash = abilities.gameObject.AddComponent<DownDashAbility>();
                    SetFloat(downDash, "cooldown", 0.6f);
                    added.Add("Down Dash");
                }
                if (!root.GetComponentInChildren<SerenityAbility>(true))
                {
                    var serenity = abilities.gameObject.AddComponent<SerenityAbility>();
                    SetFloat(serenity, "cooldown", 0f); // it runs its own real-time duration / recharge
                    added.Add("Serenity");
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
                Debug.Log(added.Count > 0
                    ? $"[ROYGBIV] Player.prefab: added {string.Join(", ", added)} under Abilities."
                    : "[ROYGBIV] Player.prefab already has every new ability. Nothing changed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
