using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A fast magic needle: white-hot core, indigo rim, so it reads as different from everything else: Dash won't
    /// save you from these. Its hits PIERCE invulnerability (DamageInfo.pierceInvulnerability: Dash's i-frames don't
    /// stop it; normal post-hit i-frames still do). Curtain needles also SHOVE the player back the way they came,
    /// even through post-hit i-frames, so the curtain can't be brute-forced. Moves on scaled time; dies outside
    /// `bounds` or after its lifetime. No physics: a swept segment test against the player's body each frame.
    /// </summary>
    public class VioletNeedle : MonoBehaviour
    {
        public static readonly Color CoreColor = new(1f, 0.97f, 0.92f);
        public static readonly Color RimColor = new(0.42f, 0.32f, 1f);

        SpriteRenderer rim, core;
        Vector2 velocity, shove;
        Rect bounds;
        float length, life, shoveTime;
        int damage;
        bool pierce;

        public static VioletNeedle Spawn(Vector2 at, Vector2 velocity, float length, int damage, Rect bounds, bool pierce = true, float life = 4f)
        {
            var go = new GameObject("VioletNeedle");
            go.transform.position = at;
            var n = go.AddComponent<VioletNeedle>();
            n.velocity = velocity;
            n.length = length;
            n.damage = damage;
            n.bounds = bounds;
            n.pierce = pierce;
            n.life = life;
            n.rim = VioletShapes.Create("Rim", VioletShapes.Needle, go.transform, Vector2.zero, RimColor, 33);
            n.rim.transform.localScale = new Vector3(length * 1.1f, 1.9f, 1f);
            n.core = VioletShapes.Create("Core", VioletShapes.Needle, go.transform, Vector2.zero, CoreColor, 34);
            n.core.transform.localScale = new Vector3(length, 0.8f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            return n;
        }

        /// <summary>On a hit, throw the player along `velocity` for `seconds` (even through post-hit i-frames).</summary>
        public VioletNeedle Shoving(Vector2 velocity, float seconds)
        {
            shove = velocity;
            shoveTime = seconds;
            return this;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            Vector2 tail = (Vector2)transform.position - velocity.normalized * length * 0.5f;
            Vector2 pos = (Vector2)transform.position + velocity * dt;
            transform.position = pos;
            Vector2 tip = pos + velocity.normalized * length * 0.5f;

            if (life <= 0f || !bounds.Contains(pos)) { Destroy(gameObject); return; }

            // Swept from last frame's tail to this frame's tip, so a fast needle can't skip over the player.
            if (!VioletHits.SegmentTouchesPlayer(tail, tip, 0.07f)) return;
            VioletHits.Hurt(damage, velocity.normalized * 3f, gameObject, pierce);
            if (shoveTime > 0f) VioletHits.Shove(shove, shoveTime);
            VioletHits.Burst(tip, RimColor, 4, 3f, 0.2f);
            Destroy(gameObject);
        }
    }
}
