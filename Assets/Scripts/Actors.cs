using UnityEngine;

namespace OrbitalRift
{
    public enum EnemyKind { Scout, Spiral, Diver, Turret, Boss }
    public enum BossAiState { Orbit, Barrage, Charge }

    public sealed class Projectile : MonoBehaviour
    {
        public Vector2 Velocity;
        public bool FromPlayer;
        public DamageElement Element;
        public float Damage;
        public float Life;
        public SpriteRenderer Renderer;
        public bool PreserveSpriteColor;
        public bool VisualOnly;
        private Vector3 defaultScale;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); defaultScale = transform.localScale; }
        public void SetVisual(Sprite sprite, bool preserveColor, bool hostile)
        {
            Renderer.sprite = sprite;
            PreserveSpriteColor = preserveColor;
            transform.localScale = hostile ? Vector3.one * .14f : defaultScale;
        }
        public void ResetProjectile(Vector2 position, Vector2 velocity, bool fromPlayer, Color color, DamageElement element, float damage)
        {
            transform.position = position; Velocity = velocity; FromPlayer = fromPlayer; Element = element; Damage = Mathf.Max(0f, damage); Life = 3f; VisualOnly = false;
            Renderer.color = PreserveSpriteColor ? Color.white : color;
        }
    }

    public sealed class Enemy : MonoBehaviour
    {
        public EnemyKind Kind;
        public float Angle, Radius, Health, MaxHealth, FireTimer, Life;
        public float BossStateTimer;
        public BossAiState BossState;
        public int Points;
        public SpriteRenderer Renderer;
        private Sprite fallbackSprite;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); fallbackSprite = Renderer.sprite; }
        public void ResetEnemy(EnemyKind kind, float angle, int phase, Sprite customSprite)
        {
            // Босс не должен исчезнуть сам по таймеру: переход к ядру возможен
            // только после того, как игрок снимет весь его запас здоровья.
            Kind = kind; Angle = angle; Radius = .95f; Life = kind == EnemyKind.Boss ? 999f : 18f;
            FireTimer = Random.Range(BalanceSettings.EnemyFireInterval(phase) * .85f, BalanceSettings.EnemyFireInterval(phase) * 1.45f);
            Health = kind == EnemyKind.Boss ? BossSettings.Health : kind == EnemyKind.Turret ? 5 : kind == EnemyKind.Diver ? 2 : 1;
            MaxHealth = Health;
            Points = kind == EnemyKind.Boss ? BossSettings.Points : kind == EnemyKind.Scout ? 100 : kind == EnemyKind.Spiral ? 175 : kind == EnemyKind.Diver ? 250 : 350;
            BossState = BossAiState.Orbit;
            BossStateTimer = kind == EnemyKind.Boss ? 2.4f : 0f;
            var fallbackColor = kind == EnemyKind.Scout ? new Color(1f,.55f,.12f) : kind == EnemyKind.Spiral ? new Color(1f,.16f,.45f) : kind == EnemyKind.Diver ? new Color(.95f,.25f,.8f) : kind == EnemyKind.Boss ? new Color(.62f,.2f,1f) : new Color(1f,.8f,.18f);
            Renderer.sprite = customSprite != null ? customSprite : fallbackSprite;
            Renderer.color = customSprite != null ? Color.white : fallbackColor;
            var desiredSize = kind == EnemyKind.Boss ? BossSettings.WorldSize : kind == EnemyKind.Turret ? .32f : .38f;
            var spriteSize = Mathf.Max(Renderer.sprite.bounds.size.x, Renderer.sprite.bounds.size.y);
            transform.localScale = spriteSize > .0001f ? Vector3.one * (desiredSize / spriteSize) : Vector3.one * desiredSize;
        }
    }

    public sealed class DamageShard : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public SpriteRenderer Renderer;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); }
        public void ResetShard(Vector2 position, Vector2 velocity, float size, Color color, float lifetime = .28f)
        {
            transform.position = position;
            transform.localScale = Vector3.one * size;
            Velocity = velocity;
            Life = lifetime;
            MaxLife = lifetime;
            Renderer.color = color;
        }
    }

    public sealed class StarParticle : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Life;
        public float Brightness;
        public bool IsShield;
        public bool IsPurple;
        public int ShieldHits;
        public float ShieldAngle;
        public float ShieldRadius;
        public SpriteRenderer Renderer;
        public TrailRenderer Trail;
        private void Awake()
        {
            EnsureRenderers();
        }

        private void EnsureRenderers()
        {
            Renderer = GetComponent<SpriteRenderer>();
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
        public void ResetStar(Vector2 direction, float speed, float life)
        {
            if (Renderer == null || Trail == null) EnsureRenderers();
            IsShield = false;
            IsPurple = false;
            ShieldHits = 0;
            ShieldAngle = 0f;
            ShieldRadius = 0f;
            transform.position = direction * Random.Range(.05f, .45f);
            Velocity = direction * (StarStreamSettings.BaseSpeed + speed * Random.Range(StarStreamSettings.SpeedMultiplierMin, StarStreamSettings.SpeedMultiplierMax));
            Life = Random.Range(StarStreamSettings.MinLifetime, StarStreamSettings.MaxLifetime);
            Brightness = Random.Range(StarStreamSettings.StreamAlphaMin, StarStreamSettings.StreamAlphaMax) * StarStreamSettings.StreamBrightness;
            transform.localScale = Vector3.one * Random.Range(StarStreamSettings.MinSize, StarStreamSettings.MaxSize);
            Renderer.color = new Color(1, 1, 1, Brightness);
            Trail.time = StarStreamSettings.TrailLength;
            Trail.startColor = new Color(1f, 1f, 1f, Brightness * StarStreamSettings.TrailFade);
            Trail.Clear();
        }

        public void SetPurple(bool purple)
        {
            IsPurple = purple;
            var tint = purple ? new Color(.76f, .38f, 1f, 1f) : Color.white;
            Renderer.color = new Color(tint.r, tint.g, tint.b, Brightness);
            Trail.startColor = new Color(tint.r, tint.g, tint.b, Brightness * StarStreamSettings.TrailFade);
        }
    }
}
