namespace OrbitalRift
{
    // Параметры первого полноценного босса. Трогайте их для баланса,
    // не меняя логику состояний в GameManager.
    public static class BossSettings
    {
        public const int Phase = 3;
        // Deliberately generous while the boss/lens encounter is being debugged.
        // This keeps the room alive long enough to inspect every projectile path.
        public const float Health = 180f;
        public const int ExpeditionHealth = 180;
        public const int Points = 2500;
        public const float WorldSize = 1.48f;
        public const float OrbitRadius = 2.45f;
        public const float IntroDelay = 1.15f;
        // Более редкие и медленные атаки оставляют игроку пространство для манёвра.
        public const float AimBurstInterval = .95f;
        public const float BarrageInterval = .55f;
        public const float ChargeInterval = .72f;
        public const float ProjectileSpeedMultiplier = 1.05f;

        // "Взгляд разлома": короткая честная подготовка, затем медленный
        // вращающийся лазер.  Эти числа вынесены сюда, чтобы баланс босса не
        // был спрятан внутри GameManager.
        public static float BeamTelegraphDuration => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.CastDelay ?? 1.05f;
        public static float BeamSweepDuration => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Duration ?? 4.35f;
        public static float BeamAngularSpeed => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Beam.AngularSpeed ?? 27f;
        public static float BeamLength => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Beam.Length ?? 5.3f;
        public static float BeamInnerSafeRadius => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Beam.InnerSafeRadius ?? .32f;
        public static float BeamHalfWidthDegrees => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Beam.HalfWidthDegrees ?? 7.5f;
        public static float BeamHitCooldown => BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Beam.HitInterval ?? .72f;

        public static float RootTelegraphDuration => BossAssetRegistry.Ability(BossAbilityId.VoidGravityRoots)?.CastDelay ?? OrbitalAbilitySettings.RootTelegraphDuration;
        public static float RootLockDuration => BossAssetRegistry.Ability(BossAbilityId.VoidGravityRoots)?.Duration ?? OrbitalAbilitySettings.RootVisualDuration;

        public static int BeamCount(float healthRatio)
        {
            // Phase two removes one safe lane, but never turns the pattern into
            // unavoidable damage: the player can always rotate through a gap.
            return BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)?.Beam.Number(healthRatio) ?? (healthRatio <= .50f ? 3 : 2);
        }

        // Резисты никогда не равны нулю: неподходящий корабль наносит меньше
        // урона, но не становится бесполезным. Текущая кинетика сохраняет
        // прежний баланс одиночной версии.
        public static float Resistance(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Fire: return .65f;
                case DamageElement.Cold: return 1.35f;
                case DamageElement.Poison: return .80f;
                default: return 1f;
            }
        }

        public static DamageElement AttackElement(BossAiState state)
        {
            switch (state)
            {
                case BossAiState.Barrage: return DamageElement.Poison;
                case BossAiState.Charge: return DamageElement.Fire;
                default: return DamageElement.Cold;
            }
        }
    }
}
