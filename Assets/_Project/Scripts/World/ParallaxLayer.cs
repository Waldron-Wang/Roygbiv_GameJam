using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Put on a background layer (Far / Mid / Near). The layer follows the camera by a fraction of its
    /// movement, so far layers seem to drift slowly and near layers scroll almost with the level.
    ///   follow = 1: glued to the camera (infinitely far away).   follow = 0: static, like the level itself.
    ///
    /// The layout you see in the Scene view is what the player sees when the camera sits at its
    /// authored position. Each color's art goes in as a child sprite (Far_yellow, Far_red, ...) with its own
    /// Recolorable (Desaturate ticked), so every color in every layer recolors on its own.
    /// </summary>
    [DefaultExecutionOrder(100)] // after CameraFollow, so the layer uses this frame's camera position
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("Horizontal: how much this layer moves with the camera. Far ~0.85, Mid ~0.55, Near ~0.25.")]
        [Range(0f, 1f)] [SerializeField] float follow = 0.5f;
        [Tooltip("Vertical: same idea. Usually the same as or higher than Follow, so the background stays in view on tall levels.")]
        [Range(0f, 1f)] [SerializeField] float verticalFollow = 0.5f;
        [Tooltip("Leave empty to use the main camera.")]
        [SerializeField] Transform cam;

        Vector3 home;
        Vector3 camHome;

        void Awake()
        {
            if (!cam && Camera.main) cam = Camera.main.transform;
            // Awake runs before CameraFollow.Start snaps onto the player, so these are the authored positions.
            home = transform.position;
            if (cam) camHome = cam.position;
        }

        void LateUpdate()
        {
            if (!cam) return;
            var delta = cam.position - camHome;
            transform.position = new Vector3(home.x + delta.x * follow, home.y + delta.y * verticalFollow, home.z);
        }
    }
}
