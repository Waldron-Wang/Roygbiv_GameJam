using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Makes hits read: on Health.Damaged it flashes the sprites, jitters the visual and kicks the camera.
    /// Put it next to a Health (bosses, enemies). It only moves the visual child, never the body,
    /// so it doesn't fight movement code.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [Tooltip("Shaken and flashed. Defaults to the child named Visual.")]
        [SerializeField] Transform visual;

        [Header("Flash")]
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashTime = 0.08f;

        [Header("Shake")]
        [SerializeField] float shakeAmplitude = 0.2f;
        [SerializeField] float shakeTime = 0.25f;
        [Tooltip("Camera kick strength in world units. 0 = none.")]
        [SerializeField] float cameraShake = 0.15f;

        Health health;
        SpriteRenderer[] sprites;
        Color[] baseColors;
        Vector3 visualPos;
        Coroutine flash, shake;

        void Awake()
        {
            health = GetComponentInParent<Health>();
            if (!visual) visual = transform.Find("Visual");
            if (visual) visualPos = visual.localPosition;
            sprites = (visual ? visual : transform).GetComponentsInChildren<SpriteRenderer>();
            baseColors = new Color[sprites.Length];
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
            if (shake != null) EndShake();
        }

        void OnDamaged(DamageInfo info)
        {
            flash ??= StartCoroutine(Flash());

            if (shake != null) StopCoroutine(shake);
            if (visual) shake = StartCoroutine(Shake());

            if (cameraShake > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam))
                cam.Shake(cameraShake, shakeTime);
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

        IEnumerator Shake()
        {
            for (float t = 0f; t < shakeTime; t += Time.deltaTime)
            {
                float strength = shakeAmplitude * (1f - t / shakeTime);
                visual.localPosition = visualPos + (Vector3)(Random.insideUnitCircle * strength);
                yield return null;
            }
            EndShake();
        }

        void EndShake()
        {
            if (visual) visual.localPosition = visualPos;
            shake = null;
        }
    }
}
