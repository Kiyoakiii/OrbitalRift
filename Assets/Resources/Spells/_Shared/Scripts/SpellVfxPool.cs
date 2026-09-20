using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    public sealed class SpellVfxPool : MonoBehaviour
    {
        [Tooltip("Change this prefab before entering Play Mode to use a spell variant.")]
        public SpellProjectileVfx ProjectilePrefab;
        private struct Flight { public SpellProjectileVfx Fx; public ObjectPool<SpellProjectileVfx> Pool; }
        private struct Impact { public SpellImpactVfx Fx; public ObjectPool<SpellImpactVfx> Pool; }
        private readonly Dictionary<SpellProjectileVfx, ObjectPool<SpellProjectileVfx>> shotPools = new Dictionary<SpellProjectileVfx, ObjectPool<SpellProjectileVfx>>();
        private readonly Dictionary<SpellImpactVfx, ObjectPool<SpellImpactVfx>> impactPools = new Dictionary<SpellImpactVfx, ObjectPool<SpellImpactVfx>>();
        private readonly List<Flight> activeShots = new List<Flight>(64);
        private readonly List<Impact> activeImpacts = new List<Impact>(24);
        public int ActiveShots => activeShots.Count;
        public int ActiveImpacts => activeImpacts.Count;
        public int ImpactCount { get; private set; }

        public void Initialize()
        {
            if (ProjectilePrefab == null) ProjectilePrefab = Resources.Load<SpellProjectileVfx>("Spells/PlasmaBolt/Prefabs/PlasmaBolt");
            Prewarm(ProjectilePrefab, 32, 16);
        }

        public void Prewarm(SpellProjectileVfx prefab, int flightCount = 12, int impactCount = 8)
        {
            if (prefab == null || prefab.Profile == null || prefab.ImpactPrefab == null)
            { Debug.LogError("Spell VFX: projectile/profile/impact asset is missing.", this); return; }
            if (!shotPools.ContainsKey(prefab)) shotPools.Add(prefab, new ObjectPool<SpellProjectileVfx>(prefab, transform, flightCount));
            if (!impactPools.ContainsKey(prefab.ImpactPrefab)) impactPools.Add(prefab.ImpactPrefab, new ObjectPool<SpellImpactVfx>(prefab.ImpactPrefab, transform, impactCount));
        }

        public bool Attach(Projectile owner, SpellProjectileVfx prefab = null, SpellVfxProfile profile = null, SpellImpactVfx impactPrefab = null)
        {
            if (prefab == null) prefab = ProjectilePrefab;
            if (owner == null || owner.SpellVfx != null || prefab == null || activeShots.Count >= 128) return false;
            if (!shotPools.TryGetValue(prefab, out var pool))
            { Prewarm(prefab); if (!shotPools.TryGetValue(prefab, out pool)) return false; }
            var fx = pool.Get(); fx.Profile = profile != null ? profile : prefab.Profile;
            fx.ImpactPrefab = impactPrefab != null ? impactPrefab : prefab.ImpactPrefab;
            EnsureImpactPool(fx.ImpactPrefab);
            fx.Begin(owner); activeShots.Add(new Flight { Fx=fx, Pool=pool });
            return true;
        }

        public void Hit(Projectile owner, Vector3 point)
        {
            var fx = owner.SpellVfx;
            if (fx == null) return;
            if (activeImpacts.Count < 24)
            {
                var pool = impactPools[fx.ImpactPrefab];
                var impact = pool.Get(); impact.Begin(point, fx.Profile);
                activeImpacts.Add(new Impact { Fx=impact, Pool=pool }); ImpactCount++;
            }
            fx.End();
        }

        private void EnsureImpactPool(SpellImpactVfx prefab)
        { if (prefab != null && !impactPools.ContainsKey(prefab)) impactPools.Add(prefab, new ObjectPool<SpellImpactVfx>(prefab, transform, 4)); }

        public void EmitImpact(Vector3 point, SpellImpactVfx prefab, SpellVfxProfile profile)
        {
            if (prefab == null || profile == null || activeImpacts.Count >= 24) return;
            EnsureImpactPool(prefab); var pool = impactPools[prefab];
            var fx = pool.Get(); fx.Begin(point, profile); activeImpacts.Add(new Impact { Fx=fx, Pool=pool });
        }

        // Match the swept gameplay hit, including a frame that crosses the entire target.
        public static Vector2 ContactPoint(Vector2 start, Vector2 end, Vector2 center, float radius)
        {
            var direction = end - start; var offset = start - center;
            var a = direction.sqrMagnitude;
            var c = offset.sqrMagnitude - radius * radius;
            if (c <= 0 || a < .000001f) return start;
            var b = Vector2.Dot(offset, direction);
            var discriminant = b * b - a * c;
            if (discriminant < 0) return end;
            var t = (-b - Mathf.Sqrt(discriminant)) / a;
            return Vector2.Lerp(start, end, Mathf.Clamp01(t));
        }

        public void Tick(float dt)
        {
            for (var i = activeShots.Count - 1; i >= 0; i--)
                if (activeShots[i].Fx.Tick(dt)) { activeShots[i].Pool.Release(activeShots[i].Fx); activeShots.RemoveAt(i); }
            for (var i = activeImpacts.Count - 1; i >= 0; i--)
                if (activeImpacts[i].Fx.Tick(dt)) { activeImpacts[i].Pool.Release(activeImpacts[i].Fx); activeImpacts.RemoveAt(i); }
        }

        public void Clear()
        {
            foreach (var shot in activeShots) shot.Pool.Release(shot.Fx);
            foreach (var impact in activeImpacts) impact.Pool.Release(impact.Fx);
            activeShots.Clear(); activeImpacts.Clear();
        }

        public void SetLayerMask(int mask)
        {
            foreach (var shot in activeShots) shot.Fx.SetLayerMask(mask);
            foreach (var impact in activeImpacts) impact.Fx.SetLayerMask(mask);
        }
    }
}
