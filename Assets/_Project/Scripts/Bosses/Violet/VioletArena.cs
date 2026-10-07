using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// The duel's arena, placed in the scene (its walls, floor and platforms are painted in the tilemap). The arrival
    /// cinematic cuts here: the player appears at `playerSpawn`, the king at `bossSpawn`, and the camera locks on the
    /// center of `cameraView`. `inner` is the space between the walls (the boss's movement and attacks stay inside it).
    /// The door is a gate in the left wall, closed. Positions are LOCAL to this object; gizmos draw them all.
    /// </summary>
    public class VioletArena : MonoBehaviour
    {
        [Tooltip("Between the walls, local: x from the left wall's inner face to the right one's, y from the floor up.")]
        public Rect inner = new(0f, 0f, 24f, 16f);
        [Tooltip("What the locked camera frames, local. The camera holds on its center (16:9 at size 7 is 24.9 x 14).")]
        public Rect cameraView = new(-0.45f, -2f, 24.9f, 14f);
        [Tooltip("Where the player's feet land after the cut.")]
        public Transform playerSpawn;
        [Tooltip("Where the king stands for the duel (his feet).")]
        public Transform bossSpawn;
        [Tooltip("The gate in the left wall. Closed for the whole duel.")]
        public VioletGate door;

        Vector2 Origin => transform.position;
        public float MinX => Origin.x + inner.xMin;
        public float MaxX => Origin.x + inner.xMax;
        public float Floor => Origin.y + inner.yMin;
        public Vector2 CameraCenter => Origin + cameraView.center;
        public Vector2 PlayerFeet => playerSpawn ? (Vector2)playerSpawn.position : new Vector2(MinX + 3f, Floor);
        public float BossX => bossSpawn ? bossSpawn.position.x : MaxX - 5f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireCube(new Vector3((MinX + MaxX) * 0.5f, Floor + inner.height * 0.5f), new Vector3(inner.width, inner.height, 0f));
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireCube(CameraCenter, cameraView.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(PlayerFeet + new Vector2(0f, 0.6f), new Vector3(0.8f, 1.2f, 0f));
            Gizmos.color = new Color(0.8f, 0.4f, 1f);
            Gizmos.DrawWireCube(new Vector3(BossX, Floor + 1.6f), new Vector3(1.6f, 3.2f, 0f));
        }
    }
}
