using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// How Violet's attacks find and hurt the player: overlap tests against the player's body (its non-trigger
    /// collider), damage with the right flags, and the curtain's shove. Shapes are plain math or one physics
    /// query, so a wave, a beam, a ring or a needle all hit exactly where they're drawn.
    /// </summary>
    public static class VioletHits
    {
        static readonly List<Collider2D> Results = new();
        static ContactFilter2D solids;
        static bool filterReady;
        static PlayerController bodyOwner;
        static Collider2D body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            bodyOwner = null;
            body = null;
        }

        static ContactFilter2D Solids
        {
            get
            {
                if (filterReady) return solids;
                solids = ContactFilter2D.noFilter;
                solids.useTriggers = false;
                filterReady = true;
                return solids;
            }
        }

        public static PlayerController Player => PlayerController.Instance;

        /// <summary>The player's body box (its non-trigger collider on the root). False if there's no player.</summary>
        public static bool PlayerBox(out Bounds bounds)
        {
            bounds = default;
            var pc = Player;
            if (!pc || pc.Health.IsDead) return false;
            if (bodyOwner != pc || !body)
            {
                // Cached: a needle storm asks this hundreds of times a frame.
                bodyOwner = pc;
                body = null;
                foreach (var c in pc.GetComponents<Collider2D>())
                    if (!c.isTrigger) { body = c; break; }
            }
            if (!body || !body.enabled) return false;
            bounds = body.bounds;
            return true;
        }

        /// <summary>Axis-aligned rectangle (world) against the player's body.</summary>
        public static bool RectTouchesPlayer(Rect r)
        {
            if (!PlayerBox(out var b)) return false;
            return b.min.x < r.xMax && b.max.x > r.xMin && b.min.y < r.yMax && b.max.y > r.yMin;
        }

        /// <summary>A rotated box (center, full size, degrees) against the player's body: one physics query.</summary>
        public static bool BoxTouchesPlayer(Vector2 center, Vector2 size, float angle)
        {
            var pc = Player;
            if (!pc || pc.Health.IsDead) return false;
            Physics2D.OverlapBox(center, size, angle, Solids, Results);
            foreach (var c in Results)
                if (c && c.attachedRigidbody && c.attachedRigidbody.transform == pc.transform) return true;
            return false;
        }

        /// <summary>A thick segment (a needle, radius = half its width) against the player's body.</summary>
        public static bool SegmentTouchesPlayer(Vector2 a, Vector2 b, float radius)
        {
            if (!PlayerBox(out var box)) return false;
            Vector2 min = (Vector2)box.min - Vector2.one * radius, max = (Vector2)box.max + Vector2.one * radius;
            // Liang-Barsky clip of the segment against the grown box.
            float t0 = 0f, t1 = 1f;
            var d = b - a;
            return Clip(-d.x, a.x - min.x, ref t0, ref t1) && Clip(d.x, max.x - a.x, ref t0, ref t1)
                && Clip(-d.y, a.y - min.y, ref t0, ref t1) && Clip(d.y, max.y - a.y, ref t0, ref t1);
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(p, 0f)) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        /// <summary>A ring (circle outline `thickness` wide) against the player's body.</summary>
        public static bool RingTouchesPlayer(Vector2 center, float radius, float thickness)
        {
            if (!PlayerBox(out var b)) return false;
            var nearest = new Vector2(Mathf.Clamp(center.x, b.min.x, b.max.x), Mathf.Clamp(center.y, b.min.y, b.max.y));
            float near = Vector2.Distance(center, nearest);
            float fx = Mathf.Max(Mathf.Abs(center.x - b.min.x), Mathf.Abs(center.x - b.max.x));
            float fy = Mathf.Max(Mathf.Abs(center.y - b.min.y), Mathf.Abs(center.y - b.max.y));
            float far = Mathf.Sqrt(fx * fx + fy * fy);
            return near <= radius + thickness * 0.5f && far >= radius - thickness * 0.5f;
        }

        /// <summary>Hurts the player. pierce = lands through Dash's i-frames (post-hit i-frames still apply). True if it landed.</summary>
        public static bool Hurt(int damage, Vector2 knockback, GameObject source, bool pierce = false)
        {
            var pc = Player;
            if (!pc) return false;
            var info = new DamageInfo(damage, Team.Enemy, knockback, source) { pierceInvulnerability = pierce };
            return pc.Health.TakeDamage(info);
        }

        /// <summary>Throws the player along `velocity` for `seconds`, overriding their movement (and any Dash / Down Dash).</summary>
        public static void Shove(Vector2 velocity, float seconds)
        {
            var pc = Player;
            if (!pc || pc.Health.IsDead) return;
            if (!pc.TryGetComponent<VioletShove>(out var shove)) shove = pc.gameObject.AddComponent<VioletShove>();
            shove.Push(velocity, seconds);
        }

        public static void ShakeCamera(float amount, float time)
        {
            if (amount > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(amount, time);
        }

        public static void Puff(Vector2 at, Vector2 velocity, float size, float endSize, Color color, float life, int order = 12) =>
            HeatPuff.Spawn(at + Random.insideUnitCircle * 0.1f, velocity, size, endSize, color, life, order);

        public static void Burst(Vector2 at, Color color, int count = 10, float speed = 6f, float size = 0.35f)
        {
            for (int i = 0; i < count; i++)
                Puff(at, Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed), size, size * 0.2f, color, 0.45f);
        }
    }
}
