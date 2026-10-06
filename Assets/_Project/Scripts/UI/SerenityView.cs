using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// PLACEHOLDER Serenity look + HUD meter. It only LISTENS to GameEvents.SerenityChanged / SerenityDenied and reads
    /// the player's position; gameplay never calls it. Everything runs on unscaled time, so it stays snappy in slow motion.
    ///   Start:   a big INDIGO shockwave: three concentric rings (deep #4B2BFF core, lighter #7B6CFF edge) race out
    ///            from the player past the screen edges, drawn in the world on top of gameplay; a short indigo flash;
    ///            the picture ripples outward (SerenityFilter).
    ///   Active:  a clear indigo wash and vignette, everything but the player slightly desaturated, and a soft indigo
    ///            aura pulsing around the player (a glow behind them plus an outline copy of their sprite).
    ///   End:     the rings contract back into the player and the look fades out (~0.3 s).
    ///   Denied:  pressed while recharging: a small gray ring blips at the player.
    ///   Meter:   top left, under the HUD. Active = a bright bar draining; recharging = a dim bar filling up;
    ///            ready = full and glowing with READY. A denied press shakes it red.
    /// </summary>
    public class SerenityView : MonoBehaviour
    {
        static readonly Color Core = new(0.294f, 0.169f, 1f);   // #4B2BFF
        static readonly Color Edge = new(0.482f, 0.424f, 1f);   // #7B6CFF

        [SerializeField] Color activeColor = new(0.62f, 0.55f, 1f);
        [SerializeField] Color readyColor = new(0.48f, 0.42f, 1f);
        [SerializeField] Color rechargeColor = new(0.32f, 0.3f, 0.48f);
        [SerializeField] Color deniedColor = new(1f, 0.35f, 0.4f);
        [Tooltip("Seconds the screen look takes to come in / go out (unscaled).")]
        [SerializeField] float easeIn = 0.25f;
        [SerializeField] float easeOut = 0.3f;
        [Tooltip("Seconds the shockwave rings take to race out past the screen edges.")]
        [SerializeField] float ringsOutTime = 0.55f;
        [Tooltip("Seconds the rings take to contract back into the player when it ends.")]
        [SerializeField] float ringsInTime = 0.35f;

        static Material lineMaterial;

        SerenityState state = SerenityState.Unavailable;
        float fraction, look, deniedAt = -10f, readyAt = -10f;
        SerenityFilter filter;
        GUIStyle label;
        SpriteRenderer auraGlow, auraOutline, auraSource;
        float auraAlpha;

        void OnEnable()
        {
            GameEvents.SerenityChanged += OnChanged;
            GameEvents.SerenityDenied += OnDenied;
            GameEvents.SceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.SerenityChanged -= OnChanged;
            GameEvents.SerenityDenied -= OnDenied;
            GameEvents.SceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(string _)
        {
            state = SerenityState.Unavailable;
            look = 0f;
            auraAlpha = 0f;
            filter = null; // it lived on the old scene's camera, like the aura on the old player
        }

        void OnDenied()
        {
            deniedAt = Time.unscaledTime;
            var player = PlayerController.Instance;
            if (player) StartCoroutine(Blip(player.transform));
        }

        void OnChanged(SerenityState newState, float newFraction)
        {
            var old = state;
            state = newState;
            fraction = newFraction;
            if (old == newState) return;

            if (newState == SerenityState.Active) Begin();
            else if (old == SerenityState.Active) Release();
            if (newState == SerenityState.Ready && old == SerenityState.Recharging) readyAt = Time.unscaledTime;
        }

        void Begin()
        {
            var player = PlayerController.Instance;
            var cam = Camera.main;
            if (!player || !cam) return;
            filter = SerenityFilter.On(cam);
            if (filter)
            {
                filter.Flash(new Color(Core.r, Core.g, Core.b, 0.55f), 0.3f);
                filter.Ripple(player.transform.position, 1.1f, 0.8f);
            }
            float reach = ReachPastScreen(cam, player.transform.position);
            for (int i = 0; i < 3; i++) StartCoroutine(RingOut(player.transform.position, reach, i * 0.07f, 1f - i * 0.18f));
        }

        void Release()
        {
            var player = PlayerController.Instance;
            var cam = Camera.main;
            if (!player || !cam) return;
            float reach = ReachPastScreen(cam, player.transform.position) * 0.8f;
            for (int i = 0; i < 3; i++) StartCoroutine(RingIn(player.transform, reach * (1f - i * 0.2f), i * 0.05f));
            if (filter) filter.Ripple(player.transform.position, 0.4f, 0.4f);
        }

        /// <summary>From the player to past the farthest screen corner, in world units.</summary>
        static float ReachPastScreen(Camera cam, Vector2 from)
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            float dx = Mathf.Max(Mathf.Abs(from.x - (c.x - w)), Mathf.Abs(from.x - (c.x + w)));
            float dy = Mathf.Max(Mathf.Abs(from.y - (c.y - h)), Mathf.Abs(from.y - (c.y + h)));
            return Mathf.Sqrt(dx * dx + dy * dy) + 2f;
        }

        IEnumerator RingOut(Vector2 center, float reach, float delay, float strength)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            var ring = Ring.Make("SerenityRing");
            for (float t = 0f; t < ringsOutTime; t += Time.unscaledDeltaTime)
            {
                float k = t / ringsOutTime;
                float r = Mathf.Lerp(0.4f, reach, 1f - (1f - k) * (1f - k) * (1f - k)); // fast out, easing at the edges
                float a = strength * (k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                ring.Draw(center, r, 0.32f, 0.9f, Core, Edge, a);
                yield return null;
            }
            ring.Destroy();
        }

        IEnumerator RingIn(Transform player, float from, float delay)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            var ring = Ring.Make("SerenityRingIn");
            for (float t = 0f; t < ringsInTime && player; t += Time.unscaledDeltaTime)
            {
                float k = t / ringsInTime;
                float r = Mathf.Lerp(from, 0.2f, k * k); // gathers speed as it closes in
                ring.Draw(player.position, r, 0.25f, 0.7f, Core, Edge, 0.85f * (1f - k * 0.5f));
                yield return null;
            }
            ring.Destroy();
            if (!player) yield break;
            for (int i = 0; i < 8; i++) // a soft puff as it closes on the player
                HeatPuff.Spawn(player.position, Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f), 0.25f, 0.05f, Edge, 0.35f, 201);
        }

        IEnumerator Blip(Transform player)
        {
            var ring = Ring.Make("SerenityNotReady");
            var gray = new Color(0.62f, 0.62f, 0.66f);
            for (float t = 0f; t < 0.3f && player; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                ring.Draw(player.position, Mathf.Lerp(0.5f, 1.4f, k), 0.07f, 0.16f, gray, gray, 0.8f * (1f - k));
                yield return null;
            }
            ring.Destroy();
        }

        void Update()
        {
            bool on = state == SerenityState.Active;
            look = Mathf.MoveTowards(look, on ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, on ? easeIn : easeOut));
            auraAlpha = look;

            if (look > 0f || filter)
            {
                if (!filter && Camera.main) filter = SerenityFilter.On(Camera.main);
                if (filter)
                {
                    filter.Amount = Mathf.SmoothStep(0f, 1f, look);
                    if (look > 0f) filter.enabled = true;
                }
            }
        }

        // The aura copies the player's sprite after the animator has picked this frame's.
        void LateUpdate()
        {
            var player = PlayerController.Instance;
            if (auraAlpha <= 0.001f || !player)
            {
                if (auraGlow) auraGlow.enabled = false;
                if (auraOutline) auraOutline.enabled = false;
                return;
            }
            if (!auraGlow || auraGlow.transform.parent != player.transform) BuildAura(player);
            if (!auraGlow) return;

            float now = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);
            auraGlow.enabled = true;
            auraGlow.transform.localScale = Vector3.one * (2.2f + 0.35f * pulse);
            auraGlow.color = new Color(Edge.r, Edge.g, Edge.b, (0.22f + 0.12f * pulse) * auraAlpha);

            if (auraSource && auraOutline)
            {
                auraOutline.enabled = auraSource.enabled;
                auraOutline.sprite = auraSource.sprite;
                auraOutline.flipX = auraSource.flipX;
                auraOutline.transform.position = auraSource.transform.position;
                auraOutline.transform.rotation = auraSource.transform.rotation;
                auraOutline.transform.localScale = auraSource.transform.lossyScale * (1.12f + 0.06f * pulse);
                auraOutline.color = new Color(Core.r, Core.g, Core.b, (0.55f + 0.25f * pulse) * auraAlpha);
            }
        }

        void BuildAura(PlayerController player)
        {
            var visual = player.transform.Find("Visual");
            auraSource = visual ? visual.GetComponent<SpriteRenderer>() : player.GetComponentInChildren<SpriteRenderer>();
            int order = auraSource ? auraSource.sortingOrder : 10;
            auraGlow = IndigoShapes.Create("SerenityAuraGlow", IndigoShapes.Disc, player.transform, Vector2.zero, 2.2f, Color.clear, order - 2);
            // Not parented to the visual (its squash / flip / art swaps are copied each frame instead).
            auraOutline = new GameObject("SerenityAuraOutline").AddComponent<SpriteRenderer>();
            auraOutline.transform.SetParent(player.transform, true);
            auraOutline.sortingOrder = order - 1;
            if (auraSource) auraOutline.sharedMaterial = auraSource.sharedMaterial;
        }

        void OnGUI()
        {
            if (state == SerenityState.Unavailable || !PlayerController.Instance) return;
            label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13, richText = true };

            float now = Time.unscaledTime;
            float denied = Mathf.Clamp01(1f - (now - deniedAt) / 0.35f);
            float shake = denied * Mathf.Sin(now * 70f) * 5f;
            var bar = new Rect(12f + shake, 104f, 200f, 12f);

            Color fill;
            string text;
            switch (state)
            {
                case SerenityState.Active:
                    fill = activeColor;
                    text = "SERENITY";
                    break;
                case SerenityState.Recharging:
                    fill = rechargeColor;
                    text = "SERENITY  <color=#9a96b8>recharging</color>";
                    break;
                default:
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f);
                    float pop = Mathf.Clamp01(1f - (now - readyAt) / 0.5f);
                    fill = Color.Lerp(readyColor, Color.white, 0.15f * pulse + 0.6f * pop);
                    text = "SERENITY  <color=#c8c0ff>READY</color>  [Q]";
                    break;
            }
            fill = Color.Lerp(fill, deniedColor, denied);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
            GUI.color = new Color(fill.r * 0.35f, fill.g * 0.35f, fill.b * 0.35f, 0.9f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fraction), bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x, bar.y - 22f, 320f, 22f), text, label);
        }

        /// <summary>A circle of constant line width in the world, on top of gameplay: a bright core line inside a softer, wider edge.</summary>
        sealed class Ring
        {
            const int Segments = 72;
            LineRenderer core, edge;
            GameObject go;

            public static Ring Make(string name)
            {
                var r = new Ring { go = new GameObject(name) };
                r.edge = Line(r.go, "Edge", 199);
                r.core = Line(r.go, "Core", 200);
                return r;
            }

            static LineRenderer Line(GameObject parent, string name, int order)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent.transform, false);
                var lr = child.AddComponent<LineRenderer>();
                if (!lineMaterial) lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                lr.sharedMaterial = lineMaterial;
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = Segments;
                lr.numCornerVertices = 0;
                lr.numCapVertices = 0;
                lr.sortingOrder = order;
                lr.alignment = LineAlignment.View;
                return lr;
            }

            public void Draw(Vector2 center, float radius, float coreWidth, float edgeWidth, Color coreColor, Color edgeColor, float alpha)
            {
                if (!go) return;
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    var p = (Vector3)(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                    core.SetPosition(i, p);
                    edge.SetPosition(i, p);
                }
                core.widthMultiplier = coreWidth;
                edge.widthMultiplier = edgeWidth;
                core.startColor = core.endColor = new Color(coreColor.r, coreColor.g, coreColor.b, alpha);
                edge.startColor = edge.endColor = new Color(edgeColor.r, edgeColor.g, edgeColor.b, alpha * 0.45f);
            }

            public void Destroy()
            {
                if (go) Object.Destroy(go);
            }
        }
    }
}
