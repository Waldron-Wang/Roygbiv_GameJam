using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Round placeholder sprites made at runtime for the Indigo boss (its eye, halo, sigil and decoys),
    /// the round cousins of FlatSprite.Square. Each is 1 unit across. Art can replace any of them later.
    /// </summary>
    public static class IndigoShapes
    {
        const int Size = 128;

        static Sprite disc, ring, thinRing;
        static Font font;
        static Material lineMaterial;

        /// <summary>Plain vertex-colored material for LineRenderers (the default sprite shader, which every build has).</summary>
        public static Material LineMaterial => lineMaterial ? lineMaterial : lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "IndigoLine" };

        public static Sprite Disc => disc ? disc : disc = Make("Disc", 0f);
        public static Sprite Ring => ring ? ring : ring = Make("Ring", 0.78f);
        public static Sprite ThinRing => thinRing ? thinRing : thinRing = Make("ThinRing", 0.93f);
        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static SpriteRenderer Create(string name, Sprite sprite, Transform parent, Vector2 localPosition, float size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>World-space text with a drop shadow, centered on its position.</summary>
        public static TextMesh Label(string name, Transform parent, Vector2 localPosition, string text, float height, Color color, int order)
        {
            var shadow = MakeText(name + "Shadow", parent, localPosition + new Vector2(0.06f, -0.06f) * height, text, height, new Color(0f, 0f, 0f, 0.75f), order);
            var label = MakeText(name, parent, localPosition, text, height, color, order + 1);
            shadow.transform.SetParent(label.transform, true);
            return label;
        }

        static TextMesh MakeText(string name, Transform parent, Vector2 localPosition, string text, float height, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f; // one line is about fontSize * characterSize / 10 units tall
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        /// <param name="inner">Hole radius as a fraction of the outer radius (0 = solid disc).</param>
        static Sprite Make(string name, float inner)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float r = Size * 0.5f, edge = 1.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                float a = Mathf.Clamp01((r - d) / edge);
                if (inner > 0f) a *= Mathf.Clamp01((d - r * inner) / edge);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
