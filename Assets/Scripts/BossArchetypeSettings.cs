namespace OrbitalRift
{
    /// <summary>
    /// Identity and numbers for the boss rotation.  The encounter logic stays
    /// in GameManager, but balance and the player-facing names have one home.
    /// </summary>
    public static class BossArchetypeSettings
    {
        // Development switch: when enabled, phases 1/2/3 are boss test waves.
        // Leave false for normal gameplay.
        public static bool DebugBossTestMode = false;
        // The broken-screen presentation stays implemented, but is opt-in while the
        // encounter readability is being tuned.
        public static bool MirrorBreakEnabled = false;
        public const int DebugBossWaveCount = 3;

        // Change this array to reorder the encounter rotation.
        public static readonly BossArchetype[] Rotation =
        {
            BossArchetype.AstralFirebird,
            BossArchetype.VoidMaw,
            BossArchetype.UmbralHarrier
        };

        public const float FirebirdHealth = 36f;
        public const float FirebirdEggHealth = 10f;
        public const float FirebirdEggDuration = 5f;
        public const float FirebirdReviveHealth = 36f;
        public const float HarrierHealth = 225f;

        public static BossArchetype ForBossOrdinal(int ordinal)
        {
            // The first fight remains familiar; every next completed three-wave
            // arc reveals a new creature before the rotation repeats.
            if (Rotation == null || Rotation.Length == 0) return BossArchetype.VoidMaw;
            var index = (ordinal - 1) % Rotation.Length;
            if (index < 0) index += Rotation.Length;
            return Rotation[index];
        }

        public static bool IsBossWave(int phase)
        {
            if (DebugBossTestMode && phase >= 1 && phase <= DebugBossWaveCount) return true;
            if (GameRules.Current != null && GameRules.Current.Classic != null) return GameRules.Current.Classic.Step(phase)?.Boss != null;
            return phase > 0 && phase % BossSettings.Phase == 0;
        }

        public static BossArchetype ForPhase(int phase)
        {
            if (!DebugBossTestMode && GameRules.Current != null && GameRules.Current.Classic != null) { var boss=GameRules.Current.Classic.Step(phase)?.Boss; if(boss!=null)return boss.Archetype; }
            var ordinal = DebugBossTestMode ? phase : phase / BossSettings.Phase;
            return ForBossOrdinal(ordinal < 1 ? 1 : ordinal);
        }

        public static float Health(BossArchetype archetype)
        {
            switch (archetype)
            {
                case BossArchetype.AstralFirebird: return FirebirdHealth;
                case BossArchetype.UmbralHarrier: return HarrierHealth;
                default: return BossSettings.Health;
            }
        }

        public static string Title(BossArchetype archetype)
        {
            switch (archetype)
            {
                case BossArchetype.AstralFirebird: return "ЖАР-ПТИЦА\nПЕПЕЛЬНАЯ КОРОНА";
                case BossArchetype.UmbralHarrier: return "ТЕНЕВОЙ ОХОТНИК\nКОПИИ РАЗЛОМА";
                default: return "ПАСТЬ БЕЗДНЫ\nПЕРВЫЙ РАЗЛОМ";
            }
        }
    }
}
