using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A strip of ground fire for the Red boss. First a thin pulsing warning strip, then flames that
    /// hurt on touch (Hazard) and flicker, then they sink away. With no burn time it is only the warning
    /// strip, which makes a landing marker for a lobbed fireball.
    /// The Green boss reuses it for its thorns, with its own colors.
    /// Placeholder look (a stretched square); art can replace the sprite without touching the timing.
    /// </summary>
    public class FirePatch : MonoBehaviour
    {
        const float RiseTime = 0.06f, SinkTime = 0.15f, WarnHeight = 0.12f;
        static readonly Color HotTip = new(1f, 0.85f, 0.3f);

        SpriteRenderer sprite;
        Collider2D hurtZone;
        Color color, tipColor;
        float x, groundY, width, height, warnTime, burnTime, age, seed;

        /// <param name="groundY">Top of the floor; the flames grow up from here.</param>
        /// <param name="burnTime">0 = warning strip only (a marker).</param>
        /// <param name="tip">What the flames flicker toward. Default: a hot yellow.</param>
        public static FirePatch Spawn(float x, float groundY, float width, float height, float warnTime, float burnTime, Color color, Color? tip = null)
        {
            var sr = FlatSprite.Create("FirePatch", null, new Vector2(x, groundY), new Vector2(width, WarnHeight), color, 4);
            var go = sr.gameObject;

            // Kinematic so moving the trigger every frame is cheap; the player's dynamic body does the detecting.
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.85f); // a bit smaller than the flames, so near misses stay misses
            box.offset = new Vector2(0f, -0.075f);
            box.enabled = false;
            go.AddComponent<Hazard>();

            var patch = go.AddComponent<FirePatch>();
            patch.sprite = sr;
            patch.hurtZone = box;
            patch.color = color;
            patch.tipColor = tip ?? HotTip;
            patch.x = x;
            patch.groundY = groundY;
            patch.width = width;
            patch.height = height;
            patch.warnTime = warnTime;
            patch.burnTime = burnTime;
            patch.seed = Random.value * 100f;
            patch.Update();
            return patch;
        }

        void Update()
        {
            age += Time.deltaTime;

            if (age < warnTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(age * 25f);
                SetShape(WarnHeight, new Color(color.r, color.g, color.b, 0.3f + 0.4f * pulse));
                return;
            }

            float t = age - warnTime;
            if (t >= burnTime + SinkTime || burnTime <= 0f) { Destroy(gameObject); return; }

            hurtZone.enabled = t < burnTime;
            float rise = Mathf.Clamp01(t / RiseTime);
            float sink = t > burnTime ? 1f - (t - burnTime) / SinkTime : 1f;
            float flicker = 1f + 0.35f * (Mathf.PerlinNoise(Time.time * 14f, seed) - 0.5f);
            var c = Color.Lerp(color, tipColor, Mathf.PerlinNoise(seed, Time.time * 9f));
            SetShape(height * rise * sink * flicker, c);
        }

        void SetShape(float h, Color c)
        {
            h = Mathf.Max(h, 0.01f);
            transform.localScale = new Vector3(width, h, 1f);
            transform.position = new Vector3(x, groundY + h * 0.5f, 0f);
            sprite.color = c;
        }
    }
}
