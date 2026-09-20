using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/GameAudioSettings")] public sealed class GameAudioSettingsProfile:ScriptableObject {
public float MusicVolume=.42f;
public float EffectsVolume=.70f;
static GameAudioSettingsProfile cached; public static GameAudioSettingsProfile Current=>cached!=null?cached:cached=(Resources.Load<GameAudioSettingsProfile>("Gameplay/GameAudioSettings")??CreateInstance<GameAudioSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>cached=null;
}}