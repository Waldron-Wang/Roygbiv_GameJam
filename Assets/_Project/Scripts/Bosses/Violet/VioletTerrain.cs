using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Roygbiv
{
    /// <summary>
    /// Violet's painted terrain: Grid > Tilemap, a TilemapCollider2D merged into a CompositeCollider2D. The merged collider
    /// is saved with the scene, so a scene saved before its geometry was generated has an EMPTY collider and nothing is
    /// solid. Rebuild makes it again from the tiles (ROYGBIV > Fix Violet Colliders in the editor; VioletApproach at
    /// runtime as a safety net). GroundBelow finds the top of the tile ground under a point (where the king stands).
    /// </summary>
    public static class VioletTerrain
    {
        /// <summary>The merged collider of every solid tilemap in `scene`.</summary>
        public static List<CompositeCollider2D> Composites(Scene scene)
        {
            var list = new List<CompositeCollider2D>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tiles in root.GetComponentsInChildren<TilemapCollider2D>(true))
                    if (tiles.TryGetComponent<CompositeCollider2D>(out var composite)) list.Add(composite);
            return list;
        }

        /// <summary>Makes a tilemap's merged collider again from its tiles, right now. Returns its path count (0 = still empty).</summary>
        public static int Rebuild(CompositeCollider2D composite)
        {
            if (composite.TryGetComponent<Tilemap>(out var map)) map.CompressBounds();
            composite.TryGetComponent<TilemapCollider2D>(out var tiles);
            if (tiles) tiles.ProcessTilemapChanges(); // tile changes reach the collider lazily
            composite.GenerateGeometry();
            if (composite.pathCount == 0 && tiles)
            {
                // Still empty: make the tile collider build all its shapes again from scratch, then merge them.
                tiles.enabled = false;
                tiles.enabled = true;
                tiles.ProcessTilemapChanges();
                composite.GenerateGeometry();
            }
            return composite.pathCount;
        }

        /// <summary>The top of the nearest tile ground straight below `from` (within `distance`). Other colliders are ignored.</summary>
        public static bool GroundBelow(Vector2 from, float distance, out float y)
        {
            bool hit = Raycast(from, Vector2.down, distance, out var point);
            y = point.y;
            return hit;
        }

        /// <summary>Where a ray from `from` first meets the tiles (within `distance`). Other colliders are ignored.</summary>
        public static bool Raycast(Vector2 from, Vector2 direction, float distance, out Vector2 point)
        {
            point = from;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics2D.RaycastAll(from, direction, distance))
            {
                var c = hit.collider;
                if (!c || c.isTrigger || !c.TryGetComponent<Tilemap>(out _) || hit.distance >= best) continue;
                best = hit.distance;
                point = hit.point;
            }
            return best < float.PositiveInfinity;
        }
    }
}
