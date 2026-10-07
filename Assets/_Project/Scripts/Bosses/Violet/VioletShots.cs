using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Violet's projectiles, made at runtime (no prefabs): magic orbs and falling spectral swords. Both are plain
    /// Projectiles, so the usual rules apply: orbs marked reflectable can be punched back with the basic attack
    /// (a nod to Yellow; they glow gold-white so they read), swords shatter on anything solid (the slab shelter).
    /// </summary>
    public static class VioletShots
    {
        public static readonly Color OrbColor = new(0.72f, 0.36f, 1f);
        public static readonly Color ReflectableColor = new(1f, 0.88f, 0.55f);

        /// <param name="homesBack">Reflected, it flies back at the shooter (the duel). Off far away (the approach), where it'd never arrive.</param>
        public static Projectile Orb(Vector2 at, Vector2 direction, float speed, bool reflectable, Transform shooter, bool homesBack = true, float size = 0.6f)
        {
            var color = reflectable ? ReflectableColor : OrbColor;
            var go = new GameObject(reflectable ? "VioletOrb (punch it back)" : "VioletOrb");
            go.transform.position = at;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = IndigoShapes.Disc;
            sr.color = color;
            sr.sortingOrder = 30;
            IndigoShapes.Create("Core", IndigoShapes.Disc, go.transform, Vector2.zero, 0.5f, Color.white, 31);
            if (reflectable) IndigoShapes.Create("Rim", IndigoShapes.ThinRing, go.transform, Vector2.zero, 1.35f, new Color(1f, 0.95f, 0.75f, 0.8f), 29);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().radius = 0.45f;
            var p = go.AddComponent<Projectile>();
            p.damage = 1;
            p.speed = speed;
            p.lifetime = 9f;
            p.reflectable = reflectable;
            p.reflectSpeedMultiplier = 1.6f;
            p.reflectHomesOnShooter = reflectable && homesBack;
            p.destroyOnWorld = true;
            p.Launch(direction, Team.Enemy, shooter);
            return p;
        }

        /// <summary>A spectral sword falling straight down from `at`.</summary>
        public static Projectile Sword(Vector2 at, float speed, Color color, float length = 1.6f, bool shelterRain = false)
        {
            var go = new GameObject("SpectralSword");
            go.transform.position = at;
            go.transform.localScale = Vector3.one * (length * 0.5f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VioletShapes.SpectralSword;
            sr.color = color;
            sr.sortingOrder = 30;
            var glow = VioletShapes.Create("Glow", VioletShapes.SpectralSword, go.transform, Vector2.zero, new Color(1f, 1f, 1f, 0.5f), 31);
            glow.transform.localScale = new Vector3(0.45f, 0.9f, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.26f, 1.7f);
            box.offset = new Vector2(0f, -0.1f);
            var p = go.AddComponent<Projectile>();
            p.damage = 1;
            p.speed = speed;
            p.lifetime = 3f;
            p.destroyOnWorld = true;
            p.Launch(Vector2.down, Team.Enemy);
            if (shelterRain) VioletRainPassThrough.Register(box);
            return p;
        }
    }
}
