using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    public static class BossAssetRegistry
    {
        static Dictionary<BossArchetype, BossDefinition> bosses;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { bosses = null; }
        public static BossDefinition Get(BossArchetype type)
        {
            if (bosses == null)
            {
                bosses = new Dictionary<BossArchetype, BossDefinition>();
                foreach (var boss in Resources.LoadAll<BossDefinition>("Bosses")) if(boss.UseLegacyPresentation) bosses[boss.Archetype] = boss;
            }
            return bosses.TryGetValue(type, out var result) ? result : null;
        }
        public static BossAbilityDefinition Ability(BossAbilityId id)
        {
            foreach (var type in BossArchetypeSettings.Rotation)
            { var boss = Get(type); var ability = boss != null ? boss.Ability(id) : null; if (ability != null) return ability; }
            return null;
        }
    }
}
