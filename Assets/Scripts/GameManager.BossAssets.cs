using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    // Shared execution for configured bosses. Only egg/beam/root/summon behaviour
    // needs a distinct branch; identities, art, numbers and attack order are data.
    public sealed partial class GameManager
    {
        readonly Dictionary<Enemy, ObjectPool<Enemy>> configuredEnemyPools = new Dictionary<Enemy, ObjectPool<Enemy>>();
        readonly Dictionary<Enemy, ObjectPool<Enemy>> enemyOwners = new Dictionary<Enemy, ObjectPool<Enemy>>();
        readonly Dictionary<Projectile, ObjectPool<Projectile>> configuredShotPools = new Dictionary<Projectile, ObjectPool<Projectile>>();
        readonly Dictionary<Projectile, ObjectPool<Projectile>> shotOwners = new Dictionary<Projectile, ObjectPool<Projectile>>();

        Enemy GetConfiguredEnemy(Enemy prefab)
        {
            if (prefab == null) return enemyPool.Get();
            if (!configuredEnemyPools.TryGetValue(prefab, out var pool))
                configuredEnemyPools.Add(prefab, pool = new ObjectPool<Enemy>(prefab, arena, 2));
            var enemy = pool.Get(); enemyOwners[enemy] = pool; return enemy;
        }
        void ReleaseConfiguredEnemy(Enemy enemy)
        { if (enemyOwners.TryGetValue(enemy, out var pool)) pool.Release(enemy); else enemyPool.Release(enemy); }
        void ReleaseConfiguredShot(Projectile shot)
        { if (shotOwners.TryGetValue(shot, out var pool)) pool.Release(shot); else projectilePool.Release(shot); }

        void ConfigureBossData(Enemy boss, BossDefinition definition)
        {
            boss.Definition = definition;
            boss.Health = boss.MaxHealth = definition.MaxHp;
            boss.Points = definition.Points;
            boss.Radius = definition.OrbitRadius;
            SetSpriteWorldSize(boss.Renderer, definition.WorldSize);
            BeginBossAbility(boss, definition.InitialAbility, 1, false);
            boss.AbilityDuration = definition.InitialStateDuration;
            boss.BossStateTimer = boss.AbilityDuration;
        }

        void UpdateBoss(Enemy boss, float dt)
        {
            if (boss.Definition == null) ConfigureBossData(boss, BossAssetRegistry.Get(boss.BossType));
            boss.BossAge += dt; boss.AbilityAge += dt; boss.AppearanceView?.Tick(boss.BossAge);
            var ability = boss.ActiveAbility;
            if (ability == null) return;
            if (ability.Behaviour == BossAbilityBehaviour.RebirthEgg)
            {
                boss.BossStateTimer = Mathf.Max(0, boss.AbilityDuration - boss.AbilityAge);
                boss.Radius = Mathf.MoveTowards(boss.Radius, boss.BossSpecialTimer, dt * ability.Movement.RadiusSpeed);
                boss.Angle += dt * ability.Movement.AngularSpeed;
                PlaceBoss(boss);
                boss.FirebirdPresentation?.Render(boss, true);
                if (boss.BossStateTimer <= 0) ReviveFirebird(boss);
                return;
            }
            if (boss.AbilityAge >= ability.CastDelay + boss.AbilityDuration)
            {
                spellVfxPool?.EmitImpact(boss.transform.position, ability.AftereffectPrefab, ability.Vfx);
                ChooseConfiguredBossAbility(boss);
                ability = boss.ActiveAbility;
            }
            var casting = boss.AbilityAge < ability.CastDelay;
            boss.BossStateTimer = Mathf.Max(0, (casting ? ability.CastDelay : ability.CastDelay + boss.AbilityDuration) - boss.AbilityAge);
            if (!casting && !boss.AbilityActivated)
            {
                boss.AbilityActivated = true;
                if (ability.Behaviour == BossAbilityBehaviour.Summon)
                    SpawnHarrierClones(boss, BossHp(boss) <= ability.Summon.EnragedHealthFraction ? ability.Summon.EnragedCount : ability.Summon.Count, ability);
                if (ability.Behaviour == BossAbilityBehaviour.Roots) TryApplyGravroot(boss);
            }
            MoveConfiguredBoss(boss, casting && ability.UseCastMovement ? ability.CastMovement : ability.Movement, dt);
            if (ability.Behaviour == BossAbilityBehaviour.Beam)
            {
                boss.BossState = casting ? BossAiState.BeamTelegraph : BossAiState.BeamSweep;
                if (casting)
                {
                    var direction = (Vector2)player.position - (Vector2)boss.transform.position;
                    boss.BossBeamAngle = Mathf.MoveTowardsAngle(boss.BossBeamAngle, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, dt * 110);
                }
                else
                {
                    var beam = ability.Beam;
                    boss.BossBeamAngle += dt * beam.AngularSpeed * (BossHp(boss) <= beam.EnragedHealthFraction ? beam.EnragedSpeedMultiplier : 1);
                    if (boss.FireTimer <= 0) boss.FireTimer = TryDamagePlayerWithBossBeam(boss) ? beam.HitInterval : .12f;
                }
            }
            else if (ability.Behaviour == BossAbilityBehaviour.Roots)
            {
                boss.BossState = casting ? BossAiState.RootTelegraph : BossAiState.RootLock;
                if (casting) boss.BossRootAngle = Mathf.MoveTowardsAngle(boss.BossRootAngle, playerAngle * Mathf.Rad2Deg, dt * ability.Roots.AimSpeed);
            }
            else if (!casting && boss.FireTimer <= 0)
            {
                var attack = ability.SecondaryAbility != null ? ability.SecondaryAbility : ability;
                if (attack.Shot != null && (attack == ability || boss.AbilityAge <= ability.CastDelay + attack.CastDelay + attack.Duration))
                {
                    FireConfiguredBossShot(boss, attack.Shot);
                    boss.FireTimer = attack.Shot.FireInterval;
                    if (ability.Behaviour == BossAbilityBehaviour.Dash) boss.FirebirdPresentation?.TriggerPhoenixDiveBurst();
                }
            }
            PlaceBoss(boss);
            if(!boss.Definition.UseLegacyPresentation) { var view=boss.GetComponent<ConfiguredAttackView>()??boss.gameObject.AddComponent<ConfiguredAttackView>();view.Render(boss,casting,ShipOrbitCenter); return; }
            boss.FirebirdPresentation?.Render(boss, false);
            boss.HarrierPresentation?.Render(boss, ability.Behaviour == BossAbilityBehaviour.Dash);
            if (boss.BossPresentation != null)
            {
                var warmup = casting && ability.CastDelay > 0 ? Mathf.Clamp01(boss.AbilityAge / ability.CastDelay) : 0;
                boss.BossPresentation.Render(boss, boss.BossBeamAngle, ability.Behaviour == BossAbilityBehaviour.Beam ? warmup : 0,
                    !casting && ability.Behaviour == BossAbilityBehaviour.Beam, boss.BossRootAngle,
                    ability.Behaviour == BossAbilityBehaviour.Roots ? warmup : 0, !casting && ability.Behaviour == BossAbilityBehaviour.Roots, ShipOrbitCenter);
            }
        }
        static float BossHp(Enemy boss) => boss.MaxHealth <= 0 ? 1 : Mathf.Clamp01(boss.Health / boss.MaxHealth);
        static void PlaceBoss(Enemy boss) => boss.transform.position = new Vector2(Mathf.Cos(boss.Angle), Mathf.Sin(boss.Angle)) * boss.Radius;
        void MoveConfiguredBoss(Enemy boss, BossMovementSettings movement, float dt)
        {
            var radius = movement.Radius + Mathf.Sin(Time.time * movement.SwayFrequency) * movement.SwayAmplitude;
            boss.Radius = movement.MoveTowardsRadius ? Mathf.MoveTowards(boss.Radius, radius, dt * movement.RadiusSpeed) : Mathf.Lerp(boss.Radius, radius, dt * movement.RadiusSpeed);
            if (movement.AngularMotion == BossAngularMotion.Spin) boss.Angle += dt * movement.AngularSpeed;
            else boss.Angle = MoveTowardsAngleRadians(boss.Angle, Mathf.Atan2(player.position.y, player.position.x) + movement.PlayerAngleOffset, dt * movement.AngularSpeed);
        }
        void ChooseConfiguredBossAbility(Enemy boss)
        {
            var phaseData = boss.Definition.Phase(BossHp(boss));
            if (phaseData != null)
                foreach (var entry in phaseData.Attacks)
                {
                    var ability = entry.Ability;
                    if (ability == null || BossHp(boss) > ability.MaxHealthFraction) continue;
                    if (boss.AbilityReadyAt.TryGetValue(ability, out var ready) && boss.BossAge < ready) continue;
                    if (Random.value >= entry.Chance) continue;
                    BeginBossAbility(boss, ability, entry.DurationMultiplier); return;
                }
            BeginBossAbility(boss, boss.Definition.InitialAbility);
        }
        void BeginBossAbility(Enemy boss, BossAbilityDefinition ability, float durationMultiplier = 1, bool announce = true)
        {
            boss.ActiveAbility = ability; boss.AbilityAge = 0;
            if (boss.Definition != null) ConfigureAppearance(boss, ability != null && ability.Appearance.Enabled ? ability.Appearance : boss.Definition.Appearance); boss.AbilityActivated = false;
            if (ability == null) return;
            boss.AbilityDuration = ability.Duration * durationMultiplier;
            boss.BossState = ability.State; boss.BossStateTimer = ability.CastDelay + boss.AbilityDuration;
            boss.FireTimer = ability.CastDelay + ability.FirstShotDelay;
            boss.AbilityReadyAt[ability] = boss.BossAge + ability.Cooldown;
            if (ability.Behaviour == BossAbilityBehaviour.Roots) boss.BossRootAngle = playerAngle * Mathf.Rad2Deg;
            if (!announce) return;
            if (ability.ShowInGuide) { phaseUpgradeBannerTimer = .88f; phaseUpgradeLabel = ability.DisplayName; }
            spellVfxPool?.EmitImpact(boss.transform.position, ability.CastPrefab, ability.Vfx);
            if (ability.Sound != null) PlayEffect(ability.Sound, .8f);
            if (ability.Shake > 0) AddScreenShake(.12f, ability.Shake);
        }
        void FireConfiguredBossShot(Enemy boss, BossShotDefinition shot)
        {
            if (shot == null || player == null) return;
            var direction = ((Vector2)player.position - (Vector2)boss.transform.position).normalized;
            var count = Mathf.Clamp(shot.ProjectileCount, 1, 80);
            for (var i = 0; i < count; i++)
            {
                var angle = boss.Angle + i * Mathf.PI * 2 / count;
                var heading = shot.Pattern == BossShotPattern.Radial ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) : Rotate(direction, (i - (count - 1) * .5f) * shot.SpreadDegrees);
                SpawnConfiguredShot(shot, boss.transform.position, heading);
            }
            if (shot.Sound != null) PlayEffect(shot.Sound, .8f);
            if (shot.Shake > 0) AddScreenShake(.1f, shot.Shake);
        }
        Projectile SpawnConfiguredShot(BossShotDefinition shot, Vector2 position, Vector2 heading)
        {
            if (projectiles.Count >= GameRules.Current.ProjectileCap) return null;
            Projectile p;
            if (shot.Prefab != null)
            {
                if (!configuredShotPools.TryGetValue(shot.Prefab, out var pool)) configuredShotPools.Add(shot.Prefab, pool = new ObjectPool<Projectile>(shot.Prefab, arena, 8));
                p = pool.Get(); shotOwners[p] = pool;
            }
            else p = projectilePool.Get();
            p.SetVisual(shot.Sprite != null ? shot.Sprite : circleSprite, shot.Sprite != null, true);
            p.ResetProjectile(position, heading.normalized * shot.FlightSpeed(phase), false, shot.Vfx != null ? shot.Vfx.CoreColor : Color.white, shot.Element, shot.Damage);
            p.Shot = shot; p.Life = shot.Lifetime; p.transform.localScale = Vector3.one * shot.Scale;
            var shared = shot.VfxPrefab != null && spellVfxPool != null && spellVfxPool.Attach(p, shot.VfxPrefab, shot.Vfx, shot.ImpactPrefab);
            p.SetSpellVisualStyle(shot.VisualStyle, !shared);
            if(shot.Appearance.Enabled) { var view=p.GetComponent<ActorAppearanceView>() ?? p.gameObject.AddComponent<ActorAppearanceView>();view.Configure(shot.Appearance,p.Renderer);p.Renderer.enabled=false; }
            p.SandboxBossEffect = abilitySandbox != null && abilitySandbox.IsOpen;
            projectiles.Add(p); return p;
        }
        void TickConfiguredShot(Projectile p, float dt)
        {
            if (p.Shot == null) return;
            p.ShotAge += dt; p.GetComponent<ActorAppearanceView>()?.Tick(p.ShotAge);
            if (p.Shot.Homing && player != null)
            {
                var target = ((Vector2)player.position - (Vector2)p.transform.position).normalized;
                var angle = Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg;
                var desired = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
                p.Velocity = Rotate(Vector2.right, Mathf.MoveTowardsAngle(angle, desired, p.Shot.HomingDegreesPerSecond * dt)) * p.Velocity.magnitude;
            }
        }
        void BeginFirebirdEgg(Enemy boss)
        {
            var ability = boss.Definition != null ? boss.Definition.Ability(BossAbilityBehaviour.RebirthEgg) : BossAssetRegistry.Ability(BossAbilityId.FirebirdAshenEgg);
            BeginBossAbility(boss, ability);
            boss.BossSecondLifeSpent = true; boss.BossSpecialTimer = Random.Range(ability.Egg.RadiusRange.x, ability.Egg.RadiusRange.y);
            boss.Angle += Random.Range(-ability.Egg.AngleScatter, ability.Egg.AngleScatter);
            boss.FireTimer = 99; boss.Health = boss.MaxHealth = ability.Egg.Health;
            boss.Renderer.sprite = circleSprite; boss.Renderer.color = ability.Style.Primary;
            SetSpriteWorldSize(boss.Renderer, ability.Egg.Size);
            phaseUpgradeBannerTimer = 1.4f;
            phaseUpgradeLabel = ability.DisplayName + "\n" + ability.Egg.Health.ToString("0.#") + " HP // " + ability.Duration.ToString("0.#") + " СЕК";
        }
        void ReviveFirebird(Enemy boss)
        {
            var egg = boss.ActiveAbility;
            boss.Health = egg.Egg.ReviveHealth; boss.MaxHealth = boss.Definition.MaxHp;
            boss.Renderer.sprite = boss.BossMainSprite; boss.Renderer.color = Color.white;
            SetSpriteWorldSize(boss.Renderer, boss.Definition.WorldSize);
            boss.FirebirdPresentation?.TriggerEggBreakFlash();
            spellVfxPool?.EmitImpact(boss.transform.position, egg.AftereffectPrefab, egg.Vfx);
            BeginBossAbility(boss, boss.Definition.InitialAbility, 1, false);
            boss.AbilityDuration = egg.Egg.ReviveDelay;
            phaseUpgradeBannerTimer = 1.25f; phaseUpgradeLabel = "ПЕПЕЛ ДЫШИТ\nЖАР-ПТИЦА ВОЗРОЖДЕНА";
        }
        void SpawnHarrierClones(Enemy boss, int count, BossAbilityDefinition ability = null)
        {
            if (ability == null) ability = BossAssetRegistry.Ability(BossAbilityId.HarrierRiftCopies);
            var summon = ability.Summon;
            for (var i = 0; i < Mathf.Clamp(count, 0, 12); i++)
            {
                var clone = GetConfiguredEnemy(summon.Prefab);
                clone.ResetEnemy(EnemyKind.ShadeClone, boss.Angle + (i - (count - 1) * .5f) * .36f, phase, summon.Sprite);
                clone.SummonAbility = ability; clone.Health = clone.MaxHealth = summon.Health; clone.Life = summon.Lifetime;
                SetSpriteWorldSize(clone.Renderer, summon.Size);
                clone.Radius = boss.Radius + .16f + i * .08f; clone.CloneOrbitDirection = i % 2 == 0 ? 1 : -1;
                clone.FireTimer = .34f + i * .13f; clone.transform.position = boss.transform.position;
                enemies.Add(clone); spellVfxPool?.EmitImpact(clone.transform.position, ability.CastPrefab, ability.Vfx);
            }
        }
        void UpdateHarrierClone(Enemy clone, float dt)
        {
            var ability = clone.SummonAbility ?? BossAssetRegistry.Ability(BossAbilityId.HarrierRiftCopies);
            var s = ability.Summon;
            clone.Radius = Mathf.Lerp(clone.Radius, s.OrbitRadius, dt * 1.45f);
            clone.Angle += dt * (s.AngularSpeed + Mathf.Sin(Time.time * 2.5f + clone.GetInstanceID()) * s.AngularSway) * clone.CloneOrbitDirection;
            PlaceBoss(clone);
            var life01 = 1 - Mathf.Clamp01(clone.Life / Mathf.Max(.01f, s.Lifetime));
            clone.Renderer.color = ability.Style.Fade(life01);
            if (clone.FireTimer <= 0 && s.Shot != null)
            { SpawnConfiguredShot(s.Shot, clone.transform.position, (Vector2)player.position - (Vector2)clone.transform.position); clone.FireTimer = s.Shot.FireInterval; }
            if (player != null && invincible <= 0 && Vector2.Distance(clone.transform.position, player.position) < s.ContactRadius)
            {
                var side = Mathf.Sign(Mathf.DeltaAngle(playerAngle * Mathf.Rad2Deg, clone.Angle * Mathf.Rad2Deg));
                if (Mathf.Abs(side) < .01f) side = 1;
                targetAngle = playerAngle - side * s.ContactKnockbackDegrees * Mathf.Deg2Rad;
                invincible = .22f; AddScreenShake(.08f, .05f);
            }
        }
    }
}
