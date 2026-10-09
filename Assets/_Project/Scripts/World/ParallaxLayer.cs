using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Put on a background layer (Far / Mid / Near). The layer follows the camera by a fraction of its
    /// movement, so far layers seem to drift slowly and near layers scroll almost with the level.
    ///   follow = 1: glued to the camera (infinitely far away).   follow = 0: static, like the level itself.
    ///
    /// Each color's art is a child sprite (Far_yellow, Far_red, ...) with its own Recolorable (Desaturate ticked),
    /// so every color in every layer recolors on its own. For seamless art, set the children to Draw Mode = Tiled
    /// (a few tiles wide) and tick Loop: the layer then wraps by one tile and never runs out.
    ///
    /// The layout you see in the Scene view is what the player sees when the camera sits at its authored position.
    /// </summary>
    [DefaultExecutionOrder(100)] // after CameraFollow, so the layer uses this frame's camera position
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("Horizontal: how much this layer moves with the camera. Far ~0.9, Mid ~0.75, Near ~0.5.")]
        [Range(0f, 1f)] [SerializeField] float follow = 0.5f;
        [Tooltip("Vertical: same idea. Keep it high (0.9–1) so the art's top and bottom edges stay off screen.")]
        [Range(0f, 1f)] [SerializeField] float verticalFollow = 1f;
        [Tooltip("Wrap horizontally by one tile of the children's art (children use Draw Mode = Tiled).")]
        [SerializeField] bool loop = true;
        [Tooltip("The camera size the art was laid out for. The layer scales with the camera's zoom so it always " +
                 "fills the same part of the screen. 0 = never scale.")]
        [SerializeField] float designOrthoSize = 7f;
        [Tooltip("Leave empty to use the main camera.")]
        [SerializeField] Camera cam;

        Vector3 camHome;
        Vector2 offset;     // authored position relative to the camera
        Vector3 baseScale;
        float tileWidth;    // world units at base scale
        float artHeight;    // world units at base scale
        bool ready;

        /// <summary>0 = normal parallax. 1 = the whole picture, uncropped and centered on the camera (FinalShowcase).</summary>
        public float FullView { get; set; }

        void Awake() => Init();

        void Init()
        {
            if (!cam) cam = Camera.main;
            if (!cam) return;
            // Awake runs before CameraFollow.Start snaps onto the player, so these are the authored positions.
            camHome = cam.transform.position;
            offset = transform.position - camHome;
            baseScale = transform.localScale;
            var art = GetComponentInChildren<SpriteRenderer>();
            if (art && art.sprite)
            {
                tileWidth = art.sprite.bounds.size.x * art.transform.lossyScale.x;
                artHeight = art.sprite.bounds.size.y * art.transform.lossyScale.y;
            }
            ready = true;
        }

        void LateUpdate()
        {
            if (!ready) { Init(); if (!ready) return; }

            float scale = designOrthoSize > 0f && cam.orthographic ? cam.orthographicSize / designOrthoSize : 1f;
            var camPos = cam.transform.position;
            float driftX = (camPos.x - camHome.x) * (1f - follow);
            float driftY = (camPos.y - camHome.y) * (1f - verticalFollow);
            if (loop && tileWidth > 0f)
            {
                float w = tileWidth * scale;
                driftX = Mathf.Repeat(driftX + w * 0.5f, w) - w * 0.5f;
            }

            var pos = new Vector2(camPos.x + offset.x * scale - driftX, camPos.y + offset.y * scale - driftY);

            if (FullView > 0f && artHeight > 0f && cam.orthographic)
            {
                float fit = cam.orthographicSize * 2f / artHeight; // the art's full height fills the view
                pos = Vector2.Lerp(pos, camPos, FullView);
                scale = Mathf.Lerp(scale, fit, FullView);
            }

            transform.localScale = baseScale * scale;
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }
    }
}
