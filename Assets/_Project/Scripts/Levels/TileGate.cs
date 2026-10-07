using UnityEngine;
using UnityEngine.Tilemaps;

namespace Roygbiv
{
    /// <summary>
    /// Changes a few cells of a Tilemap at runtime: Close() puts the baked tiles in (an empty tile clears its cell).
    /// Seals the arena doors when a fight starts, grows Green's bridge, and opens Indigo's sealed gate. The cells and
    /// tiles are baked by the level's editor tool, including the neighbours whose edge pieces change with them, so
    /// there's no seam. The Tilemap's collider picks up the change by itself. Hook Close() to a LevelTrigger or a
    /// ShootableSwitch.
    /// </summary>
    public class TileGate : MonoBehaviour
    {
        [SerializeField] Tilemap map;
        [SerializeField] Vector3Int[] cells = { };
        [SerializeField] TileBase[] tiles = { };
        [SerializeField] float cameraShake = 0.25f;
        [SerializeField] Color dustColor = new(0.85f, 0.75f, 0.5f, 0.7f);

        bool closed;

        /// <summary>Editor bake: where the door goes and what it looks like once shut.</summary>
        public void Setup(Tilemap tilemap, Vector3Int[] doorCells, TileBase[] doorTiles)
        {
            map = tilemap;
            cells = doorCells;
            tiles = doorTiles;
        }

        public void Close()
        {
            if (closed || !map) return;
            closed = true;
            map.SetTiles(cells, tiles);
            foreach (var c in cells)
                HeatPuff.Spawn(map.GetCellCenterWorld(c), Random.insideUnitCircle * 2f, 0.4f, 0.9f, dustColor, 0.6f, 12);
            if (cameraShake > 0f && Camera.main && Camera.main.TryGetComponent<CameraFollow>(out var cam)) cam.Shake(cameraShake, 0.25f);
        }
    }
}
