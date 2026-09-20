using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/GameplayCameraZoomSettings")]public sealed class GameplayCameraZoomSettingsProfile:ScriptableObject {
public float Default=1f;
public float Minimum=1f;
public float Maximum=1.6f;
public float Step=.1f;
static GameplayCameraZoomSettingsProfile cached;public static GameplayCameraZoomSettingsProfile Current=>cached!=null?cached:cached=(Resources.Load<GameplayCameraZoomSettingsProfile>("Gameplay/GameplayCameraZoomSettings")??CreateInstance<GameplayCameraZoomSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>cached=null;
}}