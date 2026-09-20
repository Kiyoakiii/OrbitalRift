using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/MmrSettings")] public sealed class MmrSettingsProfile:ScriptableObject {
public int StartingMmr=25;
public int MinimumMmr=0;
public int NavigatorThreshold=0;
public int GuardianThreshold=1000;
public int LegendThreshold=2000;
public int OverlordThreshold=3000;
public int DivinityThreshold=4000;
public int MinimumGain=25;
public int MaximumGain=150;
public int MinimumLoss=15;
public int MaximumLoss=150;
static MmrSettingsProfile cached; public static MmrSettingsProfile Current=>cached!=null?cached:cached=(Resources.Load<MmrSettingsProfile>("Gameplay/MmrSettings")??CreateInstance<MmrSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>cached=null;
}}