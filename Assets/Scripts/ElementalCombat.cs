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

        public static int ReactionBonus(ElementalReaction reaction)
        {
            switch (reaction)
            {
                case ElementalReaction.Ignition: return 3;
                case ElementalReaction.Cryotoxin: return 2;
                case ElementalReaction.Steam: return 1;
                case ElementalReaction.Shatter: return 4;
                case ElementalReaction.Overcharge: return 3;
                default: return 0;
            }
        }

        public static string ReactionLabel(ElementalReaction reaction)
        {
            switch (reaction)
            {
                case ElementalReaction.Ignition: return "ИГНИЦИЯ";
                case ElementalReaction.Cryotoxin: return "КРИОТОКСИН";
                case ElementalReaction.Steam: return "ПАР";
                case ElementalReaction.Shatter: return "РАСКОЛ";
                case ElementalReaction.Overcharge: return "ПЕРЕГРУЗКА";
                default: return "ОЖИДАНИЕ РЕЗОНАНСА";
            }
        }

        private static bool Pair(DamageElement first, DamageElement second, DamageElement a, DamageElement b)
        {
            return first == a && second == b || first == b && second == a;
        }
    }
}
