using UnityEngine;

namespace OrbitalRift
{
    [CreateAssetMenu(menuName = "Orbital Rift/Boss VFX Profile")]
    public sealed class BossVfxProfile : ScriptableObject
    {
        [Header("Phoenix wings")]
        [Min(.01f)] public float WingScale = .52f;
        [Range(0, 45)] public float WingFlapAngle = 10f;
        [Range(0, 20)] public float WingSecondaryFlapAngle = 3.5f;
        [ColorUsage(true, true)] public Color WingColor = new Color(1f, .92f, .76f, .94f);

        [Header("Ambient particles · amount")]
        [Tooltip("Hard cap for the reusable particle system.")]
        [Min(1)] public int ParticleMaxCount = 96;
        [Tooltip("Particles emitted per second around the boss.")]
        [Min(0)] public float ParticleEmissionRate = 28f;

        [Header("Ambient particles · speed")]
        [Min(0)] public float ParticleSpeedMin = .10f;
        [Min(0)] public float ParticleSpeedMax = .42f;

        [Header("Ambient particles · size")]
        [Min(0)] public float ParticleSizeMin = .018f;
        [Min(0)] public float ParticleSizeMax = .075f;

        [Header("Ambient particles · lifetime")]
        [Min(0)] public float ParticleLifetimeMin = .35f;
        [Min(0)] public float ParticleLifetimeMax = 1.15f;

        [Header("Ambient particles · color")]
        [ColorUsage(true, true)] public Color ParticleColorMin = new Color(1f, .18f, .03f, .15f);
        [ColorUsage(true, true)] public Color ParticleColorMax = new Color(1f, .88f, .25f, .85f);

        [Header("Ambient particles · rotation")]
        public Vector2 ParticleRotationMinMax = new Vector2(-Mathf.PI, Mathf.PI);
        public Vector2 ParticleAngularVelocityMinMax = new Vector2(-2.2f, 2.2f);
        public float ParticleGravity = -.03f;

        [Header("Noise")]
        public bool ParticleNoiseEnabled = true;
        [Range(0, 1)] public float ParticleNoiseStrength = .16f;
        [Range(.01f, 10)] public float ParticleNoiseFrequency = 1.8f;
        [Range(0, 10)] public float ParticleNoiseScrollSpeed = .35f;

        [Header("Emission shape")]
        [Min(0)] public float ParticleEmissionRadius = .50f;
        [Range(0, 360)] public float ParticleEmissionArc = 360f;
        [Header("Ambient particles · gradient / fade")]
        public bool UseParticleGradient;
        public Gradient ParticleOverLifetime = new Gradient();
        public AnimationCurve ParticleSizeOverLifetime = AnimationCurve.EaseInOut(0, 1, 1, 0);
    }
}
