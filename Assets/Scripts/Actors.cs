using UnityEngine;

namespace OrbitalRift
{
    public enum EnemyKind { Scout, Spiral, Diver, Turret }

    public sealed class Projectile : MonoBehaviour
    {
        public Vector2 Velocity;
        public bool FromPlayer;
        public float Life;
        public SpriteRenderer Renderer;
        public bool PreserveSpriteColor;
        private Vector3 defaultScale;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); defaultScale = transform.localScale; }
        public void SetVisual(Sprite sprite, bool preserveColor, bool hostile)
        {
            Renderer.sprite = sprite;
            PreserveSpriteColor = preserveColor;
            transform.localScale = hostile ? Vector3.one * .14f : defaultScale;
        }
        public void ResetProjectile(Vector2 position, Vector2 velocity, bool fromPlayer, Color color)
        {
            transform.position = position; Velocity = velocity; FromPlayer = fromPlayer; Life = 3f;
            Renderer.color = PreserveSpriteColor ? Color.white : color;
        }
    }

    public sealed class Enemy : MonoBehaviour
    {
        public EnemyKind Kind;
        public float Angle, Radius, Health, FireTimer, Life;
        public int Points;
        public SpriteRenderer Renderer;
        private Sprite fallbackSprite;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); fallbackSprite = Renderer.sprite; }
        public void ResetEnemy(EnemyKind kind, float angle, int phase, Sprite customSprite)
        {
            Kind = kind; Angle = angle; Radius = .95f; Life = 18f;
            FireTimer = Random.Range(BalanceSettings.EnemyFireInterval(phase) * .85f, BalanceSettings.EnemyFireInterval(phase) * 1.45f);
            Health = kind == EnemyKind.Turret ? 5 : kind == EnemyKind.Diver ? 2 : 1;
            Points = kind == EnemyKind.Scout ? 100 : kind == EnemyKind.Spiral ? 175 : kind == EnemyKind.Diver ? 250 : 350;
            var fallbackColor = kind == EnemyKind.Scout ? new Color(1f,.55f,.12f) : kind == EnemyKind.Spiral ? new Color(1f,.16f,.45f) : kind == EnemyKind.Diver ? new Color(.95f,.25f,.8f) : new Color(1f,.8f,.18f);
            Renderer.sprite = customSprite != null ? customSprite : fallbackSprite;
            Renderer.color = customSprite != null ? Color.white : fallbackColor;
            var desiredSize = kind == EnemyKind.Turret ? .32f : .38f;
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
    }
}
