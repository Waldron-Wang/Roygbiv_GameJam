using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A shockwave ring that expands from a point on the floor (Double Earthsplitter). Where it crosses the
    /// player's column it sweeps from the floor upward, so jumping can't clear it: dash through it, or be out of
    /// its reach. Hurts once.
    /// </summary>
    public class VioletRing : MonoBehaviour
    {
        SpriteRenderer ring, inner;
        Color color;
        Vector2 center;
        float radius, maxRadius, speed, thickness;
        int damage;
        bool hit;

        public static VioletRing Spawn(Vector2 center, float maxRadius, float speed, float thickness, int damage, Color color)
        {
            var go = new GameObject("VioletRing");
            go.transform.position = center;
            var r = go.AddComponent<VioletRing>();
            r.center = center;
            r.maxRadius = maxRadius;
            r.speed = speed;
            r.thickness = thickness;
            r.damage = damage;
            r.color = color;
            r.radius = 0.3f;
            r.ring = IndigoShapes.Create("Ring", IndigoShapes.ThinRing, go.transform, Vector2.zero, 1f, color, 26);
            r.inner = IndigoShapes.Create("Inner", IndigoShapes.ThinRing, go.transform, Vector2.zero, 1f, Color.white, 27);
            return r;
        }

        void Update()
        {
            radius += speed * Time.deltaTime;
            if (radius >= maxRadius) { Destroy(gameObject); return; }
            float k = radius / maxRadius;
            float alpha = 1f - k * k;
            ring.transform.localScale = Vector3.one * radius * 2f;
            inner.transform.localScale = Vector3.one * (radius * 2f - thickness * 0.5f);
            ring.color = new Color(color.r, color.g, color.b, 0.95f * alpha);
            inner.color = new Color(1f, 1f, 1f, 0.6f * alpha);
            if (!hit && VioletHits.RingTouchesPlayer(center, radius, thickness * 0.8f))
                hit = VioletHits.Hurt(damage, new Vector2(Mathf.Sign(VioletHits.Player.transform.position.x - center.x) * 7f, 5f), gameObject);
        }
    }
}
