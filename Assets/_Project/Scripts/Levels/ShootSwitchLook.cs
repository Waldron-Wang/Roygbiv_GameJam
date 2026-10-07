using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The one look for "shoot this": a round white target on a dark rim with a gold bullseye, inside a pulsing
    /// gold ring. Orange's cage latch and Violet's shelter anchor both wear it, so the player learns it once.
    /// Spend() shows it's been used: a gold burst, the ring goes out and the face dims.
    /// Drawn from runtime shapes so art can replace it later; purely visual (the collider stays on the switch).
    /// </summary>
    public class ShootSwitchLook : MonoBehaviour
    {
        public static readonly Color RingColor = new(1f, 0.9f, 0.6f, 0.8f);
        static readonly Color Rim = new(0.16f, 0.14f, 0.18f);
        static readonly Color Gold = new(1f, 0.72f, 0.2f);
        static readonly Color SpentFace = new(0.55f, 0.55f, 0.55f);
        static readonly Color SpentMark = new(0.35f, 0.35f, 0.35f);

        SpriteRenderer face, bullseye, dot;
        bool spent;

        /// <summary>The pulsing ring (VioletPulse animates it).</summary>
        public SpriteRenderer Ring { get; private set; }

        /// <param name="size">Diameter of the white face in world units (the ring is about 2.3x that).</param>
        public static ShootSwitchLook Create(Transform parent, Vector2 localPosition, float size, int order)
        {
            var go = new GameObject("SwitchLook");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var look = go.AddComponent<ShootSwitchLook>();
            IndigoShapes.Create("Rim", IndigoShapes.Disc, go.transform, Vector2.zero, size * 1.25f, Rim, order);
            look.face = IndigoShapes.Create("Face", IndigoShapes.Disc, go.transform, Vector2.zero, size, Color.white, order + 1);
            look.bullseye = IndigoShapes.Create("Bullseye", IndigoShapes.ThinRing, go.transform, Vector2.zero, size * 0.62f, Gold, order + 2);
            look.dot = IndigoShapes.Create("Dot", IndigoShapes.Disc, go.transform, Vector2.zero, size * 0.24f, Gold, order + 2);
            look.Ring = IndigoShapes.Create("Ring", IndigoShapes.Ring, go.transform, Vector2.zero, size * 2.3f, RingColor, order + 3);
            look.Ring.gameObject.AddComponent<VioletPulse>();
            return look;
        }

        public void Spend()
        {
            if (spent) return;
            spent = true;
            VioletHits.Burst(transform.position, RingColor, 12, 6f, 0.4f);
            Ring.enabled = false;
            face.color = SpentFace;
            bullseye.color = SpentMark;
            dot.color = SpentMark;
        }
    }
}
