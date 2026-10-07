using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>Local collision exceptions for Royal Rain only; player shots and bodies stay solid.</summary>
    public sealed class VioletRainPassThrough : MonoBehaviour
    {
        static readonly HashSet<VioletRainPassThrough> structures = new();
        readonly List<Collider2D> swords = new();
        Collider2D[] solids;
        bool blocking;

        void Awake() => solids = GetComponents<Collider2D>();
        void OnEnable() => structures.Add(this);
        void OnDisable()
        {
            BlockRain();
            structures.Remove(this);
        }

        public static void Register(Collider2D sword)
        {
            foreach (var structure in structures)
            {
                if (structure.blocking) continue;
                structure.swords.RemoveAll(c => !c);
                structure.swords.Add(sword);
                foreach (var solid in structure.solids)
                    if (solid) Physics2D.IgnoreCollision(sword, solid, true);
            }
        }

        public void BlockRain()
        {
            blocking = true;
            foreach (var sword in swords)
                if (sword)
                    foreach (var solid in solids)
                        if (solid) Physics2D.IgnoreCollision(sword, solid, false);
            swords.Clear();
        }
    }
}
