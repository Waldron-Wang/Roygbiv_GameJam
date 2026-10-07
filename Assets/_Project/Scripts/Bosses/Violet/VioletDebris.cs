using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A loose piece flying off something that broke (crystal shards, shattered swords, the fallen crown):
    /// thrown, spinning, falling under its own gravity, then fading. Purely visual; no collider.
    /// Optionally comes to rest on a floor height and lies there a while before fading.
    /// </summary>
    public class VioletDebris : MonoBehaviour
    {
        SpriteRenderer[] sprites;
        Color[] colors;
        Vector2 velocity;
        float spin, life, age, gravity = 22f, floorY = float.NegativeInfinity, restTime;
        bool resting;

        public VioletDebris Launch(Vector2 velocity, float life, float spin = 0f)
        {
            this.velocity = velocity;
            this.life = life;
            this.spin = spin != 0f ? spin : Random.Range(-720f, 720f);
            sprites = GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
            return this;
        }

        /// <summary>Hangs in place (a flash of light rather than a falling piece).</summary>
        public VioletDebris NoGravity()
        {
            gravity = 0f;
            return this;
        }

        /// <summary>Lands on `y` (bouncing once), lies there `seconds`, then fades.</summary>
        public VioletDebris RestOn(float y, float seconds, float gravityScale = 1f)
        {
            floorY = y;
            restTime = seconds;
            gravity = 22f * gravityScale;
            life = Mathf.Max(life, seconds + 2f);
            return this;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (!resting)
            {
                velocity.y -= gravity * dt;
                transform.position += (Vector3)(velocity * dt);
                transform.Rotate(0f, 0f, spin * dt);
                if (transform.position.y <= floorY)
                {
                    transform.position = new Vector3(transform.position.x, floorY, transform.position.z);
                    if (velocity.y < -3f) { velocity = new Vector2(velocity.x * 0.4f, -velocity.y * 0.3f); spin *= 0.4f; }
                    else { resting = true; age = Mathf.Max(age, life - restTime - 0.6f); }
                }
            }
            float fade = Mathf.Clamp01((life - age) / 0.6f);
            if (age >= life) { Destroy(gameObject); return; }
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) sprites[i].color = new Color(colors[i].r, colors[i].g, colors[i].b, colors[i].a * fade);
        }
    }
}
