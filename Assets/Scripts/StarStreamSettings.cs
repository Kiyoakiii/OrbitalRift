namespace OrbitalRift
{
    // All visual controls for the white star stream live here.
    public static class StarStreamSettings
    {
        public static int BackgroundStarCount => StarStreamSettingsProfile.Current.BackgroundStarCount;
        public static float BackgroundStarBrightness => StarStreamSettingsProfile.Current.BackgroundStarBrightness;
        // Main white stream density. Raise this to make the travelling star field denser.
        public static float StarsPerSecond => StarStreamSettingsProfile.Current.StarsPerSecond;
        // First-frame seeding fills the arena immediately while later stars still emerge from
        // the central rift ring, so a new run never looks like an empty centre-only fountain.
        public static int InitialFieldStarCount => StarStreamSettingsProfile.Current.InitialFieldStarCount;
        public static float StreamBrightness => StarStreamSettingsProfile.Current.StreamBrightness;
        // Прозрачность каждой звезды выбирается отдельно при вылете.
        // 0 = полностью прозрачно, 1 = полностью ярко.
        // Most stream stars begin as understated pinpoints. A selected bright subset earns its
        // full head and opacity only while leaving the screen, selling that the player flies
        // past it instead of watching a uniform particle fountain.
        public static float StreamAlphaMin => StarStreamSettingsProfile.Current.StreamAlphaMin;
        public static float StreamAlphaMax => StarStreamSettingsProfile.Current.StreamAlphaMax;
        public static float BaseSpeed => StarStreamSettingsProfile.Current.BaseSpeed;
        public static float SpeedMultiplierMin => StarStreamSettingsProfile.Current.SpeedMultiplierMin;
        public static float SpeedMultiplierMax => StarStreamSettingsProfile.Current.SpeedMultiplierMax;
        public static float MinSize => StarStreamSettingsProfile.Current.MinSize;
        public static float MaxSize => StarStreamSettingsProfile.Current.MaxSize;
        public static float EdgeFlybyChance => StarStreamSettingsProfile.Current.EdgeFlybyChance;
        // Fly-bys start growing well before the camera edge, with a broad soft halo.
        public static float EdgeFlybyStart => StarStreamSettingsProfile.Current.EdgeFlybyStart;
        public static float EdgeFlybyMaxScale => StarStreamSettingsProfile.Current.EdgeFlybyMaxScale;
        public static float EdgeFlybyAlphaBoost => StarStreamSettingsProfile.Current.EdgeFlybyAlphaBoost;
        public static float StreamSpawnRadiusMin => StarStreamSettingsProfile.Current.StreamSpawnRadiusMin;
        public static float StreamSpawnRadiusMax => StarStreamSettingsProfile.Current.StreamSpawnRadiusMax;
        public static float InitialFieldRadiusMin => StarStreamSettingsProfile.Current.InitialFieldRadiusMin;
        public static float InitialFieldRadiusMax => StarStreamSettingsProfile.Current.InitialFieldRadiusMax;
        public static float FlybyHaloBaseScale => StarStreamSettingsProfile.Current.FlybyHaloBaseScale;
        public static float FlybyHaloMaxScale => StarStreamSettingsProfile.Current.FlybyHaloMaxScale;
        public static float MinLifetime => StarStreamSettingsProfile.Current.MinLifetime;
        public static float MaxLifetime => StarStreamSettingsProfile.Current.MaxLifetime;
        public static float TrailLength => StarStreamSettingsProfile.Current.TrailLength;
        public static float TrailWidth => StarStreamSettingsProfile.Current.TrailWidth;
        public static float TrailFade => StarStreamSettingsProfile.Current.TrailFade;
        // Perspective trails: distant pinpoints stay short; nearby fly-bys stretch out.
        public static float FarTrailSeconds => StarStreamSettingsProfile.Current.FarTrailSeconds;
        public static float NearTrailSeconds => StarStreamSettingsProfile.Current.NearTrailSeconds;
        public static float FarTrailWidth => StarStreamSettingsProfile.Current.FarTrailWidth;
        public static float NearTrailWidth => StarStreamSettingsProfile.Current.NearTrailWidth;
        public static float CombatTravelSpeed => StarStreamSettingsProfile.Current.CombatTravelSpeed; // 0 = full stop, 1 = cruise speed.
        public static float JumpTravelSpeed => StarStreamSettingsProfile.Current.JumpTravelSpeed;
        public static float JumpDuration => StarStreamSettingsProfile.Current.JumpDuration;
        public static float ScreenEdgeSlowStart => StarStreamSettingsProfile.Current.ScreenEdgeSlowStart;
        public static float ScreenEdgeSpeedMultiplier => StarStreamSettingsProfile.Current.ScreenEdgeSpeedMultiplier;
        // Редкие фиолетовые звёзды дают усиленный solid-щит.
        public static float PurpleChance => StarStreamSettingsProfile.Current.PurpleChance;
        public static float ShieldTrailLength => StarStreamSettingsProfile.Current.ShieldTrailLength;
        public static float ShieldTrailWidth => StarStreamSettingsProfile.Current.ShieldTrailWidth;

        // Static sky stars now form a slow, trail-less parallax field behind the arena.
        public static float BackgroundTravelSpeed => StarStreamSettingsProfile.Current.BackgroundTravelSpeed;
        public static float BackgroundMinRadius => StarStreamSettingsProfile.Current.BackgroundMinRadius;
        public static float BackgroundMaxRadius => StarStreamSettingsProfile.Current.BackgroundMaxRadius;
    }
}
