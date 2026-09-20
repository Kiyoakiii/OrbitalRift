using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/PairedLensRules")]public sealed class PairedLensRulesProfile:ScriptableObject {
public float LensRadius=.48f;
public float PlayerShotRadius=.065f;
public float RelayCoreRadius=.26f;
public float ExitEpsilon=.035f;
public float PlayerShotCooldown=.075f;
public float RelayCoreCooldown=.80f;
public int MaxPasses=2;
public float TrajectoryClearance=.24f + .50f;
static PairedLensRulesProfile cached;public static PairedLensRulesProfile Current=>cached!=null?cached:cached=(Resources.Load<PairedLensRulesProfile>("Gameplay/PairedLensRules")??CreateInstance<PairedLensRulesProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>cached=null;
}}