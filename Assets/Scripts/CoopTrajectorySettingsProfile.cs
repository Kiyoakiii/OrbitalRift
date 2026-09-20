using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/CoopTrajectorySettings")]public sealed class CoopTrajectorySettingsProfile:ScriptableObject {
public float HoldDuration=12f;
public float TransitionDuration=5f;
public float ArenaRadius=4.25f;
public float EllipseHorizontalScale=1.12f;
public float EllipseVerticalScale=.76f;
public float FigureEightHeightScale=.58f;
public float ThreatSpawnRadius=.55f;
public float CameraMargin=.55f;
public int LineSegments=160;
public float LineWidth=.026f;
static CoopTrajectorySettingsProfile cached;public static CoopTrajectorySettingsProfile Current=>cached!=null?cached:cached=(Resources.Load<CoopTrajectorySettingsProfile>("Gameplay/CoopTrajectorySettings")??CreateInstance<CoopTrajectorySettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>cached=null;
}}