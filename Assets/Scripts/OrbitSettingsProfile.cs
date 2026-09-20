using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/OrbitSettings")]
public sealed class OrbitSettingsProfile : ScriptableObject {
        public float Radius = 3.2f;
        public float Diameter => Radius * 2f;
        public float LineWidth = .015f;
        public int Segments = 96;
        public int DashCount = 32;
        public float DashDuty = .55f;
static OrbitSettingsProfile cached;
public static OrbitSettingsProfile Current => cached != null ? cached : cached = (Resources.Load<OrbitSettingsProfile>("Gameplay/OrbitSettings") ?? CreateInstance<OrbitSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
}
}
