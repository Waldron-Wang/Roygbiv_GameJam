using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Added to the player on demand by VioletHits.Shove: throws them along a velocity for a moment, holding their
    /// movement locked every physics step so a Dash or a surf can't carry them through (the needle curtain throws
    /// you back out). Gravity still pulls; control comes back when it ends.
    /// </summary>
    public class VioletShove : MonoBehaviour
    {
        Coroutine routine;
        IActor actor;

        public void Push(Vector2 velocity, float seconds)
        {
            actor ??= GetComponent<IActor>();
            if (actor == null || !actor.Body) return;
            foreach (var d in GetComponentsInChildren<DownDashAbility>()) d.Cancel();
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PushRoutine(velocity, seconds));
        }

        IEnumerator PushRoutine(Vector2 velocity, float seconds)
        {
            var body = actor.Body;
            body.linearVelocity = velocity;
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                if (actor.Health && actor.Health.IsDead) { routine = null; yield break; }
                actor.MovementLocked = true;
                body.linearVelocity = new Vector2(velocity.x, body.linearVelocity.y);
                yield return new WaitForFixedUpdate();
            }
            actor.MovementLocked = false;
            routine = null;
        }

        void OnDisable()
        {
            if (routine == null) return;
            routine = null;
            if (actor != null) actor.MovementLocked = false;
        }
    }
}
