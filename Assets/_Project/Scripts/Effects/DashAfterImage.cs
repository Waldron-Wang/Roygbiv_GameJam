using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    
    /// Leaves fading ghost copies of the sprites behind while active (dash afterimage).
    public class DashAfterImage : MonoBehaviour
    {
        [Tooltip("Parent of the sprites to copy. Defaults to the child named Visual, else this object.")]
        [SerializeField] Transform visual;

        [Header("Look")]
        [Tooltip("Multiplied with the sprite color. Alpha is the starting opacity of each ghost.")]
        [SerializeField] Color tint = new Color(0.6f, 0.9f, 1f, 0.6f);
        [SerializeField] float fadeTime = 0.25f;
        [Tooltip("Ghosts draw this many sorting orders behind the real sprite.")]
        [SerializeField] int sortingOrderOffset = -1;

        [Header("Rate")]
        [Tooltip("Seconds between ghosts. Smaller = denser trail.")]
        [SerializeField] float spawnInterval = 0.04f;

        SpriteRenderer[] sources;
        bool active;
        float endTime = float.PositiveInfinity;
        float nextSpawn;
        Color? tintOverride; // set by Play(duration, tint) for the current trail only

        void Awake()
        {
            if (!visual) visual = transform.Find("Visual");
            sources = (visual ? visual : transform).GetComponentsInChildren<SpriteRenderer>();
        }

        void OnDisable() => StopTrail();

        
        public void Play(float duration)
        {
            active = true;
            endTime = Time.time + duration;
            nextSpawn = 0f; // spawn one immediately
            tintOverride = null;
        }

        /// <summary>Like Play, with a different ghost tint for this trail only (Down Dash's dive).</summary>
        public void Play(float duration, Color tint)
        {
            Play(duration);
            tintOverride = tint;
        }

        public void StartTrail()
        {
            active = true;
            endTime = float.PositiveInfinity;
            nextSpawn = 0f;
            tintOverride = null;
        }

        public void StopTrail() => active = false;

        void Update()
        {
            if (!active) return;
            if (Time.time >= endTime) { active = false; return; }

            if (Time.time >= nextSpawn)
            {
                Spawn();
                nextSpawn = Time.time + spawnInterval;
            }
        }

        void Spawn()
        {
            foreach (var src in sources)
            {
                
                if (!src || !src.enabled || !src.sprite) continue;

                var go = new GameObject("Afterimage");
                go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                go.transform.localScale = src.transform.lossyScale;

                var ghost = go.AddComponent<SpriteRenderer>();
                ghost.sprite = src.sprite;
                ghost.flipX = src.flipX;
                ghost.flipY = src.flipY;
                ghost.sharedMaterial = src.sharedMaterial;
                ghost.sortingLayerID = src.sortingLayerID;
                ghost.sortingOrder = src.sortingOrder + sortingOrderOffset;
                ghost.color = src.color * (tintOverride ?? tint);

                Destroy(go, fadeTime + 0.1f); // safety net if this object is disabled mid-fade
                StartCoroutine(Fade(ghost));
            }
        }

        IEnumerator Fade(SpriteRenderer ghost)
        {
            Color start = ghost.color;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                if (!ghost) yield break;
                Color c = start;
                c.a = start.a * (1f - t / fadeTime);
                ghost.color = c;
                yield return null;
            }
            if (ghost) Destroy(ghost.gameObject);
        }
    }
}