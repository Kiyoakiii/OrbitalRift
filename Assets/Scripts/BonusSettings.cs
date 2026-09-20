namespace OrbitalRift
{
    // Настройки полёта бонуса от центра к кораблю.
    public static class BonusSettings
    {
        public static float Speed => BonusSettingsProfile.Current.Speed;
        public static float Homing => BonusSettingsProfile.Current.Homing;
        public static float Lifetime => BonusSettingsProfile.Current.Lifetime;
        public static float RotationSpeed => BonusSettingsProfile.Current.RotationSpeed;
    }
}
