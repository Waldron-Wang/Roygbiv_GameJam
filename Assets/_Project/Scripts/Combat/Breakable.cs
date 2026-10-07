using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Crates and other junk: enough hits and it's gone. Team.Neutral, so player shots, melee and even
    /// enemy shots all count. Needs a non-trigger collider (Orange: crate walls block the run until broken).
    /// </summary>
    public class Breakable : MonoBehaviour, IDamageable
    {
        [SerializeField] int hits = 1;
        [SerializeField] float cameraShake = 0.1f;

        public Team Team => Team.Neutral;
        public int Hits { get => hits; set => hits = value; }

        /// <summary>A hit that didn't break it (for visuals: flash, jolt).</summary>
        public event Action Damaged;
        /// <summary>The last hit, just before it's destroyed (for visuals: debris).</summary>
        public event Action Broken;

        bool broken;

        public bool TakeDamage(in DamageInfo info)
        {
            if (broken) return true; // two hits in one frame: it's already going
            if (--hits > 0)
            {
                Damaged?.Invoke();
                return true;
            }
            broken = true;
            Broken?.Invoke();
            if (cameraShake > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam))
                cam.Shake(cameraShake, 0.15f);
            Destroy(gameObject);
            return true;
        }
    }
}
