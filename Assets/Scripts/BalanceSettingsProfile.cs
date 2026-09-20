using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/BalanceSettings")]
public sealed class BalanceSettingsProfile : ScriptableObject {
        public float EnemyMoveStartMultiplier = .45f;
        public float EnemyMoveMultiplierPerPhase = .15f;
        public float EnemyMoveMultiplierMax = 2.2f;
        public float EnemyProjectileStartSpeed = 1.55f;
        public float EnemyProjectileSpeedPerPhase = .38f;
        public float EnemyProjectileSpeedMax = 6.5f;
        public float EnemyFireIntervalStart = 2.05f;
        public float EnemyFireIntervalReductionPerPhase = .085f;
        public float EnemyFireIntervalMin = .48f;
        public float SpawnIntervalStart = 1.32f;
        public float SpawnIntervalReductionPerPhase = .07f;
        public float SpawnIntervalMin = .35f;
        public float PlayerProjectileStartSpeed = 6.7f;
        public float PlayerProjectileSpeedUpgrade = 1.16f;
        public float PlayerFireIntervalStart = .30f;
        public float PlayerFireRateUpgrade = 1.18f;
        public float SplitShotIntervalMultiplier = .76f;
static BalanceSettingsProfile cached;
public static BalanceSettingsProfile Current => cached != null ? cached : cached = (Resources.Load<BalanceSettingsProfile>("Gameplay/BalanceSettings") ?? CreateInstance<BalanceSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
}
}
