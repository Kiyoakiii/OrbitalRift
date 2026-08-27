namespace OrbitalRift
{
    // All visual controls for the white star stream live here.
    public static class StarStreamSettings
    {
        public const int BackgroundStarCount = 100;
        public const float BackgroundStarBrightness = .7f;
        public const float StarsPerSecond = 22f;
        public const float StreamBrightness = 1f;
        public const float BaseSpeed = 1.8f;
        public const float SpeedMultiplierMin = 1.3f;
        public const float SpeedMultiplierMax = 1.8f;
        public const float MinSize = .022f;
        public const float MaxSize = .09f;
        public const float MinLifetime = 4.8f;
        public const float MaxLifetime = 6.8f;
        public const float TrailLength = .82f;
        public const float TrailWidth = .05f;
        public const float TrailFade = .3f; // 0..1, brightness at the head before it fades to zero.
        // При приближении к краю видимого экрана поток плавно замедляется.
        public const float ScreenEdgeSlowStart = .66f;
        public const float ScreenEdgeSpeedMultiplier = .28f;
    }
}
