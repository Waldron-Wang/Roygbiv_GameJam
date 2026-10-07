using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The Green boss's lash: a thorny vine whipping out along the floor from its root at `startX`. A dark body
    /// with a lit top edge, thorns along both sides (raked back against the whip), a few leaves, and a barbed tip.
    /// The thorns and leaves are laid out along the full length up front and show up as the tip passes them.
    /// Purely visual: the boss does the hit test against the same rectangle (reach x thickness).
    /// Drawn from runtime shapes so art can replace it later.
    /// </summary>
    public class GreenVine : MonoBehaviour
    {
        const float ThornPitch = 0.55f, LeafPitch = 2.3f;

        readonly List<(GameObject go, float at)> details = new();
        SpriteRenderer body, shine;
        Transform tip;
        float thickness;
        int dir;

        public static GreenVine Create(float startX, int dir, float length, float y, float thickness, Color color, int order)
        {
            var go = new GameObject("Lash");
            go.transform.position = new Vector2(startX, y);
            var vine = go.AddComponent<GreenVine>();
            vine.dir = dir;
            vine.thickness = thickness;

            var dark = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, color.a);
            var light = Color.Lerp(color, new Color(0.55f, 0.9f, 0.35f), 0.55f);
            vine.body = FlatSprite.Create("Body", go.transform, go.transform.position, Vector2.one, color, order);
            vine.shine = FlatSprite.Create("Shine", go.transform, go.transform.position, Vector2.one, light, order + 1);

            int index = 0;
            for (float at = ThornPitch * 0.5f; at < length; at += ThornPitch, index++)
            {
                bool up = index % 2 == 0;
                var thorn = VioletShapes.Create("Thorn", Thorn(), go.transform, new Vector2(dir * at, (up ? 0.5f : -0.5f) * thickness), dark, order - 1);
                thorn.transform.localScale = new Vector3(0.24f, 0.32f, 1f);
                thorn.transform.localRotation = Quaternion.Euler(0f, 0f, up ? dir * 25f : 180f - dir * 25f);
                vine.details.Add((thorn.gameObject, at));
            }
            index = 0;
            for (float at = LeafPitch * 0.6f; at < length; at += LeafPitch, index++)
            {
                bool up = index % 2 == 0;
                var leaf = VioletShapes.Create("Leaf", Leaf(), go.transform, new Vector2(dir * at, (up ? 0.35f : -0.35f) * thickness), light, order + 2);
                leaf.transform.localScale = new Vector3(dir * 0.55f, up ? 0.3f : -0.3f, 1f);
                leaf.transform.localRotation = Quaternion.Euler(0f, 0f, dir * (up ? 20f : -20f));
                vine.details.Add((leaf.gameObject, at));
            }

            var head = VioletShapes.Create("Tip", Barb(), go.transform, Vector2.zero, dark, order + 2);
            head.transform.localScale = new Vector3(dir * thickness * 1.5f, thickness * 1.5f, 1f);
            vine.tip = head.transform;
            vine.Set(0f);
            return vine;
        }

        /// <param name="reach">How far the tip is from the root.</param>
        public void Set(float reach)
        {
            float r = Mathf.Max(0.01f, reach);
            body.transform.localPosition = new Vector2(dir * r * 0.5f, 0f);
            body.transform.localScale = new Vector3(r, thickness, 1f);
            shine.transform.localPosition = new Vector2(dir * r * 0.5f, thickness * 0.28f);
            shine.transform.localScale = new Vector3(r, thickness * 0.2f, 1f);
            tip.localPosition = new Vector2(dir * r, 0f);
            tip.gameObject.SetActive(reach > 0.05f);
            foreach (var (go, at) in details) go.SetActive(at < reach - 0.2f);
        }

        // Shapes (pivot at the base): a curved thorn, a leaf pointing +x, and an arrowhead barb pointing +x.
        static Sprite Thorn() => VioletShapes.Polygon("VineThorn", Vector2.zero, 0.2f,
            new(-0.5f, 0f), new(-0.2f, 0.45f), new(0.05f, 1f), new(0.12f, 0.5f), new(0.5f, 0f));

        static Sprite Leaf() => VioletShapes.Polygon("VineLeaf", Vector2.zero, 0.25f,
            new(0f, 0f), new(0.25f, 0.4f), new(0.6f, 0.55f), new(1f, 0.25f), new(0.6f, -0.05f), new(0.25f, -0.08f));

        static Sprite Barb() => VioletShapes.Polygon("VineBarb", new Vector2(0f, 0f), 0.2f,
            new(-0.3f, 0.25f), new(0.15f, 0.3f), new(-0.05f, 0.5f), new(0.7f, 0f), new(-0.05f, -0.5f), new(0.15f, -0.3f), new(-0.3f, -0.25f));
    }
}
