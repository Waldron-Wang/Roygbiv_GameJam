using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A pool of lava that hurts on touch (Red's forge path: dash over the ditch, short-hop the puddles under the hot
    /// beams). Its origin is the middle of its base; it fills `size` upward from there. Needs a BoxCollider2D and a
    /// Hazard (the bake adds them); the collider is sized here, a bit inside the pool so near misses stay misses.
    /// Drawn from runtime shapes (a glowing pool with a white-hot skin, crusted edges, popping bubbles and rising
    /// embers) so art can replace it later. Keeps its colors while the world is gray, so it always reads as danger.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(Hazard))]
    public class LavaPool : MonoBehaviour
    {
        [SerializeField] Vector2 size = new(2f, 0.4f);
        [SerializeField] Color lava = new(1f, 0.35f, 0.05f);
        [SerializeField] Color hot = new(1f, 0.85f, 0.35f);
        [SerializeField] Color crust = new(0.25f, 0.08f, 0.05f);
        [SerializeField] int order = 5;

        SpriteRenderer glow, skin;
        SpriteRenderer[] bubbles;
        float[] bubbleAge, bubbleLife;
        float emberTimer, seed;

        void Awake()
        {
            var box = GetComponent<BoxCollider2D>();
            box.size = new Vector2(size.x * 0.9f, size.y * 0.8f);
            box.offset = new Vector2(0f, size.y * 0.4f);
            seed = Random.value * 100f;

            var root = new GameObject("Lava").transform;
            root.SetParent(transform, false);
            glow = Part(root, "Glow", new Vector2(0f, size.y * 0.5f), new Vector2(size.x + 0.4f, size.y + 0.8f), new Color(lava.r, lava.g, lava.b, 0.25f), order - 1);
            Part(root, "Pool", new Vector2(0f, size.y * 0.5f), size, lava, order);
            skin = Part(root, "Skin", new Vector2(0f, size.y - 0.05f), new Vector2(size.x, 0.1f), hot, order + 1);
            Part(root, "CrustL", new Vector2(-size.x * 0.5f, size.y * 0.5f), new Vector2(0.18f, size.y + 0.12f), crust, order + 2);
            Part(root, "CrustR", new Vector2(size.x * 0.5f, size.y * 0.5f), new Vector2(0.18f, size.y + 0.12f), crust, order + 2);

            int n = Mathf.Max(2, Mathf.RoundToInt(size.x * 1.5f));
            bubbles = new SpriteRenderer[n];
            bubbleAge = new float[n];
            bubbleLife = new float[n];
            for (int i = 0; i < n; i++)
            {
                bubbles[i] = IndigoShapes.Create("Bubble", IndigoShapes.Disc, root, Vector2.zero, 0.1f, hot, order + 1);
                Respawn(i);
                bubbleAge[i] = Random.Range(0f, bubbleLife[i]);
            }
        }

        static SpriteRenderer Part(Transform root, string name, Vector2 position, Vector2 partSize, Color color, int order)
        {
            var sr = FlatSprite.Create(name, root, Vector2.zero, partSize, color, order);
            sr.transform.localPosition = position;
            return sr;
        }

        void Respawn(int i)
        {
            bubbleAge[i] = 0f;
            bubbleLife[i] = Random.Range(0.6f, 1.4f);
            bubbles[i].transform.localPosition = new Vector2(Random.Range(-0.4f, 0.4f) * size.x, size.y - 0.06f);
        }

        void Update()
        {
            float t = Time.time;
            glow.color = new Color(lava.r, lava.g, lava.b, 0.18f + 0.12f * Mathf.Sin(t * 3f + seed));
            skin.color = Color.Lerp(hot, lava, 0.4f * Mathf.PerlinNoise(t * 4f, seed));

            // Bubbles swell on the surface, then pop.
            for (int i = 0; i < bubbles.Length; i++)
            {
                bubbleAge[i] += Time.deltaTime;
                float k = bubbleAge[i] / bubbleLife[i];
                if (k >= 1f) { Respawn(i); k = 0f; }
                bubbles[i].transform.localScale = Vector3.one * Mathf.Lerp(0.04f, 0.2f, k);
                bubbles[i].color = new Color(hot.r, hot.g, hot.b, k < 0.85f ? 1f : (1f - k) / 0.15f);
            }

            emberTimer -= Time.deltaTime;
            if (emberTimer > 0f) return;
            emberTimer = Random.Range(0.12f, 0.3f);
            var at = (Vector2)transform.position + new Vector2(Random.Range(-0.45f, 0.45f) * size.x, size.y);
            HeatPuff.Spawn(at, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1f, 2.2f)), 0.1f, 0.03f,
                Color.Lerp(hot, lava, Random.value), Random.Range(0.4f, 0.8f), order + 2);
        }
    }
}
