using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A checkpoint of Violet's run, placed in the scene: its position is where the player's FEET land on a respawn.
    /// Checkpoints count left to right (by x), so adding one is just dropping in another marker. The banner (a pole and
    /// a flag) lights up the first time the player passes it. VioletCheckpoint remembers the furthest one reached.
    /// </summary>
    public class VioletCheckpointMarker : MonoBehaviour
    {
        [Tooltip("Segment name, for logs.")]
        public string label;
        [Tooltip("The banner's flag: it lights up when the checkpoint is reached.")]
        [SerializeField] SpriteRenderer flag;
        [Tooltip("The flag's color once reached.")]
        [SerializeField] Color litColor = new(0.886f, 0.722f, 1f);

        public Vector2 Feet => transform.position;

        public void Light(bool burst)
        {
            if (!flag) return;
            flag.color = litColor;
            if (burst) VioletHits.Burst(flag.transform.position, litColor, 10, 4f, 0.3f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 1f);
            Gizmos.DrawWireCube(transform.position + new Vector3(0f, 0.6f), new Vector3(0.8f, 1.2f, 0f));
        }
    }
}
