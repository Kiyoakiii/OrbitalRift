using UnityEngine;

namespace OrbitalRift
{
    // Центральная точка настройки темпа игры. Начальные значения намеренно мягкие,
    // а каждое прохождение варп-фазы повышает угрозу и один из параметров корабля.
    public static class BalanceSettings
    {
        public const float EnemyMoveStartMultiplier = .45f;
        public const float EnemyMoveMultiplierPerPhase = .15f;
        public const float EnemyMoveMultiplierMax = 2.2f;

        public const float EnemyProjectileStartSpeed = 1.55f;
        public const float EnemyProjectileSpeedPerPhase = .38f;
        public const float EnemyProjectileSpeedMax = 6.5f;
        public const float EnemyFireIntervalStart = 2.05f;
        public const float EnemyFireIntervalReductionPerPhase = .085f;
        public const float EnemyFireIntervalMin = .48f;

        public const float SpawnIntervalStart = 1.32f;
        public const float SpawnIntervalReductionPerPhase = .07f;
        public const float SpawnIntervalMin = .35f;

        public const float PlayerProjectileStartSpeed = 6.7f;
        public const float PlayerProjectileSpeedUpgrade = 1.16f;
        public const float PlayerFireIntervalStart = .30f;
        public const float PlayerFireRateUpgrade = 1.18f;
        public const float SplitShotIntervalMultiplier = .76f;

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
