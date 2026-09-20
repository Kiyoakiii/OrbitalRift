using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/OrbitalAbilitySettings")]
public sealed class OrbitalAbilitySettingsProfile : ScriptableObject {
        public float EchoCooldown = 7.5f;
        public float EchoDuration = 3.25f;
        public float EchoFireInterval = .24f;
        public float EchoDamage = 1.35f;
        public float EchoProjectileSpeed = 7.3f;
        public float EchoOrbitLeadDegrees = 48f;
        public float VectorSnapCooldown = 14f;
        public float VectorSnapDegrees = 86f;
        public float VectorSnapInvulnerability = .28f;
        public float RootTelegraphDuration = 1.05f;
        public float RootHoldDuration = 1.0f;
        public float RootVisualDuration = .66f;
        public float RootCatchHalfWidthDegrees = 13.5f;
static OrbitalAbilitySettingsProfile cached;
public static OrbitalAbilitySettingsProfile Current => cached != null ? cached : cached = (Resources.Load<OrbitalAbilitySettingsProfile>("Gameplay/OrbitalAbilitySettings") ?? CreateInstance<OrbitalAbilitySettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
}
}
