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
        float shakeAmplitude, shakeDuration, shakeLeft;
        Vector3 shakeOffset;

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

        /// <summary>Jitters the view for a moment. A weaker shake never cuts a stronger one short.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (duration <= 0f || amplitude < CurrentShakeStrength) return;
            shakeAmplitude = amplitude;
            shakeDuration = shakeLeft = duration;
        }

        float CurrentShakeStrength => shakeLeft > 0f ? shakeAmplitude * shakeLeft / shakeDuration : 0f;

        void LateUpdate()
        {
            transform.position -= shakeOffset; // follow from the steady position, not last frame's jitter
            Follow();

            shakeLeft = Mathf.Max(0f, shakeLeft - Time.deltaTime);
            shakeOffset = (Vector3)(Random.insideUnitCircle * CurrentShakeStrength);
            transform.position += shakeOffset;
        }

        void Follow()
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
