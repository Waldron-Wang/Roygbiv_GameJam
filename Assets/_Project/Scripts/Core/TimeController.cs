using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The one owner of Time.timeScale and Time.fixedDeltaTime. Systems ask it instead of writing them, so they
    /// can't undo each other (unpausing used to set 1 and kill any slow motion):
    ///   Game.Time.Pause(this) / Resume(this)            pause menu, the Tip card: time stops while any pause is held
    ///   Game.Time.SetScale(this, 0.35f) / ClearScale(this)  slow motion (Serenity): requests multiply together
    /// A pause wins over every scale, and lifting it brings the slow motion back exactly as it was.
    /// fixedDeltaTime follows the scale, so physics keeps stepping at the same real rate and stays smooth.
    /// Scene loads call ResetAll(): normal speed, nothing held.
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        readonly HashSet<object> pauses = new();
        readonly Dictionary<object, float> scales = new();
        float baseFixedDeltaTime = 0.02f;

        /// <summary>True while anything holds a pause (pause menu, Tip card). Real-time timers should stop then.</summary>
        public bool IsPaused => pauses.Count > 0;

        /// <summary>Every scale request multiplied together (1 = normal speed). Pauses aren't included.</summary>
        public float Scale { get; private set; } = 1f;

        void Awake()
        {
            baseFixedDeltaTime = Time.fixedDeltaTime;
            Apply();
        }

        // Leaving Play mode: don't leave the editor in slow motion.
        void OnDestroy()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
        }

        public void Pause(object owner)
        {
            if (owner != null && pauses.Add(owner)) Apply();
        }

        public void Resume(object owner)
        {
            if (owner != null && pauses.Remove(owner)) Apply();
        }

        /// <param name="scale">0.35 = everything at 35% speed. Several owners' scales multiply.</param>
        public void SetScale(object owner, float scale)
        {
            if (owner == null) return;
            scales[owner] = Mathf.Clamp(scale, 0.05f, 4f);
            Apply();
        }

        public void ClearScale(object owner)
        {
            if (owner != null && scales.Remove(owner)) Apply();
        }

        /// <summary>Normal speed, no pause, no slow motion. Scene loads call this.</summary>
        public void ResetAll()
        {
            pauses.Clear();
            scales.Clear();
            Apply();
        }

        void Apply()
        {
            float scale = 1f;
            foreach (var s in scales.Values) scale *= s;
            Scale = scale;
            Time.timeScale = IsPaused ? 0f : scale;
            Time.fixedDeltaTime = baseFixedDeltaTime * scale;
        }
    }
}
