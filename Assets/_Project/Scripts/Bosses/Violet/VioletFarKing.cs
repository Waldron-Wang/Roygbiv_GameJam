using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The far king's anchor, placed in the scene near the end of the run. During the run the king is a big distant
    /// figure pinned near the right of the view on a far hill (VioletApproach places him every frame with these
    /// numbers). This object's position is where the intro camera starts: framed on him, close up, before the pull back
    /// to the player.
    /// </summary>
    public class VioletFarKing : MonoBehaviour
    {
        [Tooltip("Size of the distant king at the start and at the end of the run (1 = life size).")]
        public Vector2 scale = new(1.25f, 2.1f);
        [Tooltip("How hazy (faded toward the distance) he is at the start and at the end of the run.")]
        public Vector2 haze = new(0.6f, 0.12f);
        [Tooltip("How far in from the right screen edge he stands, in his own (scaled) units.")]
        public float inset = 2.4f;
        [Tooltip("Height of his feet (the far hill's top) relative to the camera center.")]
        public float hillLine = -1.9f;
        [Tooltip("The far hill under his feet.")]
        public Color hillColor = new(0.15f, 0.1f, 0.22f);

        /// <summary>Where the intro camera starts.</summary>
        public Vector2 IntroView => transform.position;

        void OnDrawGizmos()
        {
            // The intro's first frame, at 16:9 and the level camera's size.
            Gizmos.color = new Color(0.9f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(24.9f, 14f, 0f));
        }
    }
}
