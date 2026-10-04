using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A plain white square made at runtime, for code-spawned placeholders (telegraph markers, the chase's
    /// edge glow). Art can replace any of them later without touching gameplay.
    /// </summary>
    public static class FlatSprite
    {
        static Sprite square;

        public static Sprite Square => square ? square : square = Sprite.Create(
            Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        /// <summary>A 1x1-unit square scaled to `size`. No collider.</summary>
        public static SpriteRenderer Create(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order = 20)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
