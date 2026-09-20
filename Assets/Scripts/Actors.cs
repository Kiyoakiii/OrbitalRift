using UnityEngine;

namespace OrbitalRift
{
    public enum EnemyKind { Scout, Spiral, Diver, Turret, Boss, ShadeClone }
    public enum BossAiState { Orbit, Barrage, Charge, BeamTelegraph, BeamSweep, RootTelegraph, RootLock, Egg, Dash }
    public enum BossArchetype { VoidMaw, AstralFirebird, UmbralHarrier }
    public enum ProjectileVisualStyle { Default, FirebirdChick, HarrierShard, VoidPulse, SolarLance }

    public sealed class DamageShard : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public SpriteRenderer Renderer;
        public bool SandboxBossEffect;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); }
        public void ResetShard(Vector2 position, Vector2 velocity, float size, Color color, float lifetime = .28f)
        {
            transform.position = position;
            transform.localScale = Vector3.one * size;
            Velocity = velocity;
            Life = lifetime;
            MaxLife = lifetime;
            SandboxBossEffect = false;
            Renderer.color = color;
        }
    }

    public sealed class StarParticle : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Life;
        public float Brightness;
        public float BaseBrightness;
        public float BaseSize;
        public float TrailVariation;
        public bool HasEdgeFlyby;
        public bool IsShield;
        public bool IsPurple;
        public int ShieldHits;
        public float ShieldAngle;
        public float ShieldRadius;
        public Color StreamTint;
        public SpriteRenderer Renderer;
        public SpriteRenderer Halo;
        public TrailRenderer Trail;
        private void Awake()
        {
            EnsureRenderers();
        }

        private void EnsureRenderers()
        {
            Renderer = GetComponent<SpriteRenderer>();
            if (Halo == null)
            {
                var existingHalo = transform.Find("Flyby halo");
                if (existingHalo != null) Halo = existingHalo.GetComponent<SpriteRenderer>();
                if (Halo == null)
                {
                    var haloObject = new GameObject("Flyby halo");
                    haloObject.transform.SetParent(transform, false);
                    Halo = haloObject.AddComponent<SpriteRenderer>();
                }
            }
            Halo.sprite = Renderer != null ? Renderer.sprite : null;
            Halo.sortingOrder = -2;
            Halo.color = Color.clear;
            Halo.gameObject.SetActive(false);
            Trail = GetComponent<TrailRenderer>();
            if (Trail == null) Trail = gameObject.AddComponent<TrailRenderer>();
            Trail.material = new Material(Shader.Find("Sprites/Default"));
            Trail.time = StarStreamSettings.TrailLength;
            Trail.startWidth = StarStreamSettings.TrailWidth;
            Trail.endWidth = 0f;
            Trail.minVertexDistance = .02f;
            Trail.startColor = new Color(1f, 1f, 1f, StarStreamSettings.TrailFade);
            Trail.endColor = new Color(1f, 1f, 1f, 0f);
            Trail.sortingOrder = -2;
        }
        public void ResetStar(Vector2 direction, float speed, float life, bool seededAcrossArena = false)
        {
            if (Renderer == null || Trail == null || Halo == null) EnsureRenderers();
            IsShield = false;
            IsPurple = false;
            ShieldHits = 0;
            ShieldAngle = 0f;
            ShieldRadius = 0f;
            HasEdgeFlyby = Random.value < StarStreamSettings.EdgeFlybyChance;
            var spawnRadius = seededAcrossArena
                ? Random.Range(StarStreamSettings.InitialFieldRadiusMin, StarStreamSettings.InitialFieldRadiusMax)
                : Random.Range(StarStreamSettings.StreamSpawnRadiusMin, StarStreamSettings.StreamSpawnRadiusMax);
            // Normal stream stars emerge from the rift ring, not a mathematical point. The
            // opening field uses the same directions, merely pre-distributed along the paths.
            transform.position = direction * spawnRadius;
            Velocity = direction * (StarStreamSettings.BaseSpeed + speed * Random.Range(StarStreamSettings.SpeedMultiplierMin, StarStreamSettings.SpeedMultiplierMax));
            Life = Random.Range(StarStreamSettings.MinLifetime, StarStreamSettings.MaxLifetime) * (seededAcrossArena ? Random.Range(.38f, .92f) : 1f);
            BaseBrightness = Random.Range(StarStreamSettings.StreamAlphaMin, StarStreamSettings.StreamAlphaMax) * StarStreamSettings.StreamBrightness;
            if (HasEdgeFlyby)
                BaseBrightness = Mathf.Lerp(BaseBrightness, StarStreamSettings.StreamAlphaMax, .58f);
            Brightness = BaseBrightness;
            BaseSize = Random.Range(StarStreamSettings.MinSize, StarStreamSettings.MaxSize) * (HasEdgeFlyby ? 1.10f : 1f);
            TrailVariation = Random.Range(.70f, 1.30f);
            var tintRoll = Random.value;
            StreamTint = tintRoll < .20f ? new Color(.65f, .80f, 1f, 1f) :
                tintRoll < .34f ? new Color(.88f, .80f, 1f, 1f) :
                tintRoll < .43f ? new Color(.92f, .96f, 1f, 1f) : Color.white;
            transform.localScale = Vector3.one * BaseSize;
            Renderer.color = new Color(StreamTint.r, StreamTint.g, StreamTint.b, Brightness);
            Halo.sprite = Renderer.sprite;
            Halo.transform.localScale = Vector3.one * StarStreamSettings.FlybyHaloBaseScale;
            Halo.color = new Color(StreamTint.r, StreamTint.g, StreamTint.b, HasEdgeFlyby ? Brightness * .035f : 0f);
            Halo.gameObject.SetActive(HasEdgeFlyby);
            Trail.time = StarStreamSettings.TrailLength;
            Trail.startWidth = StarStreamSettings.TrailWidth;
            Trail.startColor = new Color(StreamTint.r, StreamTint.g, StreamTint.b, Brightness * StarStreamSettings.TrailFade);
            Trail.Clear();
        }

        public void SetPurple(bool purple, ShieldVfxProfile profile = null)
        {
            IsPurple = purple;
            // Purple stars are gameplay pickups: keep their silhouette stable and readable.
            if (purple) HasEdgeFlyby = false;
            var tint = purple ? (profile != null ? profile.CoreColor : new Color(.76f, .38f, 1f, 1f)) : StreamTint;
            var brightness = purple && profile != null ? profile.CoreBrightness : 1f;
            var trailColor = purple && profile != null ? profile.TrailColor : tint;
            var trailBrightness = profile != null ? profile.TrailBrightness : 1f;
            Renderer.color = new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, Brightness);
            Trail.startColor = new Color(trailColor.r * trailBrightness, trailColor.g * trailBrightness,
                trailColor.b * trailBrightness, Brightness * StarStreamSettings.TrailFade * trailColor.a);
            if (purple && profile != null)
                transform.localScale = Vector3.one * Mathf.Max(profile.CoreSize.x, profile.CoreSize.y);
            if (Halo != null)
            {
                Halo.gameObject.SetActive(!purple && HasEdgeFlyby);
                Halo.color = new Color(tint.r, tint.g, tint.b, !purple && HasEdgeFlyby ? Brightness * .035f : 0f);
            }
        }

        public void ConfigureShield(ShieldVfxProfile profile, int orbitIndex)
        {
            IsShield = true;
            Velocity = Vector2.zero;
            Life = 999f;
            ShieldAngle = Random.Range(0f, Mathf.PI * 2f);
            ShieldRadius = profile != null
                ? profile.OrbitRadius + Mathf.Max(0, orbitIndex) * profile.OrbitRadiusStep
                : .48f + Mathf.Max(0, orbitIndex) * .09f;
            ShieldHits = Mathf.Max(1, profile != null ? profile.ShieldHits : 2);
            if (Trail != null)
            {
                Trail.time = profile != null ? profile.TrailLifetime : StarStreamSettings.ShieldTrailLength;
                Trail.startWidth = profile != null ? profile.TrailWidth : StarStreamSettings.ShieldTrailWidth;
                Trail.Clear();
            }
            TickShieldVisual(profile, 0f);
        }

        public void TickShieldVisual(ShieldVfxProfile profile, float time)
        {
            var coreColor = profile != null ? profile.CoreColor : new Color(.78f, .42f, 1f, 1f);
            var coreBrightness = profile != null ? profile.CoreBrightness : 1f;
            var pulse = profile != null
                ? 1f + Mathf.Sin(time * profile.PulseSpeed * Mathf.PI * 2f) * profile.PulseAmount
                : 1f;
            var coreSize = profile != null ? Mathf.Max(profile.CoreSize.x, profile.CoreSize.y) : .13f;
            transform.localScale = Vector3.one * coreSize * pulse;
            Renderer.color = new Color(coreColor.r * coreBrightness, coreColor.g * coreBrightness,
                coreColor.b * coreBrightness, coreColor.a);

            var glowColor = profile != null ? profile.GlowColor : new Color(.60f, .18f, 1f, .28f);
            var glowBrightness = profile != null ? profile.GlowBrightness : .90f;
            if (Halo != null)
            {
                Halo.gameObject.SetActive(IsShield && glowBrightness > .001f);
                var glowSize = profile != null ? Mathf.Max(profile.GlowSize.x, profile.GlowSize.y) : .31f;
                Halo.transform.localScale = Vector3.one * (glowSize / Mathf.Max(.001f, coreSize));
                Halo.color = new Color(glowColor.r * glowBrightness, glowColor.g * glowBrightness,
                    glowColor.b * glowBrightness, glowColor.a);
            }

            if (Trail != null)
            {
                var trailColor = profile != null ? profile.TrailColor : new Color(.78f, .42f, 1f, .78f);
                var trailBrightness = profile != null ? profile.TrailBrightness : 1f;
                Trail.time = profile != null ? profile.TrailLifetime : StarStreamSettings.ShieldTrailLength;
                Trail.startWidth = profile != null ? profile.TrailWidth : StarStreamSettings.ShieldTrailWidth;
                Trail.startColor = new Color(trailColor.r * trailBrightness, trailColor.g * trailBrightness,
                    trailColor.b * trailBrightness, trailColor.a);
            }
        }
    }
}
