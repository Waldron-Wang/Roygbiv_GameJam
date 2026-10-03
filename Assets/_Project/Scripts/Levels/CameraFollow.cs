using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Minimal 2D follow camera. Set autoScrollSpeed > 0 for the Orange chase (the camera moves on its
    /// own and the player dies if left behind). Swap for Cinemachine later if you want — nothing depends on it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Vector2 offset = new(0f, 1.5f);
        [SerializeField] float smoothTime = 0.15f;
        public float autoScrollSpeed;
        [Tooltip("Auto-scroll: how far (world units) the player starts left of the screen center.")]
        public float autoScrollLead = 4f;

        Camera cam;
        Vector3 velocity;

        void Awake() => cam = GetComponent<Camera>();

        void Start()
        {
            // Snap onto the player so they start on screen whatever the Game view's aspect ratio is.
            var player = PlayerController.Instance;
            if (!player) return;
            var p = player.transform.position;
            float x = autoScrollSpeed > 0f ? p.x + autoScrollLead : p.x + offset.x;
            transform.position = new Vector3(x, p.y + offset.y, transform.position.z);
        }

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            var pos = transform.position;

            if (autoScrollSpeed > 0f)
            {
                pos.x += autoScrollSpeed * Time.deltaTime;
                if (player && player.transform.position.x < LeftEdge(pos.x) - 1f)
                    player.Health.Kill();
                if (player) pos.y = Mathf.SmoothDamp(pos.y, player.transform.position.y + offset.y, ref velocity.y, smoothTime);
                transform.position = pos;
                return;
            }

            if (!player) return;
            var target = new Vector3(player.transform.position.x + offset.x, player.transform.position.y + offset.y, pos.z);
            transform.position = Vector3.SmoothDamp(pos, target, ref velocity, smoothTime);
        }

        float LeftEdge(float centerX) => centerX - cam.orthographicSize * cam.aspect;
    }
}
