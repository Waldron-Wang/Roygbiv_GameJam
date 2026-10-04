using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Minimal 2D follow camera. Set autoScrollSpeed > 0 for the Orange chase (the camera moves on its
    /// own and the player dies if left behind; ChaseDirector drives the speed there).
    /// Swap for Cinemachine later if you want — only the Orange chase reads ScrollX / KillLineX.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Vector2 offset = new(0f, 1.5f);
        [SerializeField] float smoothTime = 0.15f;
        public float autoScrollSpeed;
        [Tooltip("Auto-scroll: how far (world units) the player starts left of the screen center.")]
        public float autoScrollLead = 4f;
        [Tooltip("Auto-scroll: how far (world units) past the left screen edge the player may fall before dying.")]
        public float leftBehindGrace = 1f;

        Camera cam;
        Vector3 velocity;
        float shakeAmplitude, shakeDuration, shakeLeft;
        Vector3 shakeOffset;

        /// <summary>Steady camera X (no shake). In auto-scroll this is the scroll position.</summary>
        public float ScrollX { get; private set; }
        public float HalfWidth => cam.orthographicSize * cam.aspect;
        /// <summary>Auto-scroll: the player dies once they are left of this.</summary>
        public float KillLineX => ScrollX - HalfWidth - leftBehindGrace;

        void Awake()
        {
            cam = GetComponent<Camera>();
            ScrollX = transform.position.x;
        }

        void Start()
        {
            // Snap onto the player so they start on screen whatever the Game view's aspect ratio is.
            var player = PlayerController.Instance;
            if (!player) return;
            var p = player.transform.position;
            float x = autoScrollSpeed > 0f ? p.x + autoScrollLead : p.x + offset.x;
            transform.position = new Vector3(x, p.y + offset.y, transform.position.z);
            ScrollX = x;
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
            ScrollX = transform.position.x;

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
                if (player && player.transform.position.x < pos.x - HalfWidth - leftBehindGrace)
                    player.Health.Kill();
                if (player) pos.y = Mathf.SmoothDamp(pos.y, player.transform.position.y + offset.y, ref velocity.y, smoothTime);
                transform.position = pos;
                return;
            }

            if (!player) return;
            var target = new Vector3(player.transform.position.x + offset.x, player.transform.position.y + offset.y, pos.z);
            transform.position = Vector3.SmoothDamp(pos, target, ref velocity, smoothTime);
        }
    }
}
