using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roygbiv
{
    public enum GameColor { Violet, Indigo, Blue, Green, Yellow, Orange, Red }

    /// <summary>
    /// Tracks which colors the player has earned and feeds them to the ColorRestore material.
    /// Call ColorProgression.Instance.Unlock(GameColor.Red) when a boss is beaten.
    /// </summary>
    public class ColorProgression : MonoBehaviour
    {
        public static ColorProgression Instance { get; private set; }

        [Tooltip("The material using the Roygbiv/ColorRestore shader (same one the Full Screen Pass uses).")]
        [SerializeField] Material material;
        [SerializeField] float fadeTime = 2f;
        [Tooltip("Save unlocked colors between runs.")]
        [SerializeField] bool persist = true;

        const int Count = 7;
        const string SaveKey = "Roygbiv.UnlockedColors";

        readonly float[] current = new float[Count]; // what the shader sees (fades in)
        readonly bool[] unlocked = new bool[Count];
        float fullColor;

        public bool IsUnlocked(GameColor c) => unlocked[(int)c];

        public bool AllUnlocked
        {
            get
            {
                foreach (var u in unlocked) if (!u) return false;
                return true;
            }
        }

        void Awake()
        {
            Instance = this;

            if (persist)
            {
                int mask = PlayerPrefs.GetInt(SaveKey, 0);
                for (int i = 0; i < Count; i++)
                {
                    unlocked[i] = (mask & (1 << i)) != 0;
                    current[i] = unlocked[i] ? 1f : 0f;
                }
                fullColor = AllUnlocked ? 1f : 0f;
            }
            Apply();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Don't leave the shared material asset gray in the editor.
            if (material) material.SetFloat("_FullColor", 1f);
        }

        public void Unlock(GameColor c)
        {
            unlocked[(int)c] = true;
            if (persist) Save();
        }

        public void ResetProgress()
        {
            for (int i = 0; i < Count; i++) { unlocked[i] = false; current[i] = 0f; }
            fullColor = 0f;
            if (persist) Save();
            Apply();
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);

            bool allFaded = true;
            for (int i = 0; i < Count; i++)
            {
                current[i] = Mathf.MoveTowards(current[i], unlocked[i] ? 1f : 0f, step);
                if (current[i] < 1f) allFaded = false;
            }

            // Only start the final "back to normal" blend after every color has faded in.
            fullColor = Mathf.MoveTowards(fullColor, allFaded ? 1f : 0f, step);
            Apply();

        }

        void Apply()
        {
            if (!material) return;
            material.SetFloatArray("_Unlock", current);
            material.SetFloat("_FullColor", fullColor);
        }

        void Save()
        {
            int mask = 0;
            for (int i = 0; i < Count; i++) if (unlocked[i]) mask |= 1 << i;
            PlayerPrefs.SetInt(SaveKey, mask);
            PlayerPrefs.Save();
        }

        [ContextMenu("Test: Unlock Next")]
        void TestUnlockNext()
        {
            for (int i = 0; i < Count; i++)
                if (!unlocked[i])
                {
                    Unlock((GameColor)i);
                    Debug.Log($"Unlocked {(GameColor)i}, material assigned: {material != null}");
                    return;
                }
            Debug.Log("All colors already unlocked");
        }

        [ContextMenu("Test: Reset")]
        void TestReset()
        {
            ResetProgress();
            Debug.Log($"Reset, material assigned: {material != null}");
        }
    }
}