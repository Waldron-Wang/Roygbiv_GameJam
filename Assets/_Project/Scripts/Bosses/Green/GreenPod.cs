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
    /// Look: a bramble stalk with two leaves, topped by a closed bud of petals around the glowing core; the petals
    /// part as it ripens, showing more of the core. Drawn from runtime shapes; art can replace them without
    /// touching the timing.
    /// </summary>
    public class GreenPod : MonoBehaviour, IDamageable
    {
        const float FlightTime = 0.55f, SproutTime = 0.35f, SeedSize = 0.3f, StartSize = 0.4f, RipeSize = 0.95f;
        static readonly Color HuskColor = new(0.2f, 0.5f, 0.15f);
        static readonly Color RipeColor = new(0.75f, 0.7f, 0.15f);
        static readonly Color StalkColor = new(0.15f, 0.35f, 0.1f);
        static readonly Color LeafColor = new(0.25f, 0.6f, 0.18f);
        // Petals (angle, width, height): two tall ones behind the core, two shorter ones cupping it from the
        // sides, leaving it showing in the middle (its color says which ability is inside).
        static readonly Vector3[] BackPetals = { new(-12f, 1f, 1f), new(12f, 1f, 1f) };
        static readonly Vector3[] FrontPetals = { new(-30f, 0.75f, 0.8f), new(30f, 0.75f, 0.8f) };

        SpriteRenderer stalk, core, glow;
        SpriteRenderer[] leaves, back, front;
        Transform bud;
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
            pod.leaves = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++) pod.leaves[i] = VioletShapes.Create("Leaf", LeafShape(), go.transform, Vector2.zero, LeafColor, 5);
            pod.bud = new GameObject("Bud").transform;
            pod.bud.SetParent(go.transform, false);
            pod.glow = IndigoShapes.Create("Glow", IndigoShapes.Disc, pod.bud, Vector2.zero, 1.6f, Color.clear, 4);
            pod.back = Petals(pod.bud, BackPetals, 6);
            pod.front = Petals(pod.bud, FrontPetals, 8);
            pod.bud.gameObject.SetActive(false);
            pod.core = VioletShapes.Create("Core", SeedShape(), null, from, coreColor, 7);
            pod.core.transform.localScale = Vector3.one * SeedSize;
            pod.core.transform.SetParent(go.transform, true);

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
            if (core.sprite != IndigoShapes.Disc) core.sprite = IndigoShapes.Disc; // the seed has sprouted

            // Throbs faster and harder as it ripens; flickers sickly yellow in its last quarter.
            float rate = Mathf.Lerp(2f, 12f, r * r);
            float throb = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((age - FlightTime) * rate * Mathf.PI), 3f) * Mathf.Lerp(0.04f, 0.14f, r);
            float size = Mathf.Lerp(StartSize, RipeSize, Mathf.Sqrt(r)) * grown * (1f + throb + 0.25f * jolt);
            var shake = UnityEngine.Random.insideUnitCircle * (0.08f * jolt + (r > 0.75f ? 0.04f * (r - 0.75f) / 0.25f : 0f));
            var c = (Vector2)transform.position + shake;

            stalk.transform.localScale = new Vector3(0.14f, Mathf.Max(0.01f, stalkHeight * grown), 1f);
            stalk.transform.position = spot + Vector2.up * (stalkHeight * grown * 0.5f);
            for (int i = 0; i < leaves.Length; i++)
            {
                float side = i == 0 ? -1f : 1f;
                leaves[i].transform.position = spot + Vector2.up * (stalkHeight * grown * (i == 0 ? 0.35f : 0.65f)) + Vector2.right * side * 0.05f;
                leaves[i].transform.localScale = new Vector3(side * 0.42f * grown, 0.24f * grown, 1f);
                leaves[i].transform.rotation = Quaternion.Euler(0f, 0f, side * 25f);
            }

            // The bud: petals part as it ripens; the husk sickens toward yellow and flickers near the end.
            var sway = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 2f) * 6f);
            bud.gameObject.SetActive(true);
            bud.position = c;
            bud.rotation = sway;
            bud.localScale = Vector3.one * size;
            bool flicker = r > 0.75f && Mathf.Repeat(age * Mathf.Lerp(4f, 12f, (r - 0.75f) / 0.25f), 1f) < 0.5f;
            var huskNow = Color.Lerp(HuskColor, RipeColor, Mathf.Max(0f, (r - 0.5f) * 2f)) * (flicker ? 1.3f : 1f);
            huskNow.a = 1f;
            SetPetals(back, BackPetals, r * 10f, new Color(huskNow.r * 0.75f, huskNow.g * 0.75f, huskNow.b * 0.75f));
            SetPetals(front, FrontPetals, r * 20f, huskNow);
            float shine = 0.3f + 0.4f * Mathf.Clamp01(throb / 0.14f) + 0.2f * r;
            glow.color = new Color(coreColor.r, coreColor.g, coreColor.b, shine);

            core.transform.position = c + (Vector2)(sway * Vector2.up) * size * 0.08f;
            core.transform.rotation = sway;
            core.transform.localScale = Vector3.one * size * 0.5f;

            if (r >= 1f && !done)
            {
                done = true;
                ripened?.Invoke(this);
                Destroy(gameObject);
            }
        }

        static SpriteRenderer[] Petals(Transform bud, Vector3[] layout, int order)
        {
            var petals = new SpriteRenderer[layout.Length];
            for (int i = 0; i < layout.Length; i++)
            {
                petals[i] = VioletShapes.Create("Petal", PetalShape(), bud, new Vector2(0f, -0.5f), HuskColor, order);
                petals[i].transform.localScale = new Vector3(layout[i].y, layout[i].z, 1f);
            }
            return petals;
        }

        /// <param name="open">Extra degrees each petal leans out from the middle.</param>
        static void SetPetals(SpriteRenderer[] petals, Vector3[] layout, float open, Color color)
        {
            for (int i = 0; i < petals.Length; i++)
            {
                float angle = layout[i].x;
                petals[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle + Mathf.Sign(angle) * open);
                petals[i].color = color;
            }
        }

        // Shapes (pivot at the base): a pointed petal 1 tall, a seed, a leaf pointing +x.
        static Sprite PetalShape() => VioletShapes.Polygon("PodPetal", Vector2.zero, 0.3f,
            new(0f, 0f), new(0.24f, 0.15f), new(0.3f, 0.45f), new(0.2f, 0.75f), new(0f, 1f), new(-0.2f, 0.75f), new(-0.3f, 0.45f), new(-0.24f, 0.15f));

        static Sprite SeedShape() => VioletShapes.Polygon("PodSeed", new Vector2(0f, 0.5f), 0.2f,
            new(0f, 0f), new(0.35f, 0.25f), new(0.45f, 0.55f), new(0.25f, 0.85f), new(0f, 1f), new(-0.25f, 0.85f), new(-0.45f, 0.55f), new(-0.35f, 0.25f));

        static Sprite LeafShape() => VioletShapes.Polygon("PodLeaf", Vector2.zero, 0.25f,
            new(0f, 0f), new(0.25f, 0.4f), new(0.6f, 0.55f), new(1f, 0.25f), new(0.6f, -0.05f), new(0.25f, -0.08f));

        static void Puff(Vector2 at, Vector2 velocity, float size, float endSize, Color color, float life) =>
            HeatPuff.Spawn(at + UnityEngine.Random.insideUnitCircle * 0.15f, velocity, size, endSize, color, life, 12);
    }
}
