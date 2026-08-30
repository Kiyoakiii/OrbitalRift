#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Детерминированные проверки формул. Не требуют запуска сцены и поэтому
    /// выполняются за доли секунды перед каждой Android-сборкой.
    /// </summary>
    public static class GameplayRulesValidator
    {
        [MenuItem("Orbital Rift/Run Gameplay Rules Tests")]
        public static void RunFromMenu()
        {
            var errors = new List<string>();
            Validate(errors);
            if (errors.Count > 0)
                throw new BuildFailedException("Gameplay rules tests failed:\n- " + string.Join("\n- ", errors));
            Debug.Log("Orbital Rift gameplay rules tests passed.");
        }

        public static void Validate(List<string> errors)
        {
            ValidateMmr(errors);
            ValidateDifficultyCurve(errors);
            ValidateCoreConstants(errors);
            ValidatePlayerCommandContract(errors);
            ValidateElementalRules(errors);
            ValidateShipLoadouts(errors);
            ValidateProceduralSectors(errors);
            ValidateCoopSimulation(errors);
        }

        private static void ValidateMmr(List<string> errors)
        {
            if (MmrSettings.CalculateChange(100, 100) != MmrSettings.MinimumGain)
                errors.Add("A run equal to current MMR must award the minimum gain.");
            if (MmrSettings.CalculateChange(200, 100) != MmrSettings.MaximumGain)
                errors.Add("A run at twice current MMR must award the maximum gain.");
            if (MmrSettings.CalculateChange(0, 1000) != -MmrSettings.MaximumLoss)
                errors.Add("A zero-score run must not exceed the configured maximum loss.");
            if (MmrSettings.CalculateChange(0, 25) != -25)
                errors.Add("MMR loss must stop at the zero-rating floor.");

            var previous = int.MinValue;
            for (var score = 0; score <= 2000; score += 10)
            {
                var change = MmrSettings.CalculateChange(score, 1000);
                if (change < previous)
                {
                    errors.Add("MMR reward must be monotonic as run score increases.");
                    break;
                }
                previous = change;
            }
        }

        private static void ValidateDifficultyCurve(List<string> errors)
        {
            var previousMove = 0f;
            var previousShotSpeed = 0f;
            var previousFireInterval = float.MaxValue;
            var previousSpawnInterval = float.MaxValue;
            var previousPlayerSpeed = 0f;
            var previousPlayerInterval = float.MaxValue;
            for (var phase = 1; phase <= 40; phase++)
            {
                var move = BalanceSettings.EnemyMovementMultiplier(phase);
                var shotSpeed = BalanceSettings.EnemyProjectileSpeed(phase);
                var fireInterval = BalanceSettings.EnemyFireInterval(phase);
                var spawnInterval = BalanceSettings.SpawnInterval(phase);
                var playerSpeed = BalanceSettings.PlayerProjectileSpeed(phase);
                var playerInterval = BalanceSettings.PlayerFireInterval(phase, false);

                if (move + .0001f < previousMove || move > BalanceSettings.EnemyMoveMultiplierMax + .0001f)
                    errors.Add("Enemy movement curve is not monotonic or exceeds its cap at phase " + phase + ".");
                if (shotSpeed + .0001f < previousShotSpeed || shotSpeed > BalanceSettings.EnemyProjectileSpeedMax + .0001f)
                    errors.Add("Enemy projectile curve is not monotonic or exceeds its cap at phase " + phase + ".");
                if (fireInterval > previousFireInterval + .0001f || fireInterval < BalanceSettings.EnemyFireIntervalMin - .0001f)
                    errors.Add("Enemy fire interval curve is invalid at phase " + phase + ".");
                if (spawnInterval > previousSpawnInterval + .0001f || spawnInterval < BalanceSettings.SpawnIntervalMin - .0001f)
                    errors.Add("Spawn interval curve is invalid at phase " + phase + ".");
                if (playerSpeed + .0001f < previousPlayerSpeed || playerInterval > previousPlayerInterval + .0001f)
                    errors.Add("Player upgrade curve regresses at phase " + phase + ".");

                previousMove = move;
                previousShotSpeed = shotSpeed;
                previousFireInterval = fireInterval;
                previousSpawnInterval = spawnInterval;
                previousPlayerSpeed = playerSpeed;
                previousPlayerInterval = playerInterval;
            }
        }

        private static void ValidateCoreConstants(List<string> errors)
        {
            var bossPhase = BossSettings.Phase;
            var bossHealth = BossSettings.Health;
            var bossAimInterval = BossSettings.AimBurstInterval;
            var bossBarrageInterval = BossSettings.BarrageInterval;
            var orbitRadius = OrbitSettings.Radius;
            var orbitLineWidth = OrbitSettings.LineWidth;
            var orbitSegments = OrbitSettings.Segments;
            var bonusSpeed = BonusSettings.Speed;
            var bonusLifetime = BonusSettings.Lifetime;
            var starsPerSecond = StarStreamSettings.StarsPerSecond;
            var shieldTrailLength = StarStreamSettings.ShieldTrailLength;

            if (bossPhase != 3) errors.Add("The first boss must remain on phase 3.");
            if (bossHealth <= 0f || bossAimInterval <= 0f || bossBarrageInterval <= 0f)
                errors.Add("Boss health and attack intervals must be positive.");
            if (orbitRadius <= 0f || orbitLineWidth <= 0f || orbitSegments < 24)
                errors.Add("Orbit geometry settings are invalid.");
            if (bonusSpeed <= 0f || bonusLifetime <= 0f)
                errors.Add("Bonus flight settings are invalid.");
            if (starsPerSecond <= 0f || shieldTrailLength <= 0f)
                errors.Add("Star stream settings are invalid.");
        }

        private static void ValidatePlayerCommandContract(List<string> errors)
        {
            if (new PlayerCommandFrame(-7, false, false).OrbitDirection != -1 ||
                new PlayerCommandFrame(8, false, false).OrbitDirection != 1 ||
                new PlayerCommandFrame(0, false, false).OrbitDirection != 0)
                errors.Add("Player commands must normalize orbit input to -1, 0 or 1 for network transport.");
        }

        private static void ValidateElementalRules(List<string> errors)
        {
            var elements = new[] { DamageElement.Kinetic, DamageElement.Fire, DamageElement.Cold, DamageElement.Poison };
            foreach (var first in elements)
            foreach (var second in elements)
                if (ElementalCombat.ResolveReaction(first, second) != ElementalCombat.ResolveReaction(second, first))
                    errors.Add("Elemental reactions must be symmetrical for network simulation.");

            if (ElementalCombat.ResolveReaction(DamageElement.Fire, DamageElement.Poison) != ElementalReaction.Ignition ||
                ElementalCombat.ResolveReaction(DamageElement.Cold, DamageElement.Poison) != ElementalReaction.Cryotoxin ||
                ElementalCombat.ResolveReaction(DamageElement.Fire, DamageElement.Cold) != ElementalReaction.Steam ||
                ElementalCombat.ResolveReaction(DamageElement.Cold, DamageElement.Kinetic) != ElementalReaction.Shatter)
                errors.Add("The core Resonance reaction matrix is incomplete.");

            if (ElementalCombat.ReactionBonus(ElementalReaction.Ignition) <= 0 ||
                ElementalCombat.ReactionBonus(ElementalReaction.Shatter) <= ElementalCombat.ReactionBonus(ElementalReaction.Steam) ||
                string.IsNullOrWhiteSpace(ElementalCombat.ReactionLabel(ElementalReaction.Overcharge)))
                errors.Add("Resonance reactions must expose readable labels and positive tactical bonuses.");

            foreach (var element in elements)
                if (BossSettings.Resistance(element) < .25f || BossSettings.Resistance(element) > 2f)
                    errors.Add("Boss elemental resistance must remain useful and cannot become immunity.");
            if (Mathf.Abs(BossSettings.Resistance(DamageElement.Kinetic) - 1f) > .001f)
                errors.Add("Kinetic damage must preserve the current solo boss balance.");
        }

        private static void ValidateShipLoadouts(List<string> errors)
        {
            var usedElements = new HashSet<DamageElement>();
            for (var i = 0; i < ShipLoadoutSettings.Count; i++)
            {
                var loadout = ShipLoadoutSettings.Get((ShipArchetype)i);
                if (loadout.FireIntervalMultiplier < .5f || loadout.FireIntervalMultiplier > 1.5f ||
                    loadout.ProjectileSpeedMultiplier < .5f || loadout.ProjectileSpeedMultiplier > 1.5f ||
                    loadout.DamageMultiplier < .5f || loadout.DamageMultiplier > 1.5f)
                    errors.Add("Ship loadout multipliers are outside the initial balance envelope.");
                if (!usedElements.Add(loadout.Element))
                    errors.Add("Initial ship archetypes must demonstrate four different elements.");
            }

            var defaultShip = ShipLoadoutSettings.Get(ShipArchetype.Vanguard);
            if (defaultShip.Element != DamageElement.Kinetic ||
                Mathf.Abs(defaultShip.FireIntervalMultiplier - 1f) > .001f ||
                Mathf.Abs(defaultShip.ProjectileSpeedMultiplier - 1f) > .001f ||
                Mathf.Abs(defaultShip.DamageMultiplier - 1f) > .001f)
                errors.Add("Vanguard must preserve the original solo combat balance.");
        }

        private static void ValidateProceduralSectors(List<string> errors)
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var first = SectorGenerator.Generate(seed);
                var second = SectorGenerator.Generate(seed);
                if (!SectorGenerator.Validate(first, out var validationError))
                {
                    errors.Add("Invalid procedural sector at seed " + seed + ": " + validationError);
                    return;
                }
                if (first.Signature() != second.Signature())
                {
                    errors.Add("Procedural sector generation is not deterministic at seed " + seed + ".");
                    return;
                }
            }

            if (SectorGenerator.Generate(17).Signature() == SectorGenerator.Generate(18).Signature())
                errors.Add("Different sector seeds must not collapse to the same layout.");
        }

        private static void ValidateCoopSimulation(List<string> errors)
        {
            if (Mathf.Abs(CoopSimulationRules.StepAngle(359f, 1, 1f) - 114f) > .001f)
                errors.Add("Coop ship angles must wrap at 360 degrees.");
            if (Mathf.Abs(CoopSimulationRules.StepAngle(30f, 9, 1f) - 145f) > .001f)
                errors.Add("Network orbit input must be clamped before host simulation.");
            if (Mathf.Abs(CoopSimulationRules.StepAngle(30f, -1, -3f) - 30f) > .001f)
                errors.Add("Negative network delta time must not move a ship.");

            var eventRoom = new SectorRoom(0, SectorRoomType.Event, 2, 8);
            var eliteRoom = new SectorRoom(1, SectorRoomType.Elite, 2, 8);
            if (CoopRoomRules.EnemyHealth(eventRoom) >= CoopRoomRules.EnemyHealth(eliteRoom))
                errors.Add("Procedural event rooms must be safer than elite rooms.");
            if (CoopRoomRules.ThreatPulseInterval(SectorRoomType.Event) <= CoopRoomRules.ThreatPulseInterval(SectorRoomType.Elite))
                errors.Add("Event rooms must provide a calmer threat pulse window than elite rooms.");
            if (CoopRoomRules.ReactionBonusMultiplier(SectorRoomType.Event) <= 1f)
                errors.Add("Event rooms must reward coordinated elemental reactions.");
            if (CoopRoomRules.TeamMaxHealth <= 0 || CoopRoomRules.TeamMaxHealth > 20)
                errors.Add("Coop team hull reserve must stay inside the playable envelope.");
            if (CoopRoomRules.ThreatDamage(SectorRoomType.Event) != 0 || CoopRoomRules.ThreatDamage(SectorRoomType.Shop) != 0)
                errors.Add("Event and shop rooms must not damage the shared team hull.");
            if (CoopRoomRules.ThreatDamage(SectorRoomType.Boss) <= CoopRoomRules.ThreatDamage(SectorRoomType.Combat))
                errors.Add("Boss pulses must deal more team hull damage than standard combat.");
            if (CoopRoomRules.TeamDamageCooldown(SectorRoomType.Boss) <= 0f)
                errors.Add("Boss team damage cooldown must be positive.");

            if (CoopTrajectorySettings.HoldDuration < 5f || CoopTrajectorySettings.TransitionDuration < 2f ||
                CoopTrajectorySettings.LineSegments < 96)
                errors.Add("Coop trajectory timing or line resolution is too aggressive for mobile play.");
            var circleState = CoopTrajectorySettings.Evaluate(0f);
            var ellipseState = CoopTrajectorySettings.Evaluate(CoopTrajectorySettings.StageDuration);
            var eightState = CoopTrajectorySettings.Evaluate(CoopTrajectorySettings.StageDuration * 2f);
            if (circleState.From != CoopTrajectoryShape.Circle || circleState.IsTransitioning ||
                ellipseState.From != CoopTrajectoryShape.Ellipse || eightState.From != CoopTrajectoryShape.FigureEight)
                errors.Add("Coop trajectory must cycle circle, ellipse, figure-eight in a deterministic order.");
            var halfMorph = CoopTrajectorySettings.Evaluate(CoopTrajectorySettings.HoldDuration + CoopTrajectorySettings.TransitionDuration * .5f);
            if (!halfMorph.IsTransitioning || Mathf.Abs(halfMorph.Blend - .5f) > .001f)
                errors.Add("Coop trajectory morph must be smooth and reach an exact midpoint.");
            var circleRight = CoopTrajectorySettings.Position(0f, CoopTrajectoryShape.Circle);
            var ellipseTop = CoopTrajectorySettings.Position(90f, CoopTrajectoryShape.Ellipse);
            var eightCrossing = CoopTrajectorySettings.Position(90f, CoopTrajectoryShape.FigureEight);
            if (Vector2.Distance(circleRight, new Vector2(OrbitSettings.Radius, 0f)) > .001f ||
                Mathf.Abs(ellipseTop.y - OrbitSettings.Radius * CoopTrajectorySettings.EllipseVerticalScale) > .001f ||
                eightCrossing.sqrMagnitude > .001f)
                errors.Add("Coop trajectory geometry does not match its circle, ellipse and figure-eight contract.");
            if (OrbitSettings.Radius * CoopTrajectorySettings.EllipseHorizontalScale > OrbitSettings.Radius + .55f)
                errors.Add("Coop ellipse exceeds the mobile camera framing margin.");

            var collidingHostAngle = 90f;
            var collidingGuestAngle = 270f;
            var figureEightTime = CoopTrajectorySettings.StageDuration * 2f;
            if (!CoopSimulationRules.TryBounceShips(ref collidingHostAngle, ref collidingGuestAngle,
                    figureEightTime, out var collisionPosition))
                errors.Add("Coop ships must collide at the figure-eight crossing.");
            else
            {
                var bouncedHost = CoopTrajectorySettings.Position(collidingHostAngle, figureEightTime);
                var bouncedGuest = CoopTrajectorySettings.Position(collidingGuestAngle, figureEightTime);
                if (Vector2.Distance(bouncedHost, bouncedGuest) <= CoopSimulationRules.ShipCollisionDistance)
                    errors.Add("Coop collision bounce must separate both ships immediately.");
                if (collisionPosition.sqrMagnitude > .001f)
                    errors.Add("The figure-eight crossing collision point must remain at the arena center.");
            }

            var separatedHostAngle = 210f;
            var separatedGuestAngle = 330f;
            if (CoopSimulationRules.TryBounceShips(ref separatedHostAngle, ref separatedGuestAngle, 0f, out _))
                errors.Add("Separated coop ships must not trigger a false collision on the circle trajectory.");
        }
    }
}
#endif
