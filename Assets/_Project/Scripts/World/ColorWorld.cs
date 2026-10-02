using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// "How colorful is the world right now?" — one 0..1 value per color, animated when a color
    /// is restored. Visuals (Recolorable, shaders, post-processing, lights) read THIS and never
    /// look at the save file, so the art team can change HOW recoloring looks without touching flow.
    ///
    /// Also exposes shader globals for when the art style is picked:
    ///   _Roygbiv_Red, _Roygbiv_Orange, ... (0..1 each)   _Roygbiv_Saturation (average, 0..1)
    /// </summary>
    public class ColorWorld : MonoBehaviour
    {
        static readonly ColorId[] AllColors = (ColorId[])Enum.GetValues(typeof(ColorId));
        static readonly int SaturationId = Shader.PropertyToID("_Roygbiv_Saturation");

        readonly Dictionary<ColorId, float> amounts = new();
        readonly Dictionary<ColorId, int> shaderIds = new();

        /// <summary>(color, amount 0..1). Fires every frame while a color animates in.</summary>
        public event Action<ColorId, float> AmountChanged;

        public float GetAmount(ColorId color) => amounts.TryGetValue(color, out var v) ? v : 0f;

        public float OverallSaturation
        {
            get
            {
                float sum = 0f;
                foreach (var c in AllColors) sum += GetAmount(c);
                return sum / AllColors.Length;
            }
        }

        void Awake()
        {
            foreach (var c in AllColors) shaderIds[c] = Shader.PropertyToID("_Roygbiv_" + c);
        }

        void Start() => SyncWithProgress();

        void OnEnable() => GameEvents.ColorRestored += OnColorRestored;
        void OnDisable() => GameEvents.ColorRestored -= OnColorRestored;

        /// <summary>Snap every color to the save file (new game, load, debug).</summary>
        public void SyncWithProgress()
        {
            StopAllCoroutines();
            foreach (var c in AllColors) Set(c, Game.Progress.IsRestored(c) ? 1f : 0f);
        }

        void OnColorRestored(ColorId color) => StartCoroutine(Animate(color));

        IEnumerator Animate(ColorId color)
        {
            float duration = Mathf.Max(0.01f, Game.Config.recolorDuration);
            for (float t = GetAmount(color) * duration; t < duration; t += Time.deltaTime)
            {
                Set(color, t / duration);
                yield return null;
            }
            Set(color, 1f);
        }

        void Set(ColorId color, float value)
        {
            amounts[color] = value;
            Shader.SetGlobalFloat(shaderIds[color], value);
            Shader.SetGlobalFloat(SaturationId, OverallSaturation);
            AmountChanged?.Invoke(color, value);
        }
    }
}
