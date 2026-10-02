using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Minimal 2D follow camera. Set autoScrollSpeed > 0 for the Orange chase (the camera moves on its
    /// own and the player dies if left behind). Swap for Cinemachine later if you want — nothing depends on it.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Vector2 offset = new(0f, 1.5f);
        [SerializeField] float smoothTime = 0.15f;
        public float autoScrollSpeed;

        Vector3 velocity;

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            var pos = transform.position;

            if (autoScrollSpeed > 0f)
            {
                pos.x += autoScrollSpeed * Time.deltaTime;
                if (player && player.transform.position.x < pos.x - Camera.main.orthographicSize * Camera.main.aspect - 1f)
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
