namespace OrbitalRift
{
    // Параметры первого полноценного босса. Трогайте их для баланса,
    // не меняя логику состояний в GameManager.
    public static class BossSettings
    {
        public const int Phase = 3;
        public const float Health = 42f;
        public const int Points = 2500;
        public const float WorldSize = 1.48f;
        public const float OrbitRadius = 2.45f;
        public const float IntroDelay = 1.15f;
        // Более редкие и медленные атаки оставляют игроку пространство для манёвра.
        public const float AimBurstInterval = .95f;
        public const float BarrageInterval = .55f;
        public const float ChargeInterval = .72f;
        public const float ProjectileSpeedMultiplier = 1.05f;

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
