using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Step on it and you're thrown up to full height, jump held or not (Orange: over walls too tall to jump).
    /// Jumping over the pad by mistake means meeting the wall, so the lesson is "trust the spring".
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SpringPad : MonoBehaviour
    {
        [SerializeField] float launchVelocity = 17f;

        void Awake() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor) motor.Launch(launchVelocity);
        }
    }
}
