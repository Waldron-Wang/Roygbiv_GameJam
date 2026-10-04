using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A placeholder puff of smoke, steam, dust or sparks for the Red boss's tells: a square that drifts,
    /// slows, grows and fades, then removes itself. Swap for a particle system once there's art.
    /// </summary>
    public class HeatPuff : MonoBehaviour
    {
        SpriteRenderer sprite;
        Color color;
        Vector2 velocity;
        float startSize, endSize, life, age, spin;

        public static HeatPuff Spawn(Vector2 position, Vector2 velocity, float startSize, float endSize, Color color, float life, int order = 10)
        {
            var sr = FlatSprite.Create("Puff", null, position, Vector2.one * startSize, color, order);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            var puff = sr.gameObject.AddComponent<HeatPuff>();
            puff.sprite = sr;
            puff.color = color;
            puff.velocity = velocity;
            puff.startSize = startSize;
            puff.endSize = endSize;
            puff.life = life;
            puff.spin = Random.Range(-180f, 180f);
            return puff;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float t = age / life;
            if (t >= 1f) { Destroy(gameObject); return; }

            velocity *= Mathf.Exp(-2.5f * dt);
            transform.position += (Vector3)(velocity * dt);
            transform.Rotate(0f, 0f, spin * dt);
            transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, Mathf.Sqrt(t));
            sprite.color = new Color(color.r, color.g, color.b, color.a * (1f - t) * (1f - t));
        }
    }
}
