using UnityEngine;

namespace OrbitalRift
{
    public enum ShipArchetype : byte
    {
        Vanguard,
        Interceptor,
        Pyre,
        Alchemist
    }

    public readonly struct ShipLoadout
    {
        public readonly ShipArchetype Archetype;
        public readonly DamageElement Element;
        public readonly float FireIntervalMultiplier;
        public readonly float ProjectileSpeedMultiplier;
        public readonly float DamageMultiplier;
        public readonly Color ProjectileColor;

        public ShipLoadout(ShipArchetype archetype, DamageElement element, float fireIntervalMultiplier,
            float projectileSpeedMultiplier, float damageMultiplier, Color projectileColor)
        {
            Archetype = archetype;
            Element = element;
            FireIntervalMultiplier = fireIntervalMultiplier;
            ProjectileSpeedMultiplier = projectileSpeedMultiplier;
            DamageMultiplier = damageMultiplier;
            ProjectileColor = projectileColor;
        }
    }

    public static class ShipLoadoutSettings
    {
        public const string PlayerPrefsKey = "orbital_rift_ship_archetype";
        public const int Count = 4;

        public static ShipLoadout Get(ShipArchetype archetype)
        {
            switch (archetype)
            {
                case ShipArchetype.Interceptor:
                    return new ShipLoadout(archetype, DamageElement.Cold, .78f, 1.25f, .75f, new Color(.48f, .9f, 1f));
                case ShipArchetype.Pyre:
                    return new ShipLoadout(archetype, DamageElement.Fire, .95f, 1.05f, 1f, new Color(1f, .42f, .16f));
                case ShipArchetype.Alchemist:
                    return new ShipLoadout(archetype, DamageElement.Poison, 1.12f, .92f, 1.18f, new Color(.34f, 1f, .42f));
                default:
                    // Default loadout intentionally preserves the original solo balance.
                    return new ShipLoadout(ShipArchetype.Vanguard, DamageElement.Kinetic, 1f, 1f, 1f, new Color(.65f, 1f, 1f));
            }
        }

        public static ShipArchetype Clamp(int value)
        {
            return (ShipArchetype)Mathf.Clamp(value, 0, Count - 1);
        }

        public static string Title(ShipArchetype archetype)
        {
            switch (archetype)
            {
                case ShipArchetype.Interceptor: return "ПЕРЕХВАТ";
                case ShipArchetype.Pyre: return "ПИРО";
                case ShipArchetype.Alchemist: return "ХИМИК";
                default: return "ШТУРМ";
            }
        }
    }
}
