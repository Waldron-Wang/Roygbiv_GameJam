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

        const float Plate = 0.26f, BarWidth = 0.14f, Outline = 0.05f, HookHeight = 0.55f, LinkPitch = 0.2f;
        static readonly Color Iron = new(0.32f, 0.3f, 0.28f);
        static readonly Color IronDark = new(0.22f, 0.2f, 0.19f);

        State state;
        Transform cage, chain;
        ShootSwitchLook latchLook;
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

            // The cage: a gold frame around an empty middle. Its pivot is the bottom center.
            cage = new GameObject("Cage").transform;
            cage.SetParent(transform, false);
            cage.localPosition = new Vector3(0f, hangHeight, 0f);
            BuildCage(color);

            // The latch: a shootable target (its collider is the hit area; ShootSwitchLook is the look),
            // chained to the cage's hook.
            var latchPos = new Vector2(latchX, groundY + latchHeight);
            var latch = course.Make("Latch (shoot me)", latchPos - (Vector2)transform.position, new Vector2(0.6f, 0.6f), Color.white, true, transform, true);
            latch.GetComponent<SpriteRenderer>().enabled = false;
            latch.AddComponent<ShootableSwitch>().OnActivated.AddListener(Drop);
            latchLook = ShootSwitchLook.Create(transform, latchPos - (Vector2)transform.position, 0.6f, 8);

            var hook = (Vector2)transform.position + new Vector2(0f, hangHeight + size.y + HookHeight);
            chain = BuildChain(latchPos, hook);
        }

        /// <summary>Gold bars with dark edges between two plates, a cap and a hook on top, rivets at the corners.</summary>
        void BuildCage(Color gold)
        {
            var edge = new Color(gold.r * 0.4f, gold.g * 0.3f, gold.b * 0.15f);
            var lit = Color.Lerp(gold, Color.white, 0.4f);
            float w = size.x, h = size.y;
            const int bars = 5;
            for (int i = 0; i < bars; i++)
            {
                float x = Mathf.Lerp(-w * 0.5f + BarWidth, w * 0.5f - BarWidth, i / (bars - 1f));
                Bar("Bar", new Vector2(x, h * 0.5f), new Vector2(BarWidth, h - Plate), gold, edge, 12);
            }
            Bar("Band", new Vector2(0f, h * 0.55f), new Vector2(w - 0.1f, 0.1f), gold, edge, 14);
            Bar("Base", new Vector2(0f, Plate * 0.5f), new Vector2(w + 0.2f, Plate), gold, edge, 16);
            Bar("Top", new Vector2(0f, h - Plate * 0.5f), new Vector2(w + 0.2f, Plate), gold, edge, 16);
            Bar("Cap", new Vector2(0f, h + 0.1f), new Vector2(w * 0.5f, 0.2f), gold, edge, 16);
            Piece("TopShine", new Vector2(0f, h - Plate * 0.3f), new Vector2(w + 0.1f, Plate * 0.25f), lit, 18);
            IndigoShapes.Create("Hook", IndigoShapes.Ring, cage, new Vector2(0f, h + 0.4f), 0.45f, edge, 16);
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 2; row++)
                    IndigoShapes.Create("Rivet", IndigoShapes.Disc, cage, new Vector2(side * (w * 0.5f - 0.02f), row == 0 ? Plate * 0.5f : h - Plate * 0.5f), 0.12f, edge, 19);
        }

        void Bar(string name, Vector2 at, Vector2 barSize, Color fill, Color edge, int order)
        {
            Piece(name + "Edge", at, barSize + Vector2.one * (Outline * 2f), edge, order);
            Piece(name, at, barSize, fill, order + 1);
        }

        void Piece(string name, Vector2 at, Vector2 pieceSize, Color color, int order)
        {
            var sr = FlatSprite.Create(name, cage, Vector2.zero, pieceSize, color, order);
            sr.transform.localPosition = at;
        }

        /// <summary>Iron links from `from` to `to`, alternating face-on rings and edge-on bars.</summary>
        Transform BuildChain(Vector2 from, Vector2 to)
        {
            var root = new GameObject("Chain").transform;
            root.SetParent(transform, false);
            var span = to - from;
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            int count = Mathf.Max(2, Mathf.CeilToInt(span.magnitude / LinkPitch));
            for (int i = 0; i < count; i++)
            {
                bool ring = i % 2 == 0;
                var link = ring
                    ? IndigoShapes.Create("Link", IndigoShapes.Ring, root, Vector2.zero, 1f, Iron, 6)
                    : FlatSprite.Create("Link", root, Vector2.zero, Vector2.one, IronDark, 7);
                link.transform.position = Vector2.Lerp(from, to, (i + 0.5f) / count);
                link.transform.rotation = rotation;
                link.transform.localScale = ring ? new Vector3(LinkPitch * 1.5f, 0.16f, 1f) : new Vector3(LinkPitch * 1.3f, 0.05f, 1f);
            }
            return root;
        }

        void Drop()
        {
            if (state != State.Hanging) return;
            state = State.Falling;
            if (chain)
            {
                // The chain snaps: its links fall away.
                for (int i = chain.childCount - 1; i >= 0; i--)
                {
                    var link = chain.GetChild(i);
                    link.SetParent(null, true);
                    link.gameObject.AddComponent<VioletDebris>().Launch(new Vector2(Random.Range(-2f, 2f), Random.Range(0f, 3f)), 0.8f);
                }
                Destroy(chain.gameObject);
            }
            if (latchLook) latchLook.Spend();
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
