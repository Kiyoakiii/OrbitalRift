namespace OrbitalRift
{
    // All visual controls for the white star stream live here.
    public static class StarStreamSettings
    {
        public const int BackgroundStarCount = 100;
        public const float BackgroundStarBrightness = .7f;
        public const float StarsPerSecond = 18f;
        public const float StreamBrightness = 1f;
        // Прозрачность каждой звезды выбирается отдельно при вылете.
        // 0 = полностью прозрачно, 1 = полностью ярко.
        public const float StreamAlphaMin = .05f;
        public const float StreamAlphaMax = 1f;
        public const float BaseSpeed = 1.8f;
        public const float SpeedMultiplierMin = 1.3f;
        public const float SpeedMultiplierMax = 1.8f;
        public const float MinSize = .022f;
        public const float MaxSize = .09f;
        public const float MinLifetime = 4.8f;
        public const float MaxLifetime = 6.8f;
        public const float TrailLength = .82f;
        public const float TrailWidth = .05f;
        public const float TrailFade = .3f;
        public const float ScreenEdgeSlowStart = .66f;
        public const float ScreenEdgeSpeedMultiplier = .28f;
        // Редкие фиолетовые звёзды дают усиленный solid-щит.
        public const float PurpleChance = .03f;
        public const float ShieldTrailLength = .34f;
        public const float ShieldTrailWidth = .065f;
    }
}
