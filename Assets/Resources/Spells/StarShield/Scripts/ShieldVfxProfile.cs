using UnityEngine;

namespace OrbitalRift
{
    [CreateAssetMenu(menuName = "Orbital Rift/Shield VFX Profile")]
    public sealed class ShieldVfxProfile : ScriptableObject
    {
        [Header("Core · world units, HDR multiplier")]
        public Vector2 CoreSize = new Vector2(.13f, .13f);
        [ColorUsage(true, true)] public Color CoreColor = new Color(.76f, .38f, 1f);
        [Range(0, 5)] public float CoreBrightness = 1.15f;
        [Range(0, .3f)] public float PulseAmount = .08f;
        [Min(0)] public float PulseSpeed = 3.2f;

        [Header("Glow")]
        public Vector2 GlowSize = new Vector2(.31f, .31f);
        [ColorUsage(true, true)] public Color GlowColor = new Color(.60f, .18f, 1f, .28f);
        [Range(0, 3)] public float GlowBrightness = .90f;

        [Header("Trail")]
        [Min(.001f)] public float TrailWidth = .065f;
        [Range(.02f, 10.8f)] public float TrailLifetime = .34f;
        [Range(0, 3)] public float TrailBrightness = .90f;
        [ColorUsage(true, true)] public Color TrailColor = new Color(.78f, .42f, 1f, .78f);

        [Header("Orbit / gameplay")]
        [Min(.01f)] public float PickupRadius = .34f;
        [Min(.01f)] public float OrbitRadius = .48f;
        [Min(0)] public float OrbitRadiusStep = .09f;
        [Range(.1f, 20)] public float OrbitSpeed = 3.4f;
        [Range(1, 5)] public int ShieldHits = 2;
        [Range(1, 8)] public int MaxShields = 3;

        [Header("Lifetime / despawn")]
        [Tooltip("0 = the loose star is released only after leaving the camera view.")]
        [Min(0)] public float DespawnRadius = 0f;
        [Tooltip("Extra normalized viewport margin kept around the visible screen.")]
        [Range(0, 1)] public float ScreenPadding = .12f;

        [Header("Impact")]
        [Min(.01f)] public float ImpactSize = .85f;
        [Range(0, 4)] public float ImpactBrightness = 1.3f;
        [Range(0, 48)] public int ImpactParticleCount = 10;
        [Range(.2f, 2)] public float ImpactDuration = .30f;
        [ColorUsage(true, true)] public Color ImpactColor = new Color(.86f, .50f, 1f);
    }
}
