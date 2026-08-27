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

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); }
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

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); }
        public void ResetEnemy(EnemyKind kind, float angle, int phase)
        {
            Kind = kind; Angle = angle; Radius = .95f; Life = 18f; FireTimer = Random.Range(.45f, 1.2f);
            Health = kind == EnemyKind.Turret ? 5 : kind == EnemyKind.Diver ? 2 : 1;
            Points = kind == EnemyKind.Scout ? 100 : kind == EnemyKind.Spiral ? 175 : kind == EnemyKind.Diver ? 250 : 350;
            Renderer.color = kind == EnemyKind.Scout ? new Color(1f,.55f,.12f) : kind == EnemyKind.Spiral ? new Color(1f,.16f,.45f) : kind == EnemyKind.Diver ? new Color(.95f,.25f,.8f) : new Color(1f,.8f,.18f);
            transform.localScale = Vector3.one * (kind == EnemyKind.Turret ? .32f : .22f);
        }
    }

    public sealed class StarParticle : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Life;
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
            transform.position = direction * Random.Range(.05f, .35f);
            Velocity = direction * (StarStreamSettings.BaseSpeed + speed * Random.Range(StarStreamSettings.SpeedMultiplierMin, StarStreamSettings.SpeedMultiplierMax));
            Life = Random.Range(StarStreamSettings.MinLifetime, StarStreamSettings.MaxLifetime);
            transform.localScale = Vector3.one * Random.Range(StarStreamSettings.MinSize, StarStreamSettings.MaxSize);
            Renderer.color = new Color(1,1,1,StarStreamSettings.StreamBrightness);
            Trail.time = StarStreamSettings.TrailLength;
            Trail.Clear();
        }
    }
}
