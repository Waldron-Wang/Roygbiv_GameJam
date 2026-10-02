using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The save file: plain data, no Unity objects. Owned by GameManager; read it via Game.Progress.
    /// Only GameManager should modify it — everyone else listens to GameEvents.
    /// </summary>
    [Serializable]
    public class GameProgress
    {
        const string SaveKey = "roygbiv.save";

        public List<ColorId> restoredColors = new();
        public List<AbilityId> unlockedAbilities = new();

        public int RestoredCount => restoredColors.Count;
        public bool IsRestored(ColorId color) => restoredColors.Contains(color);
        public bool HasAbility(AbilityId ability) => unlockedAbilities.Contains(ability);

        public void Restore(ColorId color)
        {
            if (!IsRestored(color)) restoredColors.Add(color);
        }

        public void Unlock(AbilityId ability)
        {
            if (ability != AbilityId.None && !HasAbility(ability)) unlockedAbilities.Add(ability);
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);

        public static GameProgress Load()
        {
            var json = PlayerPrefs.GetString(SaveKey, "");
            return string.IsNullOrEmpty(json) ? new GameProgress() : JsonUtility.FromJson<GameProgress>(json);
        }

        public static void DeleteSave() => PlayerPrefs.DeleteKey(SaveKey);
    }
}
