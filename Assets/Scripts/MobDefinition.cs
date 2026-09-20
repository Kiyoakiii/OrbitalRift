using System;
using UnityEngine;
namespace OrbitalRift
{
    [CreateAssetMenu(menuName="Orbital Rift/Enemies/Mob")]
    public sealed class MobDefinition : ScriptableObject
    {
        public string MobId, DisplayName;
        [Tooltip("Выбирает готовую схему движения, не идентичность моба.")] public EnemyKind Movement;
        public Enemy Prefab;
        public Sprite Sprite;
        public ActorAppearance Appearance = new ActorAppearance();
        [Min(1)] public float Health=1;
        [Min(0)] public int Points=100;
        [Min(.01f)] public float Size=.38f, HitRadius=.28f, Lifetime=18;
        public Color Tint=Color.white;
        public float Radius=3.1f, AngularSpeed=.7f, RadialAmplitude, RadialFrequency=2.2f, TrackingSpeed=.8f;
        public bool ShootInClassic=true, ShootInDefense;
        public BossShotDefinition Shot;
        [Tooltip("Общие способности. Запускаются по очереди; собственный Cooldown задаёт период. Для мобов: Projectiles, Dash, Summon, Beam, Roots.")]
        public BossAbilityDefinition[] Spells=Array.Empty<BossAbilityDefinition>();
        public float DefenseSpeed=.52f, DefenseSpeedPerWave=.022f, DefenseWobble, DefenseWobbleFrequency=3.2f, DefenseAngularSpeed=.75f;
    }
}
