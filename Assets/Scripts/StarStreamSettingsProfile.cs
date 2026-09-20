using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/StarStreamSettings")]
public sealed class StarStreamSettingsProfile : ScriptableObject {
        public int BackgroundStarCount = 560;
        public float BackgroundStarBrightness = .7f;
        public float StarsPerSecond = 15f;
        public int InitialFieldStarCount = 105;
        public float StreamBrightness = 1f;
        public float StreamAlphaMin = .025f;
        public float StreamAlphaMax = .58f;
        public float BaseSpeed = 1.8f;
        public float SpeedMultiplierMin = 1.3f;
        public float SpeedMultiplierMax = 1.8f;
        public float MinSize = .014f;
        public float MaxSize = .055f;
        public float EdgeFlybyChance = .32f;
        public float EdgeFlybyStart = .07f;
        public float EdgeFlybyMaxScale = 5.25f;
        public float EdgeFlybyAlphaBoost = .88f;
        public float StreamSpawnRadiusMin = .34f;
        public float StreamSpawnRadiusMax = .72f;
        public float InitialFieldRadiusMin = .78f;
        public float InitialFieldRadiusMax = 6.45f;
        public float FlybyHaloBaseScale = 0.1f;
        public float FlybyHaloMaxScale = 1.4f;
        public float MinLifetime = 4.8f;
        public float MaxLifetime = 6.8f;
        public float TrailLength = .82f;
        public float TrailWidth = .05f;
        public float TrailFade = .3f;
        public float FarTrailSeconds = .2f;
        public float NearTrailSeconds = .6f;
        public float FarTrailWidth = .006f;
        public float NearTrailWidth = .065f;
        public float CombatTravelSpeed = .06f;
        public float JumpTravelSpeed = 5.5f;
        public float JumpDuration = 1.45f;
        public float ScreenEdgeSlowStart = .66f;
        public float ScreenEdgeSpeedMultiplier = .28f;
        public float PurpleChance = .03f;
        public float ShieldTrailLength = .34f;
        public float ShieldTrailWidth = .065f;
        public float BackgroundTravelSpeed = .037f;
        public float BackgroundMinRadius = .2f;
        public float BackgroundMaxRadius = 9.4f;
static StarStreamSettingsProfile cached;
public static StarStreamSettingsProfile Current => cached != null ? cached : cached = (Resources.Load<StarStreamSettingsProfile>("Gameplay/StarStreamSettings") ?? CreateInstance<StarStreamSettingsProfile>());
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
}
}
