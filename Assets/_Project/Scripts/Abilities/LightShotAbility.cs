using UnityEngine;

namespace Roygbiv
{
    /// <summary>Yellow's reward. Fires a projectile. Also hits switches (Orange chase).</summary>
    public class LightShotAbility : AbilityBase
    {
        [SerializeField] Projectile projectilePrefab;
        [SerializeField] float spawnOffset = 0.7f;

        public override AbilityId Id => AbilityId.LightShot;

        public override bool HandleInput(in PlayerIntent intent) => intent.shootPressed && TryActivate();

        protected override void Activate()
        {
            if (projectilePrefab == null) { Debug.LogWarning("LightShot has no projectile prefab."); return; }
            var dir = Owner.AimDirection;
            var pos = (Vector2)Owner.Root.position + dir * spawnOffset;
            Instantiate(projectilePrefab, pos, Quaternion.identity).Launch(dir, Owner.Team);
        }
    }
}
