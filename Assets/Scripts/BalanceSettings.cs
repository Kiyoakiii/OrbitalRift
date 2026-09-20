using UnityEngine;

namespace OrbitalRift
{
    // Центральная точка настройки темпа игры. Начальные значения намеренно мягкие,
    // а каждое прохождение варп-фазы повышает угрозу и один из параметров корабля.
    public static class BalanceSettings
    {
        public static float EnemyMoveStartMultiplier => BalanceSettingsProfile.Current.EnemyMoveStartMultiplier;
        public static float EnemyMoveMultiplierPerPhase => BalanceSettingsProfile.Current.EnemyMoveMultiplierPerPhase;
        public static float EnemyMoveMultiplierMax => BalanceSettingsProfile.Current.EnemyMoveMultiplierMax;

        public static float EnemyProjectileStartSpeed => BalanceSettingsProfile.Current.EnemyProjectileStartSpeed;
        public static float EnemyProjectileSpeedPerPhase => BalanceSettingsProfile.Current.EnemyProjectileSpeedPerPhase;
        public static float EnemyProjectileSpeedMax => BalanceSettingsProfile.Current.EnemyProjectileSpeedMax;
        public static float EnemyFireIntervalStart => BalanceSettingsProfile.Current.EnemyFireIntervalStart;
        public static float EnemyFireIntervalReductionPerPhase => BalanceSettingsProfile.Current.EnemyFireIntervalReductionPerPhase;
        public static float EnemyFireIntervalMin => BalanceSettingsProfile.Current.EnemyFireIntervalMin;

        public static float SpawnIntervalStart => BalanceSettingsProfile.Current.SpawnIntervalStart;
        public static float SpawnIntervalReductionPerPhase => BalanceSettingsProfile.Current.SpawnIntervalReductionPerPhase;
        public static float SpawnIntervalMin => BalanceSettingsProfile.Current.SpawnIntervalMin;

        public static float PlayerProjectileStartSpeed => BalanceSettingsProfile.Current.PlayerProjectileStartSpeed;
        public static float PlayerProjectileSpeedUpgrade => BalanceSettingsProfile.Current.PlayerProjectileSpeedUpgrade;
        public static float PlayerFireIntervalStart => BalanceSettingsProfile.Current.PlayerFireIntervalStart;
        public static float PlayerFireRateUpgrade => BalanceSettingsProfile.Current.PlayerFireRateUpgrade;
        public static float SplitShotIntervalMultiplier => BalanceSettingsProfile.Current.SplitShotIntervalMultiplier;

        // Фаза 2, 4, 6… — скорострельность; фаза 3, 5, 7… — скорость заряда.
        public static int FireRateTier(int phase) => Mathf.Max(0, phase / 2);
        public static int ProjectileSpeedTier(int phase) => Mathf.Max(0, (phase - 1) / 2);

        public static float EnemyMovementMultiplier(int phase) => Mathf.Min(
            EnemyMoveMultiplierMax,
            EnemyMoveStartMultiplier + Mathf.Max(0, phase - 1) * EnemyMoveMultiplierPerPhase);

        public static float EnemyProjectileSpeed(int phase) => Mathf.Min(
            EnemyProjectileSpeedMax,
            EnemyProjectileStartSpeed + Mathf.Max(0, phase - 1) * EnemyProjectileSpeedPerPhase);

        public static float EnemyFireInterval(int phase) => Mathf.Max(
            EnemyFireIntervalMin,
            EnemyFireIntervalStart - Mathf.Max(0, phase - 1) * EnemyFireIntervalReductionPerPhase);

        public static float SpawnInterval(int phase) => Mathf.Max(
            SpawnIntervalMin,
            SpawnIntervalStart - Mathf.Max(0, phase - 1) * SpawnIntervalReductionPerPhase);

        public static float PlayerProjectileSpeed(int phase) =>
            PlayerProjectileStartSpeed * Mathf.Pow(PlayerProjectileSpeedUpgrade, ProjectileSpeedTier(phase));

        public static float PlayerFireInterval(int phase, bool splitShot) =>
            PlayerFireIntervalStart / Mathf.Pow(PlayerFireRateUpgrade, FireRateTier(phase)) *
            (splitShot ? SplitShotIntervalMultiplier : 1f);
    }
}
