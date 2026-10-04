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

        public bool TakeDamage(in DamageInfo info)
        {
            if (--hits > 0) return true;
            if (cameraShake > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam))
                cam.Shake(cameraShake, 0.15f);
            Destroy(gameObject);
            return true;
        }
    }
}
