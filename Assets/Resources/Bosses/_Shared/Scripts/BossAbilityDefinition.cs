using System;
using UnityEngine;

namespace OrbitalRift
{
    public enum BossAbilityBehaviour { Projectiles, Dash, Summon, RebirthEgg, Beam, Roots, SandboxGravityWell, SandboxAura }
    public enum BossAngularMotion { TrackPlayer, Spin }

    [CreateAssetMenu(menuName = "Orbital Rift/Bosses/Ability")]
    public sealed class BossAbilityDefinition : ScriptableObject
    {
        public ActorAppearance Appearance = new ActorAppearance();
        [Header("Способность")]
        public BossAbilityId AbilityId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public bool ShowInGuide = true;
        [Tooltip("Существующий эффект только для Sandbox. Не включается в боевые фазы.")]
        public bool SandboxOnly;
        public BossAbilityBehaviour Behaviour;
        public BossAiState State;
        [Header("Время · секунды")]
        [Tooltip("Минимальное время между началами этой способности. 0 — без дополнительной блокировки.")]
        [Min(0)] public float Cooldown;
        [Tooltip("Подготовка перед активным действием. Для луча и корней это безопасный телеграф.")]
        [Min(0)] public float CastDelay;
        [Min(.01f)] public float Duration = 3;
        [Tooltip("Задержка первого снаряда после выбора способности, дополнительно к Cast Delay.")]
        [Min(0)] public float FirstShotDelay = .2f;
        [Range(0, 1)] public float MaxHealthFraction = 1;
        [Header("Движение во время способности")]
        public BossMovementSettings Movement = new BossMovementSettings();
        public bool UseCastMovement;
        public BossMovementSettings CastMovement = new BossMovementSettings();
        [Header("Связанные объекты")]
        public BossShotDefinition Shot;
        [Tooltip("Например, призыв двойников одновременно запускает отдельную способность Холодный веер.")]
        public BossAbilityDefinition SecondaryAbility;
        public SpellVfxProfile Vfx;
        public SpellImpactVfx CastPrefab;
        public SpellImpactVfx AftereffectPrefab;
        public AudioClip Sound;
        [Range(0, 1)] public float Shake = .08f;
        [Header("Цвет / градиент / затухание")]
        public BossEffectStyle Style = new BossEffectStyle();
        [Header("Призыв · используется Summon")]
        public BossSummonSettings Summon = new BossSummonSettings();
        [Header("Перерождение · используется Rebirth Egg")]
        public BossEggSettings Egg = new BossEggSettings();
        [Header("Луч · используется Beam")]
        public BossBeamSettings Beam = new BossBeamSettings();
        [Header("Корни · используется Roots")]
        public BossRootSettings Roots = new BossRootSettings();
        [Header("Чёрная дыра · Sandbox")]
        public BossGravitySettings Gravity = new BossGravitySettings();
    }

    [Serializable]
    public sealed class BossMovementSettings
    {
        [Min(0)] public float Radius = 2.45f;
        public bool MoveTowardsRadius;
        [Min(0)] public float RadiusSpeed = 1.5f;
        [Min(0)] public float SwayAmplitude;
        [Min(0)] public float SwayFrequency = 1.8f;
        public BossAngularMotion AngularMotion;
        [Tooltip("Радианы в секунду.")]
        public float AngularSpeed = 1;
        [Tooltip("Угловой сдвиг от игрока, радианы.")]
        public float PlayerAngleOffset;
    }
    [Serializable]
    public sealed class BossSummonSettings
    {
        public Enemy Prefab;
        public Sprite Sprite;
        public BossShotDefinition Shot;
        [Min(1)] public int Count = 2;
        [Min(1)] public int EnragedCount = 3;
        [Range(0, 1)] public float EnragedHealthFraction = .52f;
        [Min(.01f)] public float Health = 3;
        [Min(.01f)] public float Lifetime = 5.6f;
        [Min(.01f)] public float Size = .72f;
        public float OrbitRadius = 2.38f;
        public float AngularSpeed = 1.46f;
        public float AngularSway = .32f;
        [Min(0)] public float ContactRadius = .36f;
        public float ContactKnockbackDegrees = 22;
    }
    [Serializable]
    public sealed class BossEggSettings
    {
        [Min(.01f)] public float Health = 10;
        [Min(.01f)] public float ReviveHealth = 36;
        public Vector2 RadiusRange = new Vector2(.68f, .98f);
        [Min(0)] public float AngleScatter = .72f;
        [Min(.01f)] public float Size = .58f;
        [Min(.01f)] public float HitRadius = .38f;
        [Min(0)] public float ReviveDelay = 2.2f;
    }
    [Serializable]
    public sealed class BossBeamSettings
    {
        [Range(1, 8)] public int Count = 2;
        [Range(1, 8)] public int EnragedCount = 3;
        [Range(0, 1)] public float EnragedHealthFraction = .5f;
        [Min(.01f)] public float Length = 5.3f;
        [Min(0)] public float InnerSafeRadius = .32f;
        [Range(0, 45)] public float HalfWidthDegrees = 7.5f;
        public float AngularSpeed = 27;
        public float EnragedSpeedMultiplier = 1.22f;
        [Min(.01f)] public float HitInterval = .72f;
        [Min(0)] public int Damage = 1;
        public int Number(float hp01) => hp01 <= EnragedHealthFraction ? EnragedCount : Count;
    }
    [Serializable]
    public sealed class BossRootSettings
    {
        [Min(0)] public float LockDuration = 1;
        [Range(0, 90)] public float HalfWidthDegrees = 12;
        public float AimSpeed = 92;
    }
    [Serializable]
    public sealed class BossGravitySettings
    {
        public float AnchorAngle = .72f;
        public float AnchorRadius = .46f;
        public float PlayerPullMin = .34f, PlayerPullMax = 1.34f;
        public float ProjectilePull = 5.6f, ParticlePull = 2.8f;
        public float MinDistanceSquared = .18f;
    }

    [Serializable]
    public sealed class BossEffectStyle
    {
        [NonSerialized] private MaterialPropertyBlock lineTint;
        [ColorUsage(true, true)] public Color Primary = Color.white;
        [ColorUsage(true, true)] public Color Secondary = Color.white;
        [ColorUsage(true, true)] public Color Highlight = Color.white;
        public Gradient RibbonGradient = new Gradient();
        public Gradient ParticleGradient = new Gradient();
        public AnimationCurve AlphaOverLife = AnimationCurve.Linear(0, 1, 1, 0);
        public AnimationCurve GlowOverLife = AnimationCurve.Linear(0, 1, 1, 0);
        [Min(0)] public float Brightness = 1;
        public Color Tint(Color color, float opacity = 1)
        { color.r *= Brightness; color.g *= Brightness; color.b *= Brightness; color.a *= opacity; return color; }
        public Color Fade(float t, bool glow = false)
        { var c = RibbonGradient.Evaluate(Mathf.Clamp01(t)); c.a *= (glow ? GlowOverLife : AlphaOverLife).Evaluate(Mathf.Clamp01(t)); return Tint(c); }
        public void ApplyLine(LineRenderer line, float opacity = 1)
        {
            line.colorGradient = RibbonGradient;
            if (lineTint == null) lineTint = new MaterialPropertyBlock();
            lineTint.SetColor("_Color", new Color(Brightness, Brightness, Brightness, opacity));
            line.SetPropertyBlock(lineTint);
        }
    }
}
