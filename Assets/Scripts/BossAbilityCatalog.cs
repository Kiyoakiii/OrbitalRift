using System;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Player-facing spell cards for the three original boss encounters.
    /// The cards read the same assets as the encounter runtime.
    /// </summary>
    public enum BossAbilityId
    {
        FirebirdSolarChicks,
        FirebirdAshenEgg,
        FirebirdPhoenixDive,
        HarrierRiftCopies,
        HarrierPhaseDash,
        HarrierColdFan,
        VoidRiftBeam,
        VoidGravityRoots,
        VoidBarrage,
        FirebirdMainShot,
        HarrierMainShot,
        VoidMainShot,
        VoidCharge,
        VoidBlackHole,
        FirebirdSolarPlume,
        FirebirdEmberCore
    }

    [Serializable]
    public sealed class BossAbilityInfo
    {
        public BossAbilityId Id;
        public BossAbilityDefinition Asset;
        public string Name;
        public string ShortDescription;
        public string Timing;
        public float Cooldown;
        public float Duration;
        public string IconResource;
        public Color Accent;
        [NonSerialized] public float CachedCastDelay = -1, CachedAbilityCooldown = -1, CachedShotInterval = -1;
    }

    public static class BossAbilityCatalog
    {
        private static readonly System.Collections.Generic.Dictionary<BossDefinition, BossAbilityInfo[]> cache = new System.Collections.Generic.Dictionary<BossDefinition, BossAbilityInfo[]>();
        public static BossAbilityInfo[] For(BossArchetype archetype) => For(BossAssetRegistry.Get(archetype));
        public static BossAbilityInfo[] For(BossDefinition boss)
        {
            if (boss == null) return Array.Empty<BossAbilityInfo>();
            var count = 0;
            foreach (var ability in boss.Abilities) if (ability != null && ability.ShowInGuide) count++;
            if (!cache.TryGetValue(boss, out var infos) || infos.Length != count)
            {
                infos = new BossAbilityInfo[count];
                for (var i = 0; i < count; i++) infos[i] = new BossAbilityInfo();
                cache[boss] = infos;
            }
            var index = 0;
            foreach (var ability in boss.Abilities)
            {
                if (ability == null || !ability.ShowInGuide) continue;
                var info = infos[index++];
                var interval = ability.Shot != null ? ability.Shot.FireInterval : -1;
                var timingChanged = info.Asset != ability || info.Duration != ability.Duration || info.CachedCastDelay != ability.CastDelay ||
                    info.CachedAbilityCooldown != ability.Cooldown || info.CachedShotInterval != interval;
                info.Asset = ability; info.Id = ability.AbilityId; info.Name = ability.DisplayName;
                info.ShortDescription = ability.Description;
                info.Cooldown = ability.Shot != null ? ability.Shot.FireInterval : ability.Cooldown;
                info.Duration = ability.Duration; info.Accent = ability.Style.Primary;
                if (timingChanged) info.Timing = "подготовка " + ability.CastDelay.ToString("0.##") + " с · действие " + ability.Duration.ToString("0.##") + " с" +
                    (ability.Shot != null ? " · выстрел " + ability.Shot.FireInterval.ToString("0.##") + " с" : " · повтор " + ability.Cooldown.ToString("0.##") + " с");
                info.CachedCastDelay = ability.CastDelay; info.CachedAbilityCooldown = ability.Cooldown; info.CachedShotInterval = interval;
            }
            return infos;
        }
    }
}
