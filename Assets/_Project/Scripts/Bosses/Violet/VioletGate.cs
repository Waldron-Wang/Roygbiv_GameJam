using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A slab of solid stone that rises out of the floor to seal a passage, and sinks back to open it
    /// (Royal Rain's seals, the arena door). Kinematic, so rising it lifts anyone standing on it instead of
    /// trapping them inside. Put it on a block that already has a solid collider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class VioletGate : MonoBehaviour
    {
        Rigidbody2D body;
        float openY, closedY;
        Coroutine move;

        public bool IsClosed { get; private set; }

        /// <param name="openY">Center height when open (sunk into the floor).</param>
        /// <param name="closedY">Center height when closed.</param>
        public void Setup(float openY, float closedY, bool startClosed)
        {
            body = GetComponent<Rigidbody2D>();
            if (!body) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            this.openY = openY;
            this.closedY = closedY;
            IsClosed = startClosed;
            var p = transform.position;
            transform.position = new Vector3(p.x, startClosed ? closedY : openY, p.z);
            body.position = transform.position;
        }

        public void Close(float seconds) => MoveTo(true, seconds);
        public void Open(float seconds) => MoveTo(false, seconds);

        void MoveTo(bool closed, float seconds)
        {
            if (!body) return;
            IsClosed = closed;
            if (move != null) StopCoroutine(move);
            move = StartCoroutine(Slide(closed ? closedY : openY, seconds));
        }

        IEnumerator Slide(float targetY, float seconds)
        {
            float startY = body.position.y;
            for (float t = 0f; t < 1f;)
            {
                yield return new WaitForFixedUpdate();
                t = seconds > 0f ? Mathf.Min(1f, t + Time.fixedDeltaTime / seconds) : 1f;
                body.MovePosition(new Vector2(body.position.x, Mathf.Lerp(startY, targetY, Mathf.SmoothStep(0f, 1f, t))));
                if (Random.value < 0.3f)
                    VioletHits.Puff(new Vector2(body.position.x + Random.Range(-0.6f, 0.6f), Mathf.Min(startY, targetY) + (closedY - openY) * 0.5f),
                        new Vector2(Random.Range(-2f, 2f), Random.Range(0.5f, 2f)), 0.3f, 0.7f, new Color(0.5f, 0.4f, 0.6f, 0.5f), 0.5f, 8);
            }
            if (Mathf.Abs(targetY - closedY) < 0.01f) VioletHits.ShakeCamera(0.15f, 0.2f);
            move = null;
        }
    }
}
