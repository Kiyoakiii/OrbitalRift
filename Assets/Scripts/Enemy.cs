using UnityEngine;

namespace OrbitalRift
{
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyKind Kind;
        [System.NonSerialized] public MobDefinition Mob;
        public ActorAppearanceView AppearanceView;
        public float VisualAge, SpellTimer; public int SpellIndex;
        public float Angle, Radius, Health, MaxHealth, FireTimer, Life;
        public float BossStateTimer;
        // The beam uses a separate angle from the boss orbit.  That keeps the
        // attack readable even while the creature is moving around the arena.
        public float BossBeamAngle;
        // Unlike a beam, roots lock a point on the player's orbit.
        public float BossRootAngle;
        public BossAiState BossState;
        public BossArchetype BossType;
        [System.NonSerialized] public BossDefinition Definition;
        [System.NonSerialized] public BossAbilityDefinition ActiveAbility;
        [System.NonSerialized] public BossAbilityDefinition SummonAbility;
        public float AbilityAge, AbilityDuration, BossAge;
        public bool AbilityActivated;
        public readonly System.Collections.Generic.Dictionary<BossAbilityDefinition, float> AbilityReadyAt = new System.Collections.Generic.Dictionary<BossAbilityDefinition, float>();
        public bool BossSecondLifeSpent;
        public float BossSpecialTimer;
        public float CloneOrbitDirection;
        public Sprite BossMainSprite;
        public int Points;
        public SpriteRenderer Renderer;
        public VoidMawBossPresentation BossPresentation;
        public FirebirdBossPresentation FirebirdPresentation;
        public HarrierBossPresentation HarrierPresentation;
        private Sprite fallbackSprite;

        private void Awake() { Renderer = GetComponent<SpriteRenderer>(); fallbackSprite = Renderer.sprite; }
        public void ResetEnemy(EnemyKind kind, float angle, int phase, Sprite customSprite)
        {
            Renderer.enabled = true; Mob = null; VisualAge = SpellTimer = 0; SpellIndex = 0; AppearanceView?.Configure(null, Renderer);
            Definition = null; ActiveAbility = null; SummonAbility = null;
            AbilityAge = AbilityDuration = BossAge = 0; AbilityReadyAt.Clear();
            AbilityActivated = false;
            // Босс не должен исчезнуть сам по таймеру: переход к ядру возможен
            // только после того, как игрок снимет весь его запас здоровья.
            Kind = kind; Angle = angle; Radius = .95f; Life = kind == EnemyKind.Boss ? 999f : kind == EnemyKind.ShadeClone ? 5.6f : 18f;
            FireTimer = Random.Range(BalanceSettings.EnemyFireInterval(phase) * .85f, BalanceSettings.EnemyFireInterval(phase) * 1.45f);
            Health = kind == EnemyKind.Boss ? BossSettings.Health : kind == EnemyKind.ShadeClone ? 3 : kind == EnemyKind.Turret ? 5 : kind == EnemyKind.Diver ? 2 : 1;
            MaxHealth = Health;
            Points = kind == EnemyKind.Boss ? BossSettings.Points : kind == EnemyKind.ShadeClone ? 120 : kind == EnemyKind.Scout ? 100 : kind == EnemyKind.Spiral ? 175 : kind == EnemyKind.Diver ? 250 : 350;
            BossState = BossAiState.Orbit;
            BossStateTimer = kind == EnemyKind.Boss ? 2.4f : 0f;
            BossBeamAngle = 0f;
            BossRootAngle = 0f;
            BossType = BossArchetype.VoidMaw;
            BossSecondLifeSpent = false;
            BossSpecialTimer = 0f;
            CloneOrbitDirection = 1f;
            BossMainSprite = customSprite;
            var fallbackColor = kind == EnemyKind.Scout ? new Color(1f,.55f,.12f) : kind == EnemyKind.Spiral ? new Color(1f,.16f,.45f) : kind == EnemyKind.Diver ? new Color(.95f,.25f,.8f) : kind == EnemyKind.ShadeClone ? new Color(.56f,.24f,1f,.58f) : kind == EnemyKind.Boss ? new Color(.62f,.2f,1f) : new Color(1f,.8f,.18f);
            Renderer.sprite = customSprite != null ? customSprite : fallbackSprite;
            Renderer.color = customSprite != null ? Color.white : fallbackColor;
            var desiredSize = kind == EnemyKind.Boss ? BossSettings.WorldSize : kind == EnemyKind.ShadeClone ? .72f : kind == EnemyKind.Turret ? .32f : .38f;
            var spriteSize = Mathf.Max(Renderer.sprite.bounds.size.x, Renderer.sprite.bounds.size.y);
            transform.localScale = spriteSize > .0001f ? Vector3.one * (desiredSize / spriteSize) : Vector3.one * desiredSize;
            Renderer.transform.rotation = Quaternion.identity;
            if (kind != EnemyKind.Boss && BossPresentation != null) BossPresentation.SetVisible(false);
            if (kind != EnemyKind.Boss && FirebirdPresentation != null) FirebirdPresentation.SetVisible(false);
            if (kind != EnemyKind.Boss && HarrierPresentation != null) HarrierPresentation.SetVisible(false);
        }
    }

}
