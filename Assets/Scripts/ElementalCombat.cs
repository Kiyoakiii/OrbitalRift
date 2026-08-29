using UnityEngine;

namespace OrbitalRift
{
    public enum DamageElement : byte
    {
        Kinetic,
        Fire,
        Cold,
        Poison
    }

    public enum ElementalReaction : byte
    {
        None,
        Ignition,
        Cryotoxin,
        Steam,
        Shatter,
        Overcharge
    }

    /// <summary>Shared rules for ships, bosses, hazards and future procedural rooms.</summary>
    public static class ElementalCombat
    {
        public static float ApplyResistance(float rawDamage, float multiplier)
        {
            return Mathf.Max(0f, rawDamage) * Mathf.Clamp(multiplier, .25f, 2f);
        }

        public static ElementalReaction ResolveReaction(DamageElement first, DamageElement second)
        {
            if (first == second)
                return first == DamageElement.Kinetic ? ElementalReaction.None : ElementalReaction.Overcharge;
            if (Pair(first, second, DamageElement.Fire, DamageElement.Poison)) return ElementalReaction.Ignition;
            if (Pair(first, second, DamageElement.Cold, DamageElement.Poison)) return ElementalReaction.Cryotoxin;
            if (Pair(first, second, DamageElement.Fire, DamageElement.Cold)) return ElementalReaction.Steam;
            if (Pair(first, second, DamageElement.Cold, DamageElement.Kinetic)) return ElementalReaction.Shatter;
            return ElementalReaction.None;
        }

        public static string ShortName(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Fire: return "ОГОНЬ";
                case DamageElement.Cold: return "ХОЛОД";
                case DamageElement.Poison: return "ЯД";
                default: return "КИНЕТИКА";
            }
        }

        private static bool Pair(DamageElement first, DamageElement second, DamageElement a, DamageElement b)
        {
            return first == a && second == b || first == b && second == a;
        }
    }
}
