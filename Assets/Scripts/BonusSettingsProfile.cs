using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/BonusSettings")]
public sealed class BonusSettingsProfile : ScriptableObject {
        public float Speed = 1.9f;
        public float Homing = 1.8f;
        public float Lifetime = 7f;
        public float RotationSpeed = 160f;
static BonusSettingsProfile cached;
public static BonusSettingsProfile Current => cached != null ? cached : cached = (Resources.Load<BonusSettingsProfile>("Gameplay/BonusSettings") ?? CreateInstance<BonusSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
}
}
