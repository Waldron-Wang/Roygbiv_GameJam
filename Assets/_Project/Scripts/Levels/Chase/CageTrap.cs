using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE: the way to stop the boss. A cage hangs over the track; its latch hangs further back, roughly
    /// where the player will be when the boss passes under the cage. Hit the latch (Light Shot, or jump +
    /// melee) and the cage drops. If the boss is under it when it lands, the boss is trapped (stunned);
    /// otherwise it crashes onto the track as a low pile the player has to jump.
    ///
    /// Shot travel + drop time mean the player has to LEAD the shot, and the boss's wobble moves the
    /// sweet spot. ChaseCourse builds these; nothing here needs scene setup.
    /// </summary>
    public class CageTrap : MonoBehaviour
    {
        enum State { Hanging, Falling, Holding, Settled, Broken }

        [SerializeField] Vector2 size = new(2.8f, 2.6f);
        [SerializeField] float dropTime = 0.25f;
        [Tooltip("Traps the boss if its center is within this distance of the cage's center on landing.")]
        [SerializeField] float captureRadius = 1.6f;
        [Tooltip("Missed: the cage crumples into a pile this size, a low hurdle.")]
        [SerializeField] Vector2 pileSize = new(2f, 0.8f);

        State state;
        Transform cage, rope;
        SpriteRenderer latchSprite;
        ChaseCourse course;

        public float X => transform.position.x;
        public float LatchX { get; private set; }
        public bool IsArmed => state == State.Hanging;
        public bool IsHoldingBoss => state == State.Holding;

        public static CageTrap Build(ChaseCourse course, float cageX, float latchX, float groundY, float hangHeight, float latchHeight, Color color)
        {
            var root = new GameObject("CageTrap").transform;
            root.SetParent(course.transform, false);
            root.position = new Vector3(cageX, groundY, 0f);
            var trap = root.gameObject.AddComponent<CageTrap>();
            trap.Init(course, latchX, groundY, hangHeight, latchHeight, color);
            return trap;
        }

        void Init(ChaseCourse owner, float latchX, float groundY, float hangHeight, float latchHeight, Color color)
        {
            course = owner;
            LatchX = latchX;

            // The cage: a frame of bars around an empty middle. Its pivot is the bottom center.
            cage = new GameObject("Cage").transform;
            cage.SetParent(transform, false);
            cage.localPosition = new Vector3(0f, hangHeight, 0f);
            const float bar = 0.18f;
            course.Make("Top", new Vector2(0f, size.y - bar * 0.5f), new Vector2(size.x, bar), color, false, cage, true);
            course.Make("Bottom", new Vector2(0f, bar * 0.5f), new Vector2(size.x, bar), color, false, cage, true);
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Lerp(-size.x * 0.5f + bar * 0.5f, size.x * 0.5f - bar * 0.5f, i / 3f);
                course.Make("Bar", new Vector2(x, size.y * 0.5f), new Vector2(bar, size.y), color, false, cage, true);
            }

            // The latch: a shootable target, tied to the cage top by a rope.
            var latchPos = new Vector2(latchX, groundY + latchHeight);
            var latch = course.Make("Latch (shoot me)", latchPos - (Vector2)transform.position, new Vector2(0.6f, 0.6f), Color.white, true, transform, true);
            latchSprite = latch.GetComponent<SpriteRenderer>();
            latch.AddComponent<ShootableSwitch>().OnActivated.AddListener(Drop);

            var cageTop = (Vector2)transform.position + new Vector2(0f, hangHeight + size.y);
            var span = cageTop - latchPos;
            rope = FlatSprite.Create("Rope", transform, latchPos + span * 0.5f, new Vector2(span.magnitude, 0.06f), new Color(1f, 1f, 1f, 0.6f), 5).transform;
            rope.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
        }

        void Drop()
        {
            if (state != State.Hanging) return;
            state = State.Falling;
            if (rope) Destroy(rope.gameObject);
            if (latchSprite) latchSprite.color = Color.gray;
            StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            float from = cage.localPosition.y;
            for (float t = 0f; t < dropTime; t += Time.deltaTime)
            {
                float k = t / dropTime;
                cage.localPosition = new Vector3(0f, from * (1f - k * k), 0f); // accelerating fall
                yield return null;
            }
            cage.localPosition = Vector3.zero;

            var boss = LevelController.Current ? LevelController.Current.Boss as OrangeBoss : null;
            if (boss && boss.TryTrap(this, captureRadius))
            {
                state = State.Holding;
                cage.localPosition = new Vector3(boss.transform.position.x - X, 0f, 0f); // center on it
                Shake(0.25f);
            }
            else Settle();
        }

        /// <summary>Missed: crumple into a low pile that blocks the track.</summary>
        void Settle()
        {
            state = State.Settled;
            cage.localScale = new Vector3(pileSize.x / size.x, pileSize.y / size.y, 1f);
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.sharedMaterial = course.SolidMaterial;
            col.size = pileSize;
            col.offset = new Vector2(0f, pileSize.y * 0.5f);
            Shake(0.15f);
        }

        /// <summary>The boss broke out or was caught: the cage is done. The spent latch stays until it scrolls away.</summary>
        public void Shatter()
        {
            if (state == State.Broken) return;
            state = State.Broken;
            Shake(0.2f);
            if (cage) Destroy(cage.gameObject);
        }

        static void Shake(float amount)
        {
            if (Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(amount, 0.2f);
        }
    }
}
