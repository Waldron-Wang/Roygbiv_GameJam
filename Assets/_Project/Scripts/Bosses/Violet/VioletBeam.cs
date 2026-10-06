using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A magic beam: a straight band from `origin` along `angle`, `length` long and `width` wide.
    /// It WARNS first (a thin flickering line, or the whole band glowing faintly when bandWarning is on),
    /// can TRACK the player while warning and LOCK just before it fires, then FIRES for fireTime and hurts on
    /// touch (once per firing). While firing it can SWEEP (turn) or SLIDE. Also the low sweep: a wide band
    /// that fills the lane above a thin gap over the floor (only a Down Dash surf fits under it).
    /// </summary>
    public class VioletBeam : MonoBehaviour
    {
        SpriteRenderer glow, beam, core;
        Color color;
        float age, warnTime, fireTime, lockTime, trackTurn, sweep, length, width;
        Vector2 slide;
        int damage;
        bool pierce, bandWarning, hit;

        public Vector2 Origin { get; set; }
        public float Angle { get; set; }
        /// <summary>True once it has fired and faded.</summary>
        public bool Done { get; private set; }
        public bool Firing => age >= warnTime && age < warnTime + fireTime;

        public static VioletBeam Spawn(Vector2 origin, float angle, float length, float width, float warnTime, float fireTime, int damage, Color color)
        {
            var go = new GameObject("VioletBeam");
            var b = go.AddComponent<VioletBeam>();
            b.Origin = origin;
            b.Angle = angle;
            b.length = length;
            b.width = width;
            b.warnTime = warnTime;
            b.fireTime = fireTime;
            b.damage = damage;
            b.color = color;
            b.glow = FlatSprite.Create("Glow", go.transform, origin, Vector2.one, Color.clear, 24);
            b.beam = FlatSprite.Create("Beam", go.transform, origin, Vector2.one, Color.clear, 25);
            b.core = FlatSprite.Create("Core", go.transform, origin, Vector2.one, Color.clear, 26);
            b.Draw(0f);
            return b;
        }

        /// <summary>A beam that fills a rectangle (the low sweep): it comes in from the right edge of the rect.</summary>
        public static VioletBeam Band(Rect area, float warnTime, float fireTime, int damage, Color color)
        {
            var b = Spawn(new Vector2(area.xMax, area.center.y), 180f, area.width, area.height, warnTime, fireTime, damage, color);
            b.bandWarning = true;
            return b;
        }

        /// <summary>Follows the player at up to `degreesPerSecond` while warning, then holds still for `lock` seconds before it fires.</summary>
        public VioletBeam Tracking(float degreesPerSecond, float lockSeconds)
        {
            trackTurn = degreesPerSecond;
            lockTime = lockSeconds;
            return this;
        }

        /// <summary>Turns at this many degrees per second while firing.</summary>
        public VioletBeam Sweeping(float degreesPerSecond) { sweep = degreesPerSecond; return this; }

        /// <summary>Slides its origin along this velocity while firing.</summary>
        public VioletBeam Sliding(Vector2 velocity) { slide = velocity; return this; }

        public VioletBeam Piercing() { pierce = true; return this; }

        public VioletBeam BandWarning() { bandWarning = true; return this; }

        Vector2 Dir => new(Mathf.Cos(Angle * Mathf.Deg2Rad), Mathf.Sin(Angle * Mathf.Deg2Rad));

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            if (age < warnTime - lockTime && trackTurn > 0f && VioletHits.Player)
            {
                var to = (Vector2)VioletHits.Player.transform.position - Origin;
                float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                Angle = Mathf.MoveTowardsAngle(Angle, want, trackTurn * dt);
            }
            if (Firing)
            {
                Angle += sweep * dt;
                Origin += slide * dt;
            }

            float end = warnTime + fireTime + 0.2f;
            if (age >= end) { Done = true; Destroy(gameObject); return; }
            Draw(dt);

            if (!hit && Firing && age > warnTime + 0.03f)
            {
                var dir = Dir;
                var center = Origin + dir * length * 0.5f;
                // A sliver narrower than drawn, so grazing the glow doesn't count (at most 0.1 off each edge).
                if (VioletHits.BoxTouchesPlayer(center, new Vector2(length, width - Mathf.Min(0.2f, width * 0.12f)), Angle))
                    hit = VioletHits.Hurt(damage, dir * 6f + Vector2.up * 4f, gameObject, pierce);
            }
        }

        void Draw(float dt)
        {
            var dir = Dir;
            var center = Origin + dir * length * 0.5f;
            float t = age;
            if (t < warnTime)
            {
                float k = t / Mathf.Max(0.01f, warnTime);
                bool locked = t >= warnTime - lockTime;
                float flicker = 0.5f + 0.5f * Mathf.Sin(t * (locked ? 60f : 25f));
                if (bandWarning)
                {
                    Place(glow, center, length, width, new Color(color.r, color.g, color.b, (0.08f + 0.17f * k) * (0.7f + 0.3f * flicker)));
                    Place(beam, center, length, Mathf.Max(0.05f, width * 0.04f), new Color(1f, 1f, 1f, 0.3f + 0.4f * flicker)); // a hot line through the middle
                    Place(core, Origin + dir * length * 0.5f - Perp(dir) * width * 0.5f, length, 0.06f, new Color(1f, 1f, 1f, 0.5f * flicker)); // its lower edge
                }
                else
                {
                    var c = locked ? Color.Lerp(color, Color.white, flicker) : new Color(color.r, color.g, color.b, 0.35f + 0.4f * flicker);
                    Place(glow, center, length, width * 0.6f * k, new Color(color.r, color.g, color.b, 0.08f * k));
                    Place(beam, center, length, locked ? 0.12f : 0.06f, c);
                    Place(core, center, 0f, 0f, Color.clear);
                }
                return;
            }

            float f = t - warnTime;
            float fade = f < fireTime ? 1f : 1f - (f - fireTime) / 0.2f;
            float pulse = 0.92f + 0.08f * Mathf.Sin(t * 70f);
            float grow = Mathf.Clamp01(f / 0.05f);
            Place(glow, center, length, width * 1.25f * grow * pulse, new Color(color.r, color.g, color.b, 0.35f * fade));
            Place(beam, center, length, width * grow * pulse, new Color(color.r, color.g, color.b, 0.9f * fade));
            Place(core, center, length, width * 0.45f * grow * pulse, new Color(1f, 1f, 1f, 0.9f * fade));
            if (fade > 0.5f && Random.value < 0.6f)
                VioletHits.Puff(center + dir * Random.Range(-length, length) * 0.5f + Perp(dir) * Random.Range(-width, width) * 0.5f,
                    Random.insideUnitCircle * 2f, 0.25f, 0.05f, Color.Lerp(color, Color.white, 0.5f), 0.3f, 27);
        }

        static Vector2 Perp(Vector2 d) => new(-d.y, d.x);

        void Place(SpriteRenderer sr, Vector2 center, float len, float w, Color c)
        {
            sr.transform.position = center;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Angle);
            sr.transform.localScale = new Vector3(Mathf.Max(0.001f, len), Mathf.Max(0.001f, w), 1f);
            sr.color = c;
        }
    }
}
