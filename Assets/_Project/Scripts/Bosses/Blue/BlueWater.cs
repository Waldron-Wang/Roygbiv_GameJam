using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Roygbiv
{
    /// <summary>
    /// The Blue boss's rising tears: water that floods the shaft from below while the player climbs.
    ///
    /// It's there from the moment the level loads, just under the player's feet at the bottom of the screen, and
    /// starts rising a few seconds into the fight. It rises at `riseSpeed`; with `rubberBand` on it also catches up
    /// when it's far below the player (so it stays a threat after a good run) and slows right under their feet (so it
    /// never feels cheap). Falling in kills the player. It stops just under the summit floor, so the summit can't be
    /// flooded. Made and owned by BlueBoss; its Settings live on the boss and can be tuned live in Play mode.
    ///
    /// The look is procedural: a mesh whose surface rolls with two layered waves and the ripples of anything that
    /// falls in (the player, tears), a pale foam line on the crest, a lighter band under it fading to deep water,
    /// glints sliding along the surface and bubbles drifting up. It's drawn in front of the level and the player,
    /// half see-through, so a submerged player shows through it.
    /// </summary>
    public class BlueWater : MonoBehaviour
    {
        [Serializable]
        public class Settings
        {
            [Header("Speed")]
            [Tooltip("How fast it rises, units per second. The main knob.")]
            public float riseSpeed = 0.55f;
            [Tooltip("Seconds after the fight starts before it begins to rise.")]
            public float startDelay = 4f;
            [Tooltip("On: faster when far below the player, slower right under their feet (the two pairs below). Off: always riseSpeed.")]
            public bool rubberBand = true;
            [Tooltip("When it's more than this far under the player's feet it catches up...")]
            public float catchUpGap = 10f;
            [Tooltip("...at this speed (it's off screen then).")]
            public float catchUpSpeed = 2.5f;
            [Tooltip("When it's this close under the player's feet it slows...")]
            public float nearGap = 2.5f;
            [Tooltip("...to this speed.")]
            public float nearSpeed = 0.3f;

            [Header("Place")]
            [Tooltip("Where its surface sits at the start, under the player's starting height (in view at the bottom of the screen).")]
            public float startBelow = 1.2f;
            [Tooltip("It stops this far under the summit floor.")]
            public float stopBelowSummit = 1.5f;
            [Tooltip("How far it reaches past the shaft's walls on each side, so its ends are never on screen.")]
            public float overhang = 30f;

            [Header("Look")]
            public Color foam = new(0.85f, 0.93f, 1f, 0.9f);
            public Color surface = new(0.35f, 0.6f, 1f, 0.62f);
            public Color deep = new(0.05f, 0.12f, 0.45f, 0.82f);
            [Tooltip("Height of the lighter band under the surface.")]
            public float bandHeight = 1.6f;
            public float waveHeight = 0.16f;
            public float waveLength = 5f;
            public float waveSpeed = 1.6f;
            public int sortingOrder = 14;
        }

        struct Ring
        {
            public float x, born, strength;
        }

        struct Bubble
        {
            public SpriteRenderer sprite;
            public float speed, wobble, phase;
        }

        const int Columns = 192;
        const float FoamThickness = 0.12f;
        const float RippleLife = 2.2f;

        static Material material;

        readonly List<Ring> ripples = new();
        readonly List<Bubble> bubbles = new();
        readonly List<SpriteRenderer> glints = new();

        Settings settings;
        Mesh mesh;
        Vector3[] vertices;
        Color[] colors;
        float left, right, bottom, stopY, startAt, speed, bubbleDebt;
        bool wasIn;

        /// <summary>World height of the still surface (waves roll a little above and below it).</summary>
        public float Surface { get; private set; }

        /// <param name="centerX">Middle of the shaft.</param>
        /// <param name="halfWidth">Half the shaft's width; the water tucks under the walls a little past it.</param>
        /// <param name="startY">Where the player starts: the surface starts `startBelow` under it.</param>
        /// <param name="stopY">The highest it ever gets.</param>
        public static BlueWater Create(Settings settings, float centerX, float halfWidth, float startY, float stopY)
        {
            var water = new GameObject("BlueWater").AddComponent<BlueWater>();
            water.settings = settings;
            water.left = centerX - halfWidth - settings.overhang;
            water.right = centerX + halfWidth + settings.overhang;
            water.Surface = startY - settings.startBelow;
            water.bottom = water.Surface - 20f;
            water.stopY = stopY;
            water.startAt = float.PositiveInfinity; // still until Begin()
            water.Build();
            return water;
        }

        /// <summary>The fight started: it starts rising after `startDelay`.</summary>
        public void Begin() => startAt = Mathf.Min(startAt, Time.time + settings.startDelay);

        /// <summary>Something fell in at `x`: the surface rings out from there.</summary>
        public void Ripple(float x, float strength = 1f)
        {
            ripples.Add(new Ring { x = x, born = Time.time, strength = strength });
            if (ripples.Count > 12) ripples.RemoveAt(0);
        }

        /// <summary>The wavy surface's height at `x` right now.</summary>
        public float HeightAt(float x) => Surface + Wave(x, Time.time);

        void Build()
        {
            if (!material) material = new Material(Shader.Find("Sprites/Default")) { name = "BlueWater" };
            mesh = new Mesh { name = "BlueWater" };
            mesh.MarkDynamic();

            // Four rows of vertices per column: foam top, crest, bottom of the light band, the deep bottom.
            vertices = new Vector3[Columns * 4];
            colors = new Color[Columns * 4];
            var triangles = new List<int>();
            for (int c = 0; c < Columns - 1; c++)
                for (int row = 0; row < 3; row++)
                {
                    int a = c * 4 + row, b = (c + 1) * 4 + row;
                    triangles.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            UpdateMesh();
            mesh.triangles = triangles.ToArray();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = settings.sortingOrder;

            for (int i = 0; i < 7; i++)
                glints.Add(FlatSprite.Create("Glint", transform, Vector2.zero, new Vector2(0.6f, 0.05f), UnityEngine.Color.clear, settings.sortingOrder + 1));
        }

        void Update()
        {
            Rise();
            TouchPlayer();
            UpdateMesh();
            UpdateGlints();
            UpdateBubbles();
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            foreach (var b in bubbles)
                if (b.sprite) Destroy(b.sprite.gameObject);
        }

        // ---------- Rising ----------

        void Rise()
        {
            float target = 0f;
            var pc = PlayerController.Instance;
            if (Time.time >= startAt && pc && !pc.Health.IsDead && Surface < stopY)
            {
                target = settings.riseSpeed;
                if (settings.rubberBand)
                {
                    float gap = Feet(pc) - Surface;
                    if (gap > settings.catchUpGap) target = Mathf.Max(target, settings.catchUpSpeed);
                    else if (gap < settings.nearGap) target = Mathf.Min(target, settings.nearSpeed);
                }
            }
            speed = Mathf.MoveTowards(speed, target, Time.deltaTime * 2f);
            Surface = Mathf.Min(stopY, Surface + speed * Time.deltaTime);
            bottom = Mathf.Min(bottom, Surface - 20f);
        }

        void TouchPlayer()
        {
            var pc = PlayerController.Instance;
            if (!pc || pc.Health.IsDead) { wasIn = false; return; }
            float x = pc.transform.position.x, feet = Feet(pc);
            bool inWater = feet < HeightAt(x) - 0.25f;
            if (inWater && !wasIn)
            {
                Ripple(x, 1.4f);
                for (int i = 0; i < 10; i++)
                    HeatPuff.Spawn(new Vector2(x, HeightAt(x)), new Vector2(Random.Range(-4f, 4f), Random.Range(3f, 7f)), 0.25f, 0.05f, settings.foam, 0.5f, settings.sortingOrder + 1);
            }
            wasIn = inWater;
            if (inWater) pc.Health.Kill(); // drowned: the level restarts
        }

        static float Feet(PlayerController pc)
        {
            float feet = pc.transform.position.y;
            foreach (var c in pc.GetComponentsInChildren<Collider2D>())
                if (!c.isTrigger) feet = Mathf.Min(feet, c.bounds.min.y);
            return feet;
        }

        // ---------- Look ----------

        float Wave(float x, float t)
        {
            float k = Mathf.PI * 2f / settings.waveLength;
            float h = settings.waveHeight * (Mathf.Sin(k * x + settings.waveSpeed * t)
                                            + 0.5f * Mathf.Sin(k * 2.3f * x - settings.waveSpeed * 1.7f * t + 1.3f)
                                            + 0.25f * Mathf.Sin(k * 0.45f * x + settings.waveSpeed * 0.6f * t + 4f));
            foreach (var r in ripples)
            {
                float age = t - r.born;
                if (age < 0f || age > RippleLife) continue;
                float d = Mathf.Abs(x - r.x);
                // A ring that spreads out at 6 u/s, dies away with distance and time.
                h += r.strength * 0.35f * Mathf.Exp(-age * 1.8f) * Mathf.Exp(-d * 0.25f) * Mathf.Cos((d - age * 6f) * 2.2f);
            }
            return h;
        }

        void UpdateMesh()
        {
            ripples.RemoveAll(r => Time.time - r.born > RippleLife);
            float t = Time.time;
            for (int c = 0; c < Columns; c++)
            {
                float x = Mathf.Lerp(left, right, c / (Columns - 1f));
                float crest = Surface + Wave(x, t);
                int i = c * 4;
                vertices[i] = new Vector3(x, crest + FoamThickness, 0f);
                vertices[i + 1] = new Vector3(x, crest, 0f);
                vertices[i + 2] = new Vector3(x, Mathf.Min(crest, Surface) - settings.bandHeight, 0f);
                vertices[i + 3] = new Vector3(x, bottom, 0f);

                // The foam flickers a little along its length.
                var foam = settings.foam;
                foam.a *= 0.75f + 0.25f * Mathf.Sin(x * 3.1f + t * 4f);
                colors[i] = foam;
                colors[i + 1] = UnityEngine.Color.Lerp(settings.surface, settings.foam, 0.25f);
                colors[i + 2] = settings.surface;
                colors[i + 3] = settings.deep;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        void UpdateGlints()
        {
            float t = Time.time;
            for (int i = 0; i < glints.Count; i++)
            {
                // Each glint slides along the surface on its own loop and twinkles in and out.
                float span = right - left;
                float x = left + Mathf.Repeat(i * span / glints.Count + t * (0.6f + 0.25f * (i % 3)), span);
                float y = HeightAt(x) - 0.12f - (i % 3) * 0.18f;
                float a = Mathf.Max(0f, Mathf.Sin(t * (1.3f + i * 0.37f) + i * 2f));
                glints[i].transform.position = new Vector3(x, y, 0f);
                glints[i].color = new Color(1f, 1f, 1f, 0.55f * a);
            }
        }

        void UpdateBubbles()
        {
            // Bubbles rise near the camera, wobbling, and pop at the surface.
            var cam = Camera.main;
            if (cam)
            {
                float camX = cam.transform.position.x, half = cam.orthographicSize * cam.aspect;
                float viewBottom = cam.transform.position.y - cam.orthographicSize;
                bubbleDebt += Time.deltaTime * 5f;
                for (; bubbleDebt >= 1f; bubbleDebt -= 1f)
                {
                    float from = Mathf.Max(bottom, viewBottom - 1f);
                    if (from > Surface - 1f) continue;
                    float x = Mathf.Clamp(camX + Random.Range(-half, half), left + 1f, right - 1f);
                    var sr = FlatSprite.Create("Bubble", transform, new Vector2(x, Random.Range(from, Surface - 1f)), Vector2.one, UnityEngine.Color.clear, settings.sortingOrder + 1);
                    sr.sprite = IndigoShapes.Ring;
                    sr.transform.localScale = Vector3.one * Random.Range(0.12f, 0.3f);
                    bubbles.Add(new Bubble { sprite = sr, speed = Random.Range(0.8f, 1.8f), wobble = Random.Range(0.05f, 0.15f), phase = Random.Range(0f, 6f) });
                }
            }

            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                if (b.sprite)
                {
                    var p = b.sprite.transform.position;
                    p.y += b.speed * Time.deltaTime;
                    p.x += Mathf.Sin(Time.time * 3f + b.phase) * b.wobble * Time.deltaTime * 6f;
                    float top = HeightAt(p.x);
                    if (p.y < top - 0.1f)
                    {
                        b.sprite.transform.position = p;
                        float fadeIn = Mathf.Clamp01((p.y - bottom) / 2f);
                        b.sprite.color = new Color(0.85f, 0.93f, 1f, 0.5f * fadeIn);
                        continue;
                    }
                    HeatPuff.Spawn(new Vector2(p.x, top), Vector2.up * 1.5f, b.sprite.transform.localScale.x, 0.02f, settings.foam, 0.25f, settings.sortingOrder + 1);
                    Destroy(b.sprite.gameObject);
                }
                bubbles.RemoveAt(i);
            }
        }
    }
}
