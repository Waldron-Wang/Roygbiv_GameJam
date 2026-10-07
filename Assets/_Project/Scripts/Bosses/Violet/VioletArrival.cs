using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The foot of the king's hill, placed in the scene: when the player's x reaches this object's x, VioletApproach plays
    /// the arrival cinematic (letterbox, wipe, cut to the arena). Its "Stop" child is a solid wall at the hill's first
    /// step, so nobody climbs to the king.
    /// </summary>
    public class VioletArrival : MonoBehaviour
    {
        public float X => transform.position.x;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f);
            var p = transform.position;
            Gizmos.DrawLine(p + Vector3.down, p + Vector3.up * 8f);
            Gizmos.DrawWireSphere(p + Vector3.up * 8f, 0.3f);
        }
    }
}
