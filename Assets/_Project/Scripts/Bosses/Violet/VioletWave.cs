using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A crescent sword-slash wave that sweeps sideways at a fixed band of height [bottom, top]. It hurts on touch
    /// anywhere in that band. One that fills a corridor floor to ceiling can't be jumped or ducked: only Dash's
    /// i-frames get through. A low one (near the floor) is jumped; a high one is surfed under with Down Dash.
    /// Floor ripples from his slams are low waves too. A hit counts once; one blocked by i-frames doesn't count,
    /// so it can still land if the player is inside it when the i-frames run out.
    /// </summary>
    public class VioletWave : MonoBehaviour
    {
        SpriteRenderer body, core;
        Color color;
        float x, bottom, top, thickness, speed, endX, age, fade = -1f;
        int dir, damage;
        bool hit;

        /// <param name="dir">-1 = travels left (toward the player in the approach).</param>
        /// <param name="endX">Where it breaks up.</param>
        public static VioletWave Spawn(float x, float bottom, float top, int dir, float speed, float endX, float thickness, int damage, Color color)
        {
            var go = new GameObject("VioletWave");
            var w = go.AddComponent<VioletWave>();
            w.x = x;
            w.bottom = bottom;
            w.top = top;
            w.dir = dir >= 0 ? 1 : -1;
            w.speed = speed;
            w.endX = endX;
            w.thickness = thickness;
            w.damage = damage;
            w.color = color;
            w.body = VioletShapes.Create("Body", VioletShapes.Crescent, go.transform, Vector2.zero, color, 26);
            w.core = VioletShapes.Create("Core", VioletShapes.Crescent, go.transform, new Vector2(0.06f * w.dir, 0f), Color.white, 27);
            w.Update();
            return w;
        }

        public float X => x;

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            x += dir * speed * dt;
            if (fade < 0f && (endX - x) * dir <= 0f) fade = 0f;
            if (fade >= 0f)
            {
                fade += dt / 0.2f;
                if (fade >= 1f) { Destroy(gameObject); return; }
            }

            float h = top - bottom;
            float alpha = Mathf.Clamp01(age / 0.08f) * (fade >= 0f ? 1f - fade : 1f);
            transform.position = new Vector3(x, (bottom + top) * 0.5f, 0f);
            // The crescent bulges toward where it's going; it's drawn a bit wider than its hit band so near misses stay misses.
            body.transform.localScale = new Vector3(-dir * thickness * 2.2f, h * 1.04f, 1f);
            core.transform.localScale = new Vector3(-dir * thickness * 1.4f, h * 0.9f, 1f);
            float flicker = 0.85f + 0.15f * Mathf.Sin(age * 50f);
            body.color = new Color(color.r, color.g, color.b, 0.9f * alpha);
            core.color = new Color(1f, 1f, 1f, 0.8f * alpha * flicker);

            if (Random.value < 0.5f)
                VioletHits.Puff(new Vector2(x - dir * thickness * 0.3f, Random.Range(bottom, top)), new Vector2(-dir * Random.Range(1f, 3f), 0f), 0.2f, 0.05f,
                    new Color(color.r, color.g, color.b, 0.6f * alpha), 0.3f, 25);

            if (!hit && fade < 0f && age > 0.05f
                && VioletHits.RectTouchesPlayer(new Rect(x - thickness * 0.5f, bottom, thickness, h)))
                hit = VioletHits.Hurt(damage, new Vector2(dir * 7f, 4f), gameObject);
        }
    }
}
