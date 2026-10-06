using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// A soft "look here" pulse on a sprite: it breathes in size and brightness (the chain anchor you're meant
    /// to shoot). Purely visual.
    /// </summary>
    public class VioletPulse : MonoBehaviour
    {
        [Tooltip("Pulses per second.")]
        [SerializeField] float speed = 1.6f;
        [Tooltip("How much bigger it gets at the top of a pulse.")]
        [SerializeField] float grow = 0.35f;

        SpriteRenderer sprite;
        Color baseColor;
        Vector3 baseScale;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            baseColor = sprite ? sprite.color : Color.white;
            baseScale = transform.localScale;
        }

        void Update()
        {
            if (!sprite || !sprite.enabled) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f);
            transform.localScale = baseScale * (1f + grow * k);
            sprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (0.45f + 0.55f * k));
        }
    }
}
