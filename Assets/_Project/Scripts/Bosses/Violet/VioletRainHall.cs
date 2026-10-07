using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ROYAL RAIN, placed in the scene: a stretch the player gets sealed into (the seals rise when the rain starts and sink
    /// when it ends) under a barrage of spectral swords, with a slab hanging from an anchor (shoot the anchor with Light
    /// Shot to drop it onto the pillars and hide under it). A Rain VioletZone starts it. Positions are LOCAL to this object.
    /// The pillars are painted in the tilemap; the slab, anchor and chain are the Shelter object.
    /// </summary>
    public class VioletRainHall : MonoBehaviour
    {
        [Tooltip("Rise to seal the hall when the rain starts; sink when it ends.")]
        public VioletGate[] seals = { };
        [Tooltip("The shelter's slab (hangs from the anchor until it's shot down).")]
        public VioletSlab slab;
        [Tooltip("The swords fall over this local x range (from, to).")]
        public Vector2 rainRange = new(1.6f, 34.4f);
        [Tooltip("Seconds of rain.")]
        public float duration = 7f;
        [Tooltip("Seconds of warning (seals closing, swords gathering) before it starts: time to shoot the anchor.")]
        public float telegraph = 3f;

        public float FromX => transform.position.x + rainRange.x;
        public float ToX => transform.position.x + rainRange.y;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.75f, 1f, 0.5f);
            float y = transform.position.y;
            Gizmos.DrawLine(new Vector3(FromX, y + 12f), new Vector3(ToX, y + 12f));
            Gizmos.DrawLine(new Vector3(FromX, y), new Vector3(FromX, y + 12f));
            Gizmos.DrawLine(new Vector3(ToX, y), new Vector3(ToX, y + 12f));
        }
    }
}
