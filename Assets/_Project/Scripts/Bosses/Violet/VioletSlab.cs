using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ROYAL RAIN's shelter: a heavy stone slab hanging on a chain from an anchor high above, out of reach of any
    /// jump or swing. Break the destructible anchor with a player Light Shot and the chain snaps: the slab drops
    /// onto the supports and makes a roof to hide under while the spectral swords rain down (they shatter on it).
    /// It isn't solid while it hangs (the spectral swords pass through it), so standing under it does nothing until
    /// it has landed; the right support breaks on impact to open an exit, while the left support stays solid.
    /// </summary>
    public class VioletSlab : MonoBehaviour
    {
        Rigidbody2D slab;
        Collider2D[] slabColliders;
        Transform[] chain;
        SpriteRenderer anchorGlow;
        float landY;
        GameObject exitSupport;

        /// <summary>Rain starts below the hanging structure, so its solid anchor beam cannot act as a roof.
        /// Keep the spawn plane fixed even after the slab drops.</summary>
        public float RainSpawnY { get; private set; }

        public bool Dropped { get; private set; }

        /// <param name="slabBlock">The slab: a solid block. It gets a kinematic body here.</param>
        /// <param name="landCenterY">The slab's center height once it rests on the pillars.</param>
        public void Setup(GameObject slabBlock, float landCenterY, Transform[] chainLinks, SpriteRenderer glow, GameObject supportToBreak)
        {
            slab = slabBlock.AddComponent<Rigidbody2D>();
            slab.bodyType = RigidbodyType2D.Kinematic;
            slab.interpolation = RigidbodyInterpolation2D.Interpolate;
            landY = landCenterY;
            slabColliders = slabBlock.GetComponents<Collider2D>();
            float bottom = slabBlock.transform.position.y;
            foreach (var c in slabColliders) bottom = Mathf.Min(bottom, c.bounds.min.y);
            // A default spectral sword extends 0.6 units above its origin (including its visual).
            RainSpawnY = bottom - 0.9f;
            foreach (var c in slabColliders) c.enabled = false;
            chain = chainLinks;
            anchorGlow = glow;
            exitSupport = supportToBreak;
        }

        public void Drop()
        {
            if (Dropped) return;
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
            foreach (var c in slabColliders) if (c) c.enabled = true;
            // Open the right-hand exit before the next physics step; retain the left structural support.
            if (exitSupport)
            {
                foreach (var c in exitSupport.GetComponentsInChildren<Collider2D>()) c.enabled = false;
                VioletHits.Burst(exitSupport.transform.position, new Color(0.55f, 0.45f, 0.6f), 12, 5f);
                Destroy(exitSupport);
            }
            VioletHits.ShakeCamera(0.4f, 0.35f);
            var bottom = new Vector2(slab.position.x, landY);
            for (int i = 0; i < 14; i++)
                VioletHits.Puff(bottom + new Vector2(Random.Range(-2.5f, 2.5f), -0.3f), new Vector2(Random.Range(-4f, 4f), Random.Range(0.5f, 2.5f)),
                    0.4f, 1f, new Color(0.55f, 0.45f, 0.6f, 0.6f), 0.7f, 9);
        }
    }
}
