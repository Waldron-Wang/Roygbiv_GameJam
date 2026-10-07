using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A patch of thorns on the ground that hurts on touch (Green's garden: jump them, or dash over the ditch).
    /// Its origin is the middle of its base; it fills `size` upward from there. Needs a BoxCollider2D and a Hazard
    /// (the bake adds them); the collider is sized here, a bit inside the spikes so near misses stay misses.
    /// Drawn from runtime shapes (a row of spikes with light tips) so art can replace it later. Keeps its colors
    /// while the world is gray, so it always reads as danger.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(Hazard))]
    public class ThornBed : MonoBehaviour
    {
        [SerializeField] Vector2 size = new(3f, 0.7f);
        [SerializeField] Color thornColor = new(0.2f, 0.55f, 0.15f);
        [SerializeField] Color tipColor = new(0.6f, 0.9f, 0.3f);
        [SerializeField] int order = 5;

        void Awake()
        {
            var box = GetComponent<BoxCollider2D>();
            box.size = new Vector2(size.x * 0.9f, size.y * 0.75f);
            box.offset = new Vector2(0f, size.y * 0.375f);

            var spike = VioletShapes.Polygon("BedSpike", Vector2.zero, 0.3f,
                new(-0.5f, 0f), new(-0.3f, 0.28f), new(-0.14f, 0.58f), new(0f, 1f), new(0.1f, 0.62f), new(0.25f, 0.3f), new(0.5f, 0f));
            int n = Mathf.Max(2, Mathf.RoundToInt(size.x / 0.3f));
            float w = size.x / n;
            var root = new GameObject("Thorns").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < n; i++)
            {
                float x = -size.x * 0.5f + w * (i + 0.5f);
                float h = size.y * Random.Range(0.7f, 1f);
                var body = VioletShapes.Create("Thorn", spike, root, new Vector2(x, 0f), thornColor, order);
                body.transform.localScale = new Vector3(w * 1.35f, h, 1f);
                var tip = VioletShapes.Create("Tip", spike, root, new Vector2(x, h * 0.55f), tipColor, order + 1);
                tip.transform.localScale = new Vector3(w * 0.6f, h * 0.45f, 1f);
            }
            var roots = FlatSprite.Create("Roots", root, Vector2.zero, new Vector2(size.x, 0.12f), new Color(thornColor.r * 0.45f, thornColor.g * 0.4f, thornColor.b * 0.3f), order + 2);
            roots.transform.localPosition = new Vector2(0f, 0.04f);
        }
    }
}
