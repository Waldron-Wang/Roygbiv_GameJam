using System;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A stolen ability growing on a bramble stalk, for the Green boss. It flies in as a seed, sprouts, then
    /// swells toward ripe: its core glows in the stolen ability's color, and it throbs faster and sickens
    /// toward yellow as it gets close. Hit it `hits` times to break it; if it ripens first, it bursts.
    /// Shots can be set to bounce off it, so it has to be broken up close.
    /// The boss decides what all of that means (callbacks); the pod only does the growing and the looks.
    ///
    /// Team.Enemy, so the player's attacks break it and the boss's own shots pass through it. Its collider is
    /// solid for hit detection, but the player walks through it.
    /// Placeholder look (squares); art can replace the sprites without touching the timing.
    /// </summary>
    public class GreenPod : MonoBehaviour, IDamageable
    {
        const float FlightTime = 0.55f, SproutTime = 0.35f, SeedSize = 0.3f, StartSize = 0.4f, RipeSize = 0.95f;
        static readonly Color HuskColor = new(0.2f, 0.5f, 0.15f);
        static readonly Color RipeColor = new(0.75f, 0.7f, 0.15f);
        static readonly Color StalkColor = new(0.15f, 0.35f, 0.1f);

        SpriteRenderer stalk, husk, core;
        Action<GreenPod> hurt, broken, ripened;
        Color coreColor;
        Vector2 from, spot, center;
        float stalkHeight, ripenTime, age, jolt;
        int hits;
        bool done, takesShots;

        public AbilityId Ability { get; private set; }
        /// <summary>The foot of the stalk.</summary>
        public Vector2 Spot => spot;
        public bool Sprouted => age >= FlightTime;
        /// <summary>0 when it sprouts, 1 when it bursts.</summary>
        public float Ripeness => Mathf.Clamp01((age - FlightTime) / ripenTime);

        public Team Team => Team.Enemy;

        /// <param name="from">Where the seed flies in from (the player it was taken from, or the pod before it).</param>
        /// <param name="spot">The foot of the stalk, on a floor or platform top.</param>
        /// <param name="hurt">A hit that didn't break it yet.</param>
        public static GreenPod Spawn(AbilityId ability, Color coreColor, Vector2 from, Vector2 spot, float stalkHeight,
            int hits, bool takesShots, float ripenTime, Action<GreenPod> hurt, Action<GreenPod> broken, Action<GreenPod> ripened)
        {
            var center = spot + Vector2.up * (stalkHeight + RipeSize * 0.5f);
            var go = new GameObject($"Pod_{ability}");
            go.transform.position = center;

            var pod = go.AddComponent<GreenPod>();
            pod.Ability = ability;
            pod.coreColor = coreColor;
            pod.from = from;
            pod.spot = spot;
            pod.center = center;
            pod.stalkHeight = stalkHeight;
            pod.hits = hits;
            pod.takesShots = takesShots;
            pod.hurt = hurt;
            pod.ripenTime = Mathf.Max(0.1f, ripenTime);
            pod.broken = broken;
            pod.ripened = ripened;

            pod.stalk = FlatSprite.Create("Stalk", go.transform, spot, new Vector2(0.14f, 0.01f), StalkColor, 5);
            pod.husk = FlatSprite.Create("Husk", go.transform, center, Vector2.one * StartSize, HuskColor, 6);
            pod.core = FlatSprite.Create("Core", null, from, Vector2.one * SeedSize, coreColor, 7);
            pod.core.transform.SetParent(go.transform, true);
            pod.husk.enabled = false;

            // Solid so hitboxes and shots find it, a little bigger than it looks so it's easy to hit.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one * RipeSize * 1.1f;
            if (PlayerController.Instance)
                foreach (var c in PlayerController.Instance.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) Physics2D.IgnoreCollision(box, c);

            pod.Update();
            return pod;
        }

        public bool TakeDamage(in DamageInfo info)
        {
            if (done || !Sprouted) return false;
            if (!takesShots && info.source && info.source.GetComponent<Projectile>())
            {
                // Glances off the husk.
                jolt = 0.3f;
                for (int i = 0; i < 3; i++)
                    Puff(center, UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 4f), 0.12f, 0.03f, Color.white, 0.25f);
                return true; // the shot is spent
            }

            jolt = 1f;
            for (int i = 0; i < 4; i++)
                Puff(center, UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 4f), 0.2f, 0.1f, HuskColor, 0.35f);
            if (--hits > 0) { hurt?.Invoke(this); return true; }

            done = true;
            for (int i = 0; i < 10; i++)
                Puff(center, UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(3f, 7f), 0.3f, 0.1f, coreColor, 0.5f);
            broken?.Invoke(this);
            Destroy(gameObject);
            return true;
        }

        void Update()
        {
            age += Time.deltaTime;
            jolt = Mathf.MoveTowards(jolt, 0f, Time.deltaTime / 0.2f);

            if (age < FlightTime)
            {
                // A seed arcing over from where it was taken.
                float f = age / FlightTime;
                var p = Vector2.Lerp(from, center, f) + Vector2.up * (Mathf.Sin(f * Mathf.PI) * 2.5f);
                core.transform.position = p;
                core.transform.rotation = Quaternion.Euler(0f, 0f, age * 720f);
                return;
            }

            float grown = Mathf.Clamp01((age - FlightTime) / SproutTime);
            float r = Ripeness;

            // Throbs faster and harder as it ripens; flickers sickly yellow in its last quarter.
            float rate = Mathf.Lerp(2f, 12f, r * r);
            float throb = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((age - FlightTime) * rate * Mathf.PI), 3f) * Mathf.Lerp(0.04f, 0.14f, r);
            float size = Mathf.Lerp(StartSize, RipeSize, Mathf.Sqrt(r)) * grown * (1f + throb + 0.25f * jolt);
            var shake = UnityEngine.Random.insideUnitCircle * (0.08f * jolt + (r > 0.75f ? 0.04f * (r - 0.75f) / 0.25f : 0f));
            var c = (Vector2)transform.position + shake;

            stalk.transform.localScale = new Vector3(0.14f, Mathf.Max(0.01f, stalkHeight * grown), 1f);
            stalk.transform.position = spot + Vector2.up * (stalkHeight * grown * 0.5f);

            husk.enabled = true;
            husk.transform.position = c;
            husk.transform.localScale = Vector3.one * size;
            husk.transform.rotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(age * 2f) * 6f);
            bool flicker = r > 0.75f && Mathf.Repeat(age * Mathf.Lerp(4f, 12f, (r - 0.75f) / 0.25f), 1f) < 0.5f;
            husk.color = Color.Lerp(HuskColor, RipeColor, Mathf.Max(0f, (r - 0.5f) * 2f)) * (flicker ? 1.3f : 1f);

            core.transform.position = c;
            core.transform.rotation = husk.transform.rotation;
            core.transform.localScale = Vector3.one * size * 0.5f;

            if (r >= 1f && !done)
            {
                done = true;
                ripened?.Invoke(this);
                Destroy(gameObject);
            }
        }

        static void Puff(Vector2 at, Vector2 velocity, float size, float endSize, Color color, float life) =>
            HeatPuff.Spawn(at + UnityEngine.Random.insideUnitCircle * 0.15f, velocity, size, endSize, color, life, 12);
    }
}
