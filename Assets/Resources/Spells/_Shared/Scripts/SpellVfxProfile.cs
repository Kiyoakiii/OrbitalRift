using UnityEngine;

namespace OrbitalRift
{
    [CreateAssetMenu(menuName = "Orbital Rift/Spell VFX Profile")]
    public sealed class SpellVfxProfile : ScriptableObject
    {
        [Header("Core · world units, HDR multiplier")]
        public Vector2 CoreSize = new Vector2(.16f, .38f);
        [ColorUsage(true, true)] public Color CoreColor = new Color(.65f, .95f, 1f);
        [Range(0, 5)] public float CoreBrightness = 1.8f;
        [Range(0, .3f)] public float PulseAmount = .065f;
        [Min(0)] public float PulseSpeed = 3.7f;
        [Header("Glow")]
        public Vector2 GlowSize = new Vector2(.43f, .64f);
        [ColorUsage(true, true)] public Color GlowColor = new Color(.08f, .62f, 1f, .26f);
        [Range(0, 3)] public float GlowBrightness = .75f;
        [Header("Trail · length = speed × lifetime")]
        [Min(.001f)] public float TrailWidth = .16f;
        [Range(.02f, 10.8f)] public float TrailLifetime = .16f;
        [Range(0, 3)] public float TrailBrightness = .9f;
        public Gradient TrailColor = new Gradient();
        public AnimationCurve TrailShape = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [Header("World-space particles · emission per second")]
        [Range(0, 80)] public float ParticleCount = 24;
        [Min(.001f)] public float ParticleSize = .045f;
        [Min(0)] public float ParticleSpeed = .45f;
        [Range(.02f, 10)] public float ParticleLifetime = .24f;
        [ColorUsage(true, true)] public Color ParticleColor = new Color(.12f, .65f, 1f, .75f);
        [Range(0, 60)] public float MicroCount = 18;
        [Range(0, .5f)] public float NoiseStrength = .12f;
        [Header("Secondary motion · independent of projectile speed")]
        [Min(0)] public float SecondaryMotionSpeed = 2.6f;
        [Range(0, .4f)] public float SecondaryMotionRadius = .085f;
        [ColorUsage(true, true)] public Color RibbonColor = new Color(.40f, .19f, 1f, .65f);
        [Header("Impact")]
        [Min(.01f)] public float ImpactSize = .85f;
        [Range(0, 4)] public float ImpactBrightness = 1.3f;
        [Range(0, 48)] public int ImpactParticleCount = 14;
        [Range(.2f, 2)] public float ImpactDuration = .85f;
        [ColorUsage(true, true)] public Color ImpactColor = new Color(.17f, .73f, 1f);
        [Header("Lifetime / despawn")]
        [Tooltip("0 = the projectile is released only after leaving the camera view.")]
        [Min(0)] public float DespawnRadius = 0f;
        [Tooltip("Extra normalized viewport margin kept around the visible screen.")]
        [Range(0, 1)] public float ScreenPadding = .12f;
        [Header("Градиенты по времени · 0 = рождение, 1 = конец жизни")]
        [Tooltip("При выключении сохраняется прежняя внешность существующих спеллов.")]
        public bool UseLifetimeGradients;
        public Gradient CoreOverLife = new Gradient();
        public Gradient GlowOverLife = new Gradient();
        public Gradient ParticleOverLife = new Gradient();
        public Gradient ImpactOverLife = new Gradient();
        public Gradient RibbonOverTrail = new Gradient();
        public AnimationCurve AlphaOverLife = AnimationCurve.Linear(0, 1, 1, 1);
        public AnimationCurve GlowFade = AnimationCurve.Linear(0, 1, 1, 0);
        public AnimationCurve ParticleSizeOverLife = AnimationCurve.EaseInOut(0, 1, 1, 0);
        public AnimationCurve ImpactFade = AnimationCurve.Linear(0, 1, 1, 0);
        [Header("Анимация птицы · только prefab с SolarChickMotion")]
        public bool UseBirdMotionSettings;
        [Min(.01f)] public float BirdSize = .82f;
        [Min(0)] public float WingBeatSpeed = 3.6f;
        [Range(0, 1)] public float WingFold = .65f;
        public float BodyBob = .018f;
        public float BankAngle = 4.5f;
        public float FlameSize = .74f;
        public float FlameSway = .22f;
        public float FlameBrightness = .7f;
        public Color BirdTint = Color.white;
        public Color FlameLeftTint = new Color(1, .8f, .5f, .66f);
        public Color FlameRightTint = new Color(1, .42f, .12f, .48f);
    }
}
