using System;
using UnityEngine;
namespace OrbitalRift {
    [CreateAssetMenu(menuName="Orbital Rift/Gameplay/Game rules")]
    public sealed class GameRules : ScriptableObject
    {
        public EncounterSequence Classic, Defense;
        [Min(1)] public int PlayerStartingHull=3;
        public float SplitShotDuration=6, TouchOrbitSpeed=3.4f;
        [Min(0)] public float PlayerBaseDamage=1, HostileBaseDamage=1;
        [Min(.01f)] public float DefaultProjectileLifetime=3, PlayerHitInvulnerability=1;
        public float DefenseFlagshipHitRadius=.27f, DefenseFlagshipHalfWidth=2.98f, DefenseFlagshipWorldSize=6.35f;
        public MobDefinition[] Mobs=Array.Empty<MobDefinition>();
        [Min(1)] public int EnemyCapBase=4, EnemyCapPerPhase=2, EnemyCapMax=18, ProjectileCap=80;
        [Min(1)] public int DefenseHull=9, DefenseRepairEvery=3;
        public float DefenseIntermission=1.5f, DefenseSpawnInterval=.72f, DefenseSpawnReduction=.024f, DefenseSpawnMinimum=.26f;
        static GameRules cached;
        public static GameRules Current => cached != null ? cached : cached=Resources.Load<GameRules>("Gameplay/GameRules");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
    }
}
