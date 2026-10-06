using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A placeholder droplet / spark: a small round blob thrown with a velocity, pulled down by gravity, shrinking
    /// and fading, then gone (Down Dash's spray and landing splash). Purely visual, no collider. Scaled time.
    /// </summary>
    public class Droplet : MonoBehaviour
    {
        SpriteRenderer sprite;
        Color color;
        Vector2 velocity;
        float size, life, age, gravity;

        public static Droplet Spawn(Vector2 at, Vector2 velocity, float size, Color color, float life, float gravity = 18f, int order = 12)
        {
            var sr = IndigoShapes.Create("Droplet", IndigoShapes.Disc, null, at, size, color, order);
            var d = sr.gameObject.AddComponent<Droplet>();
            d.sprite = sr;
            d.color = color;
            d.velocity = velocity;
            d.size = size;
            d.life = Mathf.Max(0.05f, life);
            d.gravity = gravity;
            return d;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float t = age / life;
            if (t >= 1f) { Destroy(gameObject); return; }
            velocity.y -= gravity * dt;
            transform.position += (Vector3)(velocity * dt);
            transform.localScale = Vector3.one * size * (1f - 0.6f * t);
            sprite.color = new Color(color.r, color.g, color.b, color.a * (1f - t * t));
        }
    }
}
