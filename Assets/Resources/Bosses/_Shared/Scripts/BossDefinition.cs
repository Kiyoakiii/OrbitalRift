using System;
using UnityEngine;

namespace OrbitalRift
{
    [CreateAssetMenu(menuName = "Orbital Rift/Bosses/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        public ActorAppearance Appearance = new ActorAppearance();
        [Header("Босс · точка входа")]
        public string BossId;
        [Tooltip("Отключи для нового босса со своим PNG; готовые особые эффекты трёх боссов отключатся.")] public bool UseLegacyPresentation = true;
        public BossArchetype Archetype;
        public string DisplayName;
        [Tooltip("Объект Enemy из пула. Sprite и параметры ниже применяются при появлении.")]
        public Enemy Prefab;
        public Sprite Sprite;
        [Min(1)] public float MaxHp = 100;
        [Min(0)] public int Points = 2500;
        [Min(.01f)] public float WorldSize = 1.48f;
        [Min(.01f)] public float HitRadius = .8436f;
        [Header("Появление / движение")]
        [Min(0)] public float IntroDelay = 1.15f;
        [Min(0)] public float OrbitRadius = 2.45f;
        [Min(0)] public float InitialStateDuration = 2.4f;
        public BossAbilityDefinition InitialAbility;
        [Header("Арсенал · открой ссылку для настройки")]
        public BossShotDefinition[] Shots = Array.Empty<BossShotDefinition>();
        public BossAbilityDefinition[] Abilities = Array.Empty<BossAbilityDefinition>();
        [Tooltip("Проверяются сверху вниз. Первая фаза, чей порог HP >= текущего HP, становится активной. Расположи меньшие пороги выше.")]
        public BossPhaseDefinition[] Phases = Array.Empty<BossPhaseDefinition>();
        [Header("Внешность / сопротивления")]
        public BossVfxProfile Presentation;
        [Tooltip("Быстрые ссылки на фактически используемые профили выстрелов и способностей.")]
        public SpellVfxProfile[] VfxProfiles = Array.Empty<SpellVfxProfile>();
        public BossEffectStyle Theme = new BossEffectStyle();
        [Range(.05f, 3)] public float KineticResistance = 1;
        [Range(.05f, 3)] public float FireResistance = 1;
        [Range(.05f, 3)] public float ColdResistance = 1;
        [Range(.05f, 3)] public float PoisonResistance = 1;

        public float Resistance(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Fire: return FireResistance;
                case DamageElement.Cold: return ColdResistance;
                case DamageElement.Poison: return PoisonResistance;
                default: return KineticResistance;
            }
        }
        public BossPhaseDefinition Phase(float hp01)
        {
            foreach (var phase in Phases) if (hp01 <= phase.MaxHealthFraction) return phase;
            return Phases.Length > 0 ? Phases[Phases.Length - 1] : null;
        }
        public BossAbilityDefinition Ability(BossAbilityId id)
        {
            foreach (var ability in Abilities) if (ability != null && ability.AbilityId == id) return ability;
            return null;
        }
        public BossAbilityDefinition Ability(BossAbilityBehaviour behaviour)
        {
            foreach (var ability in Abilities) if (ability != null && ability.Behaviour == behaviour) return ability;
            return null;
        }
    }

    [Serializable]
    public sealed class BossPhaseDefinition
    {
        public string Name;
        [Range(0, 1)] public float MaxHealthFraction = 1;
        [Tooltip("Порядок важен: Chance — шанс выбора при достижении строки, последняя строка обычно имеет шанс 1.")]
        public BossAttackEntry[] Attacks = Array.Empty<BossAttackEntry>();
    }
    [Serializable]
    public sealed class BossAttackEntry
    {
        public BossAbilityDefinition Ability;
        [Range(0, 1)] public float Chance = 1;
        [Min(.01f)] public float DurationMultiplier = 1;
    }
}
