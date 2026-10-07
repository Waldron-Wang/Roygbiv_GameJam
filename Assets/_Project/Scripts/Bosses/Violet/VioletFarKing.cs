using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Where the king stands during the run: on top of his hill at the far end of the course (the hill is painted in the
    /// tilemap). When the level starts his feet go on the tile ground right under this object, so it only has to be
    /// somewhere over the hill top. He's a real figure there, off screen until the player nears the end. The intro camera
    /// starts framed on him (`introCamera`, from his feet), then pulls back along the course to the player.
    /// </summary>
    public class VioletFarKing : MonoBehaviour
    {
        [Tooltip("Where the intro camera centers, relative to his feet: left of and above him, so he stands to the right " +
                 "with the end of the course in front of him.")]
        public Vector2 introCamera = new(-6f, 2f);

        public Vector2 Spot => transform.position;

        void OnDrawGizmos()
        {
            var feet = transform.position;
            Gizmos.color = new Color(0.8f, 0.4f, 1f);
            Gizmos.DrawWireCube(feet + new Vector3(0f, 1.65f), new Vector3(1.6f, 3.3f, 0f)); // him
            // The intro's first frame, at 16:9 and the level camera's size.
            Gizmos.color = new Color(0.9f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(feet + (Vector3)introCamera, new Vector3(24.9f, 14f, 0f));
        }
    }
}
