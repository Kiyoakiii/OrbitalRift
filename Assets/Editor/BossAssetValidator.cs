#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrbitalRift
{
    public static class BossAssetValidator
    {
        public static void Validate(List<string> errors)
        {
            var bosses = Resources.LoadAll<BossDefinition>("Bosses");
            var ids = new HashSet<string>();
            var abilities = new HashSet<BossAbilityId>();
            foreach (var boss in bosses)
            {
                if (!ids.Add(boss.BossId)) errors.Add("Duplicate boss archetype: " + boss.name);
                if (boss.Prefab == null || (boss.Sprite == null && !boss.Appearance.Enabled) || boss.InitialAbility == null || boss.MaxHp <= 0 || boss.HitRadius <= 0)
                    errors.Add("Boss prefab, sprite, initial ability, HP and hit radius are required: " + boss.name);
                if (boss.Phases.Length == 0) errors.Add("Boss phases are missing: " + boss.name);
                foreach (var shot in boss.Shots)
                {
                    if (shot == null) { errors.Add("Missing shot in " + boss.name); continue; }
                    if (shot.VfxPrefab != null && shot.Vfx == null)
                        errors.Add("Shot prefab/VFX/impact link is missing: " + shot.name);
                    if (shot.Speed <= 0 || shot.Lifetime <= 0 || shot.FireInterval <= 0 || shot.ProjectileCount < 1 || shot.Damage < 0)
                        errors.Add("Invalid shot gameplay values: " + shot.name);
                    CheckPrefab(shot.Prefab != null ? shot.Prefab.gameObject : null, errors);
                    CheckPrefab(shot.VfxPrefab != null ? shot.VfxPrefab.gameObject : null, errors);
                    CheckPrefab(shot.ImpactPrefab != null ? shot.ImpactPrefab.gameObject : null, errors);
                }
                foreach (var ability in boss.Abilities)
                {
                    if (ability == null) { errors.Add("Missing ability in " + boss.name); continue; }
                    // Shared spell assets may be assigned to multiple bosses.
                    if (ability.Duration <= 0 || ability.Cooldown < 0 || ability.CastDelay < 0 || ((ability.CastPrefab != null || ability.AftereffectPrefab != null) && ability.Vfx == null))
                        errors.Add("Invalid ability timing or missing VFX: " + ability.name);
                    if (ability.Shot != null && System.Array.IndexOf(boss.Shots, ability.Shot) < 0)
                        errors.Add("Ability shot is not in the boss arsenal: " + ability.name);
                    if (ability.SecondaryAbility != null && System.Array.IndexOf(boss.Abilities, ability.SecondaryAbility) < 0)
                        errors.Add("Secondary ability is not listed in the boss: " + ability.name);
                    if (ability.Behaviour == BossAbilityBehaviour.Summon && (ability.Summon.Prefab == null || ability.Summon.Sprite == null || ability.Summon.Shot == null || ability.Summon.Health <= 0 || ability.Summon.Lifetime <= 0))
                        errors.Add("Summon prefab, sprite, shot, HP and lifetime are required: " + ability.name);
                    if (ability.Behaviour == BossAbilityBehaviour.RebirthEgg && (ability.Egg.Health <= 0 || ability.Egg.ReviveHealth <= 0))
                        errors.Add("Egg and rebirth HP must be positive: " + ability.name);
                    if (ability.Behaviour == BossAbilityBehaviour.Beam && (ability.Beam.Length <= ability.Beam.InnerSafeRadius || ability.Beam.HitInterval <= 0 || ability.Beam.Count < 1 || ability.Beam.Count > 8 || ability.Beam.EnragedCount > 8))
                        errors.Add("Invalid beam geometry/count/timing: " + ability.name);
                }
                var threshold = -1f;
                foreach (var phase in boss.Phases)
                {
                    if (phase.MaxHealthFraction < threshold) errors.Add("Phase HP thresholds must be ascending: " + boss.name);
                    threshold = phase.MaxHealthFraction;
                    foreach (var entry in phase.Attacks)
                        if (entry.Ability == null || entry.Ability.SandboxOnly || System.Array.IndexOf(boss.Abilities, entry.Ability) < 0 || entry.DurationMultiplier <= 0)
                            errors.Add("Invalid phase ability reference/duration: " + boss.name + "/" + phase.Name);
                }
                CheckPrefab(boss.Prefab != null ? boss.Prefab.gameObject : null, errors);
            }
            foreach (var type in BossArchetypeSettings.Rotation) if (BossAssetRegistry.Get(type)==null) errors.Add("Missing boss asset: " + type);
        }
        static void CheckPrefab(GameObject prefab, List<string> errors)
        {
            if (prefab == null) return;
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    errors.Add("Missing script in prefab: " + prefab.name);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader)) errors.Add("Missing/broken material: " + prefab.name);
        }
    }
}
#endif
