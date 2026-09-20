using UnityEngine;

namespace OrbitalRift
{
    public enum BossShotPattern { AimedFan, Radial }
    [CreateAssetMenu(menuName = "Orbital Rift/Bosses/Shot")]
    public sealed class BossShotDefinition : ScriptableObject
    {
        public ActorAppearance Appearance = new ActorAppearance();
        [Header("Выстрел")]
        public string ShotId;
        public string DisplayName;
        public Projectile Prefab;
        [Header("Урон / полёт")]
        [Min(0)] public float Damage = 1;
        public DamageElement Element;
        [Tooltip("Скорость в мировых единицах в секунду при первой фазе.")]
        [Min(.01f)] public float Speed = 2;
        [Tooltip("Сохраняет существующий рост скорости с номером фазы.")]
        public bool ScaleSpeedWithPhase = true;
        [Min(.01f)] public float Lifetime = 4.25f;
        [Min(.01f)] public float FireInterval = .9f;
        [Min(1)] public int ProjectileCount = 1;
        public BossShotPattern Pattern;
        [Tooltip("Угол между соседними снарядами веера, градусы.")]
        [Range(0, 360)] public float SpreadDegrees;
        public bool Homing;
        [Min(0)] public float HomingDegreesPerSecond = 90;
        [Header("Столкновение / попадание")]
        [Min(0)] public float PlayerHitRadius = .3f;
        [Min(0)] public float ShieldHitRadius = .22f;
        public bool ShieldCanBlock = true;
        public bool DestroyOnHit = true;
        [Header("Визуал · отдельный профиль цвета / trail / fade")]
        public Sprite Sprite;
        [Min(.001f)] public float Scale = .16f;
        public ProjectileVisualStyle VisualStyle;
        public SpellProjectileVfx VfxPrefab;
        public SpellVfxProfile Vfx;
        public SpellImpactVfx ImpactPrefab;
        public AudioClip Sound;
        [Range(0, 1)] public float Shake;
        public float FlightSpeed(int phase) => Speed * (ScaleSpeedWithPhase
            ? BalanceSettings.EnemyProjectileSpeed(phase) / BalanceSettings.EnemyProjectileSpeed(1) : 1);
    }
}
