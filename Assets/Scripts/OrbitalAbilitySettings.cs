namespace OrbitalRift
{
    /// <summary>
    /// Small, deliberately visible first ability kit.  Keeping the numbers out
    /// of GameManager makes the first combat slice easy to tune on a phone.
    /// </summary>
    public static class OrbitalAbilitySettings
    {
        public static float EchoCooldown => OrbitalAbilitySettingsProfile.Current.EchoCooldown;
        public static float EchoDuration => OrbitalAbilitySettingsProfile.Current.EchoDuration;
        public static float EchoFireInterval => OrbitalAbilitySettingsProfile.Current.EchoFireInterval;
        public static float EchoDamage => OrbitalAbilitySettingsProfile.Current.EchoDamage;
        public static float EchoProjectileSpeed => OrbitalAbilitySettingsProfile.Current.EchoProjectileSpeed;
        public static float EchoOrbitLeadDegrees => OrbitalAbilitySettingsProfile.Current.EchoOrbitLeadDegrees;

        public static float VectorSnapCooldown => OrbitalAbilitySettingsProfile.Current.VectorSnapCooldown;
        public static float VectorSnapDegrees => OrbitalAbilitySettingsProfile.Current.VectorSnapDegrees;
        public static float VectorSnapInvulnerability => OrbitalAbilitySettingsProfile.Current.VectorSnapInvulnerability;

        public static float RootTelegraphDuration => OrbitalAbilitySettingsProfile.Current.RootTelegraphDuration;
        public static float RootHoldDuration => OrbitalAbilitySettingsProfile.Current.RootHoldDuration;
        public static float RootVisualDuration => OrbitalAbilitySettingsProfile.Current.RootVisualDuration;
        public static float RootCatchHalfWidthDegrees => OrbitalAbilitySettingsProfile.Current.RootCatchHalfWidthDegrees;
    }
}
