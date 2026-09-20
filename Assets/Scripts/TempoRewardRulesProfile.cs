using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/TempoRewardRules")]public sealed class TempoRewardRulesProfile:ScriptableObject {
public int ModuleDuration=2;
public int MaximumModules=2;
public float FireRateMultiplier=1.12f;
public float ProjectileSpeedMultiplier=1.18f;
public float MinimumFireInterval=.08f;
static TempoRewardRulesProfile cached;public static TempoRewardRulesProfile Current=>cached!=null?cached:cached=(Resources.Load<TempoRewardRulesProfile>("Gameplay/TempoRewardRules")??CreateInstance<TempoRewardRulesProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>cached=null;
}}