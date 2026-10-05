using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Makes hits read: on Health.Damaged it flashes the sprites, blinks them and kicks the camera.
    /// Put it next to a Health (player, bosses, enemies). It only touches the sprites' color and
    /// enabled state, never the body, so it doesn't fight movement code.
    /// </summary>
    public class HitFeedbackPlayer : MonoBehaviour
    {
        [Tooltip("Parent of the sprites to flash and blink. Defaults to the child named Visual.")]
        [SerializeField] Transform visual;

        [Header("Flash")]
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashTime = 0.08f;

        [Header("Blink")]
        [Tooltip("Total time the sprites keep blinking after a hit.")]
        [SerializeField] float blinkDuration = 0.5f;
        [Tooltip("Seconds between each on/off toggle. Smaller = faster blinking.")]
        [SerializeField] float blinkInterval = 0.06f;

        [Header("Camera")]
        [Tooltip("Camera kick strength in world units. 0 = none.")]
        [SerializeField] float cameraShake = 0.15f;
        [SerializeField] float cameraShakeTime = 0.25f;

        Health health;
        SpriteRenderer[] sprites;
        Color[] baseColors;
        bool[] baseEnabled;
        Coroutine flash, blink;

        void Awake()
        {
            health = GetComponentInParent<Health>();
            if (!visual) visual = transform.Find("Visual");
            sprites = (visual ? visual : transform).GetComponentsInChildren<SpriteRenderer>();
            baseColors = new Color[sprites.Length];
            baseEnabled = new bool[sprites.Length];
        }

        void OnEnable()
        {
            if (health) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health) health.Damaged -= OnDamaged;
            // Disabling stops the coroutines, so put things back by hand.
            if (flash != null) EndFlash();
            if (blink != null) EndBlink();
        }

        void OnDamaged(DamageInfo info)
        {
            flash ??= StartCoroutine(Flash());

            // Restart the blink cleanly if we get hit again mid-blink.
            if (blink != null)
            {
                StopCoroutine(blink);
                EndBlink();
            }
            blink = StartCoroutine(Blink());

            if (cameraShake > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam))
                cam.Shake(cameraShake, cameraShakeTime);
        }

        IEnumerator Flash()
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                baseColors[i] = sprites[i].color;
                sprites[i].color = flashColor;
            }
            yield return new WaitForSeconds(flashTime);
            EndFlash();
        }

        void EndFlash()
        {
            // Skip sprites someone else recolored mid-flash (e.g. Recolorable during the reclaim fade).
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] && sprites[i].color == flashColor) sprites[i].color = baseColors[i];
            flash = null;
        }

        IEnumerator Blink()
        {
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) baseEnabled[i] = sprites[i].enabled;

            bool show = false; // first toggle hides the sprites
            float end = Time.time + blinkDuration;
            while (Time.time < end)
            {
                SetSprites(show);
                show = !show;
                yield return new WaitForSeconds(blinkInterval);
            }
            EndBlink();
        }

        void SetSprites(bool show)
        {
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) sprites[i].enabled = show && baseEnabled[i];
        }

        void EndBlink()
        {
            // Always restore to whatever each sprite was before the blink started.
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) sprites[i].enabled = baseEnabled[i];
            blink = null;
        }
    }
}