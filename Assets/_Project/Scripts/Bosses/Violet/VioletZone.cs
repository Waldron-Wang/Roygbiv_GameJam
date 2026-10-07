using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// One long-range attack of Violet's run, placed in the scene. While the player is inside its box, VioletApproach
    /// runs it (the king plays the matching gesture on his far hill, and the attack is announced at the right edge).
    ///
    /// The box is the BoxCollider2D, kept DISABLED (it's only a box to edit with the Scene view's collider handles,
    /// it never touches physics). Every position below is LOCAL to this object, so moving the zone moves its attack
    /// with it. Gizmos draw the box, the wave band, the landings and the beam.
    ///   Waves         crescent waves fill waveBand (bottom, top) and run from waveFromX to waveToX: Dash through them.
    ///   Slams         a ripple runs along each landing (x = from, y = to, z = floor height) from its right end: jump.
    ///   LowBeam       a band beam fills beamArea (warns `telegraph`, burns `fire`): only a Down Dash surf fits under.
    ///   Rain          the referenced Royal Rain hall: seals, sword barrage, shelter (runs once per life).
    ///   GatePressure  swords and lobbed orbs at the player while the referenced crystal stands.
    ///   Curtain       turns the referenced needle curtain on while the player is inside.
    /// Zones are checked left to right: where two overlap, the one whose box starts further left wins.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class VioletZone : MonoBehaviour
    {
        public enum Kind { Waves, Slams, LowBeam, Rain, GatePressure, Curtain }

        [Tooltip("Which attack runs while the player is inside the box.")]
        public Kind kind;
        [Tooltip("Name, for logs.")]
        public string label;

        [Header("Timing")]
        [Tooltip("Seconds after the player enters before the first attack.")]
        public float first = 0.8f;
        [Tooltip("Seconds between attacks (Waves, Slams, GatePressure: from one to the next; LowBeam: after a beam ends).")]
        public float interval = 1.6f;

        [Header("Waves (local)")]
        [Tooltip("Bottom (x) and top (y) of the wave band, local heights. Fill floor to ceiling so it can't be jumped or ducked.")]
        public Vector2 waveBand = new(0f, 2.98f);
        [Tooltip("Waves start here (or at the right screen edge, if that's nearer)...")]
        public float waveFromX = 30f;
        [Tooltip("...and break up here.")]
        public float waveToX = -1f;

        [Header("Slams (local)")]
        [Tooltip("Landings the ripples run along: x = from, y = to, z = floor height.")]
        public Vector3[] landings = Array.Empty<Vector3>();
        [Tooltip("Ripple height (a single jump clears it).")]
        public float rippleHeight = 0.9f;

        [Header("Low beam (local)")]
        [Tooltip("The band the beam fills. Its bottom must stay above a surfing player (~0.55) and below a standing one (1.2).")]
        public Rect beamArea = new(0f, 0.62f, 24f, 9.38f);
        [Tooltip("Warning seconds before it fires.")]
        public float telegraph = 0.9f;
        [Tooltip("Seconds it burns.")]
        public float fire = 1.2f;

        [Header("References")]
        [Tooltip("Rain: the hall it runs.")]
        public VioletRainHall rainHall;
        [Tooltip("GatePressure: pressure stops once this crystal is broken.")]
        public VioletCrystal crystal;
        [Tooltip("Curtain: turned on while the player is inside.")]
        public VioletCurtain curtain;

        /// <summary>A rain hall runs once per life.</summary>
        [NonSerialized] public bool done;

        BoxCollider2D box;

        Vector2 Origin => transform.position;

        /// <summary>The zone's box in world space.</summary>
        public Rect Area
        {
            get
            {
                if (!box) box = GetComponent<BoxCollider2D>();
                var s = transform.lossyScale;
                var size = new Vector2(box.size.x * Mathf.Abs(s.x), box.size.y * Mathf.Abs(s.y));
                var center = (Vector2)transform.TransformPoint(box.offset);
                return new Rect(center - size * 0.5f, size);
            }
        }

        public bool Contains(Vector2 p) => Area.Contains(p);

        public float WaveBottom => Origin.y + waveBand.x;
        public float WaveTop => Origin.y + waveBand.y;
        public float WaveFrom => Origin.x + waveFromX;
        public float WaveTo => Origin.x + waveToX;
        public Rect BeamArea => new(beamArea.position + Origin, beamArea.size);
        /// <summary>Landing i in world space: x = from, y = to, z = floor height.</summary>
        public Vector3 Landing(int i) => new(landings[i].x + Origin.x, landings[i].y + Origin.x, landings[i].z + Origin.y);

        void Reset()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.enabled = false;
        }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.enabled = false; // an editing handle only
        }

        void Start()
        {
            // The hint for a low beam: a thin glowing line just over the floor, "the beam stops here: fit under it".
            if (kind != Kind.LowBeam) return;
            var a = BeamArea;
            FlatSprite.Create("SafeLine", transform, new Vector2(a.center.x, a.yMin), new Vector2(a.width, 0.03f), new Color(0.76f, 0.4f, 1f, 0.35f), 4);
        }

        static readonly Color[] KindColors =
        {
            new(0.9f, 0.5f, 1f), new(1f, 0.7f, 0.3f), new(0.4f, 0.8f, 1f), new(0.7f, 0.7f, 0.9f), new(1f, 0.4f, 0.6f), new(0.5f, 0.45f, 1f),
        };

        void OnDrawGizmos()
        {
            var c = KindColors[(int)kind];
            var a = Area;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.6f);
            Gizmos.DrawWireCube(a.center, a.size);
            switch (kind)
            {
                case Kind.Waves:
                    Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
                    Gizmos.DrawCube(new Vector3((WaveFrom + WaveTo) * 0.5f, (WaveBottom + WaveTop) * 0.5f), new Vector3(Mathf.Abs(WaveFrom - WaveTo), WaveTop - WaveBottom, 0f));
                    break;
                case Kind.Slams:
                    Gizmos.color = c;
                    for (int i = 0; i < landings.Length; i++)
                    {
                        var l = Landing(i);
                        Gizmos.DrawLine(new Vector3(l.x, l.z + rippleHeight), new Vector3(l.y, l.z + rippleHeight));
                    }
                    break;
                case Kind.LowBeam:
                    var b = BeamArea;
                    Gizmos.color = new Color(c.r, c.g, c.b, 0.2f);
                    Gizmos.DrawCube(b.center, b.size);
                    break;
            }
        }
    }
}
