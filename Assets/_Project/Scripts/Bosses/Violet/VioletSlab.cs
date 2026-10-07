using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ROYAL RAIN's shelter: a heavy stone slab hanging on a chain from an anchor high above, out of reach of any
    /// jump or swing. Break the destructible anchor with a player Light Shot and the chain snaps: the slab drops
    /// onto the supports and makes a roof to hide under while the spectral swords rain down (they shatter on it).
    /// Royal Rain swords pass through it until it lands. Both support pillars remain intact.
    /// </summary>
    public class VioletSlab : MonoBehaviour
    {
        [Tooltip("How far the slab drops when the anchor breaks: down onto the pillars (painted in the tilemap).")]
        [SerializeField] float dropDistance = 4.4f;
        [Tooltip("The chain's links: they fall away when it snaps.")]
        [SerializeField] Transform[] chainLinks = { };
        [Tooltip("The anchor it hangs from: a glow marks it as the thing to shoot.")]
        [SerializeField] Transform anchor;
        [Tooltip("Set by the bake: this slab sets itself up when the level starts (off = made and set up from code).")]
        [SerializeField, HideInInspector] bool placedInScene;

        Rigidbody2D slab;
        Collider2D[] slabColliders;
        Transform[] chain;
        SpriteRenderer anchorGlow;
        float landY;
        VioletRainPassThrough rainPassThrough;
        public bool Destroyed { get; private set; }

        public bool Dropped { get; private set; }

        void Awake()
        {
            if (!placedInScene) return;
            SpriteRenderer glow = null;
            if (anchor)
            {
                glow = IndigoShapes.Create("AnchorGlow", IndigoShapes.Ring, transform.parent, anchor.position - (transform.parent ? transform.parent.position : Vector3.zero),
                    1.4f, new Color(1f, 0.9f, 0.6f, 0.8f), 8);
                glow.gameObject.AddComponent<VioletPulse>();
            }
            Setup(gameObject, transform.position.y - dropDistance, chainLinks, glow);
        }

        /// <param name="slabBlock">The slab: a solid block. It gets a kinematic body here.</param>
        /// <param name="landCenterY">The slab's center height once it rests on the pillars.</param>
        public void Setup(GameObject slabBlock, float landCenterY, Transform[] chainLinks, SpriteRenderer glow)
        {
            slab = slabBlock.GetComponent<Rigidbody2D>();
            if (!slab) slab = slabBlock.AddComponent<Rigidbody2D>();
            slab.bodyType = RigidbodyType2D.Kinematic;
            slab.interpolation = RigidbodyInterpolation2D.Interpolate;
            landY = landCenterY;
            slabColliders = slabBlock.GetComponents<Collider2D>();
            rainPassThrough = slabBlock.GetComponent<VioletRainPassThrough>();
            if (!rainPassThrough) rainPassThrough = slabBlock.AddComponent<VioletRainPassThrough>();
            chain = chainLinks;
            anchorGlow = glow;
        }

        public void Drop()
        {
            if (Dropped || Destroyed || !slab) return;
            Dropped = true;
            StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            if (anchorGlow)
            {
                VioletHits.Burst(anchorGlow.transform.position, VioletShapes.Glow, 12, 6f, 0.4f);
                anchorGlow.enabled = false;
            }
            foreach (var link in chain)
            {
                if (!link) continue;
                link.SetParent(null, true);
                link.gameObject.AddComponent<VioletDebris>().Launch(new Vector2(Random.Range(-2f, 2f), Random.Range(0f, 3f)), 1.4f);
            }

            float vy = 0f;
            yield return new WaitForSeconds(0.12f); // the chain gives, then it drops
            while (slab.position.y > landY)
            {
                yield return new WaitForFixedUpdate();
                vy -= 30f * Time.fixedDeltaTime;
                slab.MovePosition(new Vector2(slab.position.x, Mathf.Max(landY, slab.position.y + vy * Time.fixedDeltaTime)));
            }
            rainPassThrough.BlockRain();
            VioletHits.ShakeCamera(0.4f, 0.35f);
            var bottom = new Vector2(slab.position.x, landY);
            for (int i = 0; i < 14; i++)
                VioletHits.Puff(bottom + new Vector2(Random.Range(-2.5f, 2.5f), -0.3f), new Vector2(Random.Range(-4f, 4f), Random.Range(0.5f, 2.5f)),
                    0.4f, 1f, new Color(0.55f, 0.45f, 0.6f, 0.6f), 0.7f, 9);
        }

        public void OnSlabDestroyed()
        {
            if (Destroyed) return;
            Destroyed = true;
            StopAllCoroutines();
            foreach (var c in slabColliders) if (c) c.enabled = false;
            if (anchorGlow) anchorGlow.enabled = false;
            foreach (var link in chain)
            {
                if (!link || link.GetComponent<VioletDebris>()) continue;
                link.SetParent(null, true);
                link.gameObject.AddComponent<VioletDebris>().Launch(Random.insideUnitCircle * 3f, 1.4f);
            }
        }
    }
}
