using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    public sealed partial class GameManager
    {
        private struct FlightSample { public float Time, Angle; }
        private struct GhostSegment { public Vector2 A, B; public float Time; }
        private struct SandboxPulse { public Vector2 Center; public float Start, Radius; public Color Color; }
        private struct SandboxCharge { public bool Positive; public Color OriginalColor; }

        private readonly List<FlightSample> sandboxFlight = new List<FlightSample>(160);
        private readonly List<GhostSegment> sandboxGhost = new List<GhostSegment>(48);
        private readonly List<SandboxPulse> sandboxPulses = new List<SandboxPulse>(24);
        private readonly HashSet<Projectile> sandboxWaveHits = new HashSet<Projectile>();
        private readonly Dictionary<Projectile, SandboxCharge> sandboxPolarities = new Dictionary<Projectile, SandboxCharge>();
        private int sandboxPolaritySequence;
        private readonly bool[] sandboxMines = new bool[8];
        private readonly Vector2[] sandboxCharges = new Vector2[3];
        private SandboxMechanicPresentation sandboxMechanicsFx;
        private Vector3 sandboxNormalPlayerScale;
        private Vector2 sandboxPreviousPlayer, sandboxGhostLastPoint;
        private float sandboxClock, sandboxSampleClock, sandboxGhostClock, sandboxGhostCooldown;
        private float sandboxAnchorTimer, sandboxAnchorAngle, sandboxAnchorEscape;
        private float sandboxReplayTimer, sandboxReplayFireTimer;
        private float sandboxDelayedTimer;
        private float sandboxReverseTimer;
        private float sandboxWaveTimer, sandboxWavePreviousRadius;
        private bool sandboxWaveHitPlayer, sandboxGhostWasEnabled;
        private float sandboxMineTimer, sandboxMineAngle, sandboxPolarityTimer, sandboxGrowthTimer;
        private float sandboxGrowth = 1f;

        private static readonly Color AnchorColor = new Color(.24f, 1f, .78f);
        private static readonly Color ReplayColor = new Color(.38f, .78f, 1f);
        private static readonly Color ChargeColor = new Color(1f, .73f, .24f);
        private static readonly Color ReverseColor = new Color(1f, .38f, .61f);
        private static readonly Color WaveColor = new Color(.58f, .52f, 1f);
        private static readonly Color MineColor = new Color(1f, .36f, .16f);
        private static readonly Color GhostColor = new Color(.48f, 1f, .68f);

        private void InitializeSandboxMechanics()
        {
            if (player == null) return;
            sandboxNormalPlayerScale = player.localScale;
            sandboxPreviousPlayer = sandboxGhostLastPoint = player.position;
            if (sandboxMechanicsFx == null)
                sandboxMechanicsFx = GetComponent<SandboxMechanicPresentation>() ?? gameObject.AddComponent<SandboxMechanicPresentation>();
            sandboxMechanicsFx.Configure(arena);
            if (sandboxLayeredVfx == null)
                sandboxLayeredVfx = GetComponent<SandboxLayeredVfx>() ?? gameObject.AddComponent<SandboxLayeredVfx>();
            sandboxLayeredVfx.Configure(arena, circleSprite != null ? circleSprite : whiteSprite,
                shipSprite != null ? shipSprite : whiteSprite);
            sandboxFlight.Add(new FlightSample { Time = -2f, Angle = playerAngle });
            sandboxFlight.Add(new FlightSample { Time = 0f, Angle = playerAngle });
        }

        private void ResetSandboxMechanics()
        {
            if (player != null && sandboxNormalPlayerScale.sqrMagnitude > 0f) player.localScale = sandboxNormalPlayerScale;
            sandboxNormalPlayerScale = Vector3.zero;
            sandboxMechanicsFx?.Hide();
            sandboxLayeredVfx?.Hide();
            ClearSandboxPolarities();
            sandboxFlight.Clear(); sandboxGhost.Clear(); sandboxPulses.Clear(); sandboxWaveHits.Clear();
            for (var i = 0; i < sandboxMines.Length; i++) sandboxMines[i] = false;
            sandboxClock = sandboxSampleClock = sandboxGhostClock = sandboxGhostCooldown = 0f;
            sandboxAnchorTimer = sandboxAnchorEscape = sandboxReplayTimer = sandboxReplayFireTimer = 0f;
            sandboxDelayedTimer = sandboxReverseTimer = sandboxWaveTimer = sandboxMineTimer = 0f;
            sandboxPolarityTimer = sandboxGrowthTimer = 0f;
            sandboxGrowth = 1f;
            sandboxGhostWasEnabled = false;
        }

        private bool TryTriggerSandboxMechanic(AbilitySandboxAbilityId ability)
        {
            if (ability < AbilitySandboxAbilityId.OrbitAnchor) return false;
            if (!abilitySandbox.IsOpen || player == null || ability == AbilitySandboxAbilityId.GhostTrail) return true;
            var remaining = abilitySandbox.CooldownRemaining(ability);
            if (remaining > 0f) { abilitySandbox.NotifyCooldown(ability, remaining); return true; }
            abilitySandbox.StartCooldown(ability);
            abilitySandbox.NotifyActivated(ability);
            BeginSandboxLayeredAbility(ability);
            switch (ability)
            {
                case AbilitySandboxAbilityId.OrbitAnchor:
                    sandboxAnchorAngle = playerAngle + (activeControlDirection == 0 ? 1 : activeControlDirection) * 1.22f;
                    sandboxAnchorTimer = 5f; sandboxAnchorEscape = 0f;
                    SandboxPulseAt(OrbitPoint(sandboxAnchorAngle), AnchorColor, .6f);
                    break;
                case AbilitySandboxAbilityId.TrajectoryReplay:
                    sandboxReplayTimer = 6f; sandboxReplayFireTimer = .2f;
                    SandboxPulseAt(OrbitPoint(SandboxRecordedAngle(sandboxClock - 2f)), ReplayColor, .7f);
                    break;
                case AbilitySandboxAbilityId.DelayedShot:
                    sandboxDelayedTimer = 1.2f;
                    for (var i = 0; i < 3; i++) sandboxCharges[i] = OrbitPoint(playerAngle + (i - 1) * .48f, 1.05f);
                    break;
                case AbilitySandboxAbilityId.CourseRupture:
                    // A small tangential constellation makes the reversal observable even in an empty room.
                    SandboxSeedProjectiles(8, ReverseColor, true);
                    ReverseSandboxCourses(); sandboxReverseTimer = 3f;
                    SandboxPulseAt(Vector2.zero, ReverseColor, OrbitSettings.Radius);
                    break;
                case AbilitySandboxAbilityId.GravityWave:
                    sandboxWaveTimer = 1.9f; sandboxWavePreviousRadius = 0f;
                    sandboxWaveHitPlayer = false; sandboxWaveHits.Clear();
                    break;
                case AbilitySandboxAbilityId.MineRing:
                    sandboxMineTimer = 8.8f; sandboxMineAngle = playerAngle + Mathf.PI / 8f;
                    for (var i = 0; i < sandboxMines.Length; i++) sandboxMines[i] = true;
                    break;
                case AbilitySandboxAbilityId.Polarity:
                    sandboxPolarityTimer = 6f;
                    SandboxSeedProjectiles(12, ReplayColor, false);
                    break;
                case AbilitySandboxAbilityId.Gigantism:
                    sandboxGrowthTimer = 5f;
                    SandboxPulseAt(player.position, ChargeColor, 1.4f);
                    break;
            }
            return true;
        }

        private Color SandboxAbilityColor(AbilitySandboxAbilityId ability)
        {
            var definition = abilitySandbox.Find(ability);
            return definition != null ? definition.Accent : new Color(.45f, .85f, 1f);
        }

        private Sprite SandboxAbilityCore(AbilitySandboxAbilityId ability)
        {
            switch (ability)
            {
                case AbilitySandboxAbilityId.SolarChicks: return firebirdChickProjectileSprite;
                case AbilitySandboxAbilityId.PhoenixDive: return solarLanceProjectileSprite;
                case AbilitySandboxAbilityId.HarrierColdFan:
                case AbilitySandboxAbilityId.HarrierPhaseDash: return harrierShardProjectileSprite;
                case AbilitySandboxAbilityId.VoidBarrage: return voidPulseProjectileSprite;
                default: return circleSprite != null ? circleSprite : whiteSprite;
            }
        }

        private void BeginSandboxLayeredAbility(AbilitySandboxAbilityId ability)
        {
            if (!abilitySandbox.IsOpen || sandboxLayeredVfx == null) return;
            // Solar Chicks now carries the same prefab through gameplay and Sandbox.
            if (ability == AbilitySandboxAbilityId.SolarChicks && solarChickPrefab != null) return;
            var origin = sandboxBoss != null ? (Vector2)sandboxBoss.transform.position : Vector2.zero;
            var target = player != null ? (Vector2)player.position : origin;
            sandboxLayeredVfx.BeginAbility(ability, origin, target, SandboxAbilityColor(ability), SandboxAbilityCore(ability));
        }

        private void ConsumeSandboxVfxEditorCommit()
        {
            if (sandboxLayeredVfx == null || abilitySandbox == null) return;
            var editor = abilitySandbox.VfxEditor;
            if (!editor.ConsumeCommitRequest()) return;
            sandboxLayeredVfx.CommitEditorPreview(editor.SelectedAbility, editor.LayerMask,
                editor.Scale, editor.Brightness, editor.Glow);
            abilitySandbox.NotifyStatus("VFX ПРОФИЛЬ ПРИМЕНЁН", new Color(.38f, 1f, .70f));
        }

        private void UpdateSandboxLayeredVfx(float dt)
        {
            if (!abilitySandbox.IsOpen || sandboxLayeredVfx == null) return;
            var playerPosition = player != null ? (Vector2)player.position : Vector2.zero;
            var bossPosition = sandboxBoss != null ? (Vector2)sandboxBoss.transform.position : Vector2.zero;
            var editor = abilitySandbox.VfxEditor;
            if (abilitySandbox.VfxEditorOpen)
            {
                sandboxLayeredVfx.SetEditorPreview(editor.SelectedAbility, bossPosition, playerPosition,
                    SandboxAbilityColor(editor.SelectedAbility), SandboxAbilityCore(editor.SelectedAbility),
                    editor.LayerMask, editor.Scale, editor.Brightness, editor.Glow);
            }
            else
            {
                sandboxLayeredVfx.ClearEditorPreview();
            }
            sandboxLayeredVfx.Tick(dt, playerPosition, bossPosition, OrbitSettings.Radius,
                abilitySandbox.HasPassive(AbilitySandboxAbilityId.SolarPlume),
                abilitySandbox.HasPassive(AbilitySandboxAbilityId.AegisOrbit),
                abilitySandbox.HasPassive(AbilitySandboxAbilityId.EchoResonator),
                abilitySandbox.HasPassive(AbilitySandboxAbilityId.EmberCore),
                abilitySandbox.HasPassive(AbilitySandboxAbilityId.KineticOverdrive),
                abilitySandbox.HasPassive(AbilitySandboxAbilityId.GhostTrail));
        }

        private void UpdateSandboxMechanics(float dt)
        {
            // The VFX workshop is a clean animation plate. Do not keep the
            // playable sandbox's mechanic strokes alive behind the inspected
            // spell; they would be recreated every frame after isolation.
            if (abilitySandbox != null && abilitySandbox.VfxEditorOpen)
            {
                sandboxMechanicsFx?.Hide();
                return;
            }
            if (sandboxMechanicsFx == null || player == null) return;
            sandboxClock += dt;
            sandboxMechanicsFx.BeginFrame();
            UpdateSandboxAnchor(dt);
            UpdateSandboxReversal(dt);
            UpdateSandboxDelayedShot(dt);
            UpdateSandboxWave(dt);
            UpdateSandboxMines(dt);
            UpdateSandboxPolarity(dt);
            UpdateSandboxGrowth(dt);
            RecordSandboxFlight(dt);
            UpdateSandboxReplay(dt);
            UpdateSandboxGhost(dt);
            RenderSandboxPulses();
            sandboxPreviousPlayer = player.position;
            sandboxMechanicsFx.EndFrame();
        }

        private void UpdateSandboxAnchor(float dt)
        {
            if (sandboxAnchorTimer <= 0f) return;
            sandboxAnchorTimer = Mathf.Max(0f, sandboxAnchorTimer - dt);
            var delta = Mathf.DeltaAngle(playerAngle * Mathf.Rad2Deg, sandboxAnchorAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (activeControlDirection != 0 && activeControlDirection * delta < -.01f) sandboxAnchorEscape += dt;
            else sandboxAnchorEscape = Mathf.Max(0f, sandboxAnchorEscape - dt * .6f);
            var anchor = OrbitPoint(sandboxAnchorAngle);
            if (sandboxAnchorEscape >= .75f || sandboxAnchorTimer <= 0f)
            {
                sandboxAnchorTimer = 0f;
                SandboxPulseAt(anchor, AnchorColor, .8f);
                abilitySandbox.NotifyStatus("ЯКОРЬ // СВЯЗЬ СНЯТА", AnchorColor);
                return;
            }
            var before = playerAngle;
            playerAngle = MoveTowardsAngleRadians(playerAngle, sandboxAnchorAngle, dt * .78f);
            targetAngle += Mathf.DeltaAngle(before * Mathf.Rad2Deg, playerAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            PositionOnOrbit();
            var strength = 1f - sandboxAnchorEscape / .75f;
            sandboxMechanicsFx.Ring(anchor, .20f, AnchorColor, .035f);
            sandboxMechanicsFx.Arc(anchor, .31f, sandboxClock * 2f, 5.2f * strength, Alpha(AnchorColor, .65f));
            sandboxMechanicsFx.Diamond(anchor, .13f, Mathf.PI * .25f, Color.white);
            sandboxMechanicsFx.Tether(player.position, anchor, Alpha(AnchorColor, strength * .8f), sandboxClock);
            for (var i = 0; i < 3; i++)
            {
                var p = Vector2.Lerp(player.position, anchor, Mathf.Repeat(sandboxClock * .7f + i / 3f, 1f));
                sandboxMechanicsFx.Diamond(p, .045f, sandboxClock, Alpha(AnchorColor, .8f));
            }
        }

        private void RecordSandboxFlight(float dt)
        {
            sandboxSampleClock += dt;
            if (sandboxSampleClock < 1f / 30f) return;
            sandboxSampleClock = 0f;
            sandboxFlight.Add(new FlightSample { Time = sandboxClock, Angle = playerAngle });
            while (sandboxFlight.Count > 2 && (sandboxFlight[1].Time < sandboxClock - 3f || sandboxFlight.Count > 128)) sandboxFlight.RemoveAt(0);
        }

        private float SandboxRecordedAngle(float time)
        {
            if (sandboxFlight.Count == 0) return playerAngle;
            if (time <= sandboxFlight[0].Time) return sandboxFlight[0].Angle;
            for (var i = 1; i < sandboxFlight.Count; i++)
            {
                if (sandboxFlight[i].Time < time) continue;
                var a = sandboxFlight[i - 1]; var b = sandboxFlight[i];
                return Mathf.LerpAngle(a.Angle * Mathf.Rad2Deg, b.Angle * Mathf.Rad2Deg,
                    Mathf.InverseLerp(a.Time, b.Time, time)) * Mathf.Deg2Rad;
            }
            return sandboxFlight[sandboxFlight.Count - 1].Angle;
        }

        private void UpdateSandboxReplay(float dt)
        {
            if (sandboxReplayTimer <= 0f) return;
            sandboxReplayTimer = Mathf.Max(0f, sandboxReplayTimer - dt);
            var angle = SandboxRecordedAngle(sandboxClock - 2f);
            var point = OrbitPoint(angle);
            var fade = Mathf.Clamp01(sandboxReplayTimer / .45f);
            for (var i = 3; i >= 0; i--)
            {
                var a = SandboxRecordedAngle(sandboxClock - 2f - i * .10f);
                sandboxMechanicsFx.Sprite(shipSprite, OrbitPoint(a), a * Mathf.Rad2Deg + 90f,
                    .95f - i * .08f, Alpha(ReplayColor, (i == 0 ? .8f : .16f) * fade));
            }
            sandboxMechanicsFx.Arc(point, .55f, sandboxClock * 2f, 4.8f, Alpha(ReplayColor, fade * .7f));
            for (var i = 1; i <= 20; i++)
                sandboxMechanicsFx.Line(OrbitPoint(SandboxRecordedAngle(sandboxClock - 2f - (i - 1) * .04f)),
                    OrbitPoint(SandboxRecordedAngle(sandboxClock - 2f - i * .04f)), Alpha(ReplayColor, (1f - i / 21f) * .55f), .028f);
            sandboxReplayFireTimer -= dt;
            if (sandboxReplayFireTimer <= 0f)
            {
                sandboxReplayFireTimer = .42f;
                Shoot(point, -point.normalized * 5f, true, ReplayColor, DamageElement.Cold, 0f, true);
                SandboxPulseAt(point, Alpha(ReplayColor, .65f), .34f);
            }
            if (sandboxReplayTimer <= 0f) SandboxPulseAt(point, ReplayColor, .7f);
        }

        private void UpdateSandboxDelayedShot(float dt)
        {
            if (sandboxDelayedTimer <= 0f) return;
            sandboxDelayedTimer = Mathf.Max(0f, sandboxDelayedTimer - dt);
            var charge = 1f - sandboxDelayedTimer / 1.2f;
            for (var i = 0; i < sandboxCharges.Length; i++)
            {
                var point = sandboxCharges[i];
                var direction = ((Vector2)player.position - point).normalized;
                sandboxMechanicsFx.Sprite(harrierShardProjectileSprite, point, sandboxClock * 120f + i * 45f,
                    .2f + charge * .18f, Color.Lerp(ChargeColor, Color.white, charge));
                sandboxMechanicsFx.Arc(point, .32f, sandboxClock * -3f, Mathf.PI * 2f * charge, ChargeColor, .035f);
                sandboxMechanicsFx.Line(point + direction * .36f, player.position, Alpha(ChargeColor, .16f), .012f);
                sandboxMechanicsFx.Chevron(point + direction * .48f, Mathf.Atan2(direction.y, direction.x), .1f, ChargeColor);
                if (sandboxDelayedTimer <= 0f)
                {
                    ShootStyledHostile(point, direction * 3.7f, ChargeColor, DamageElement.Kinetic, .22f, solarLanceProjectileSprite);
                    SandboxPulseAt(point, ChargeColor, .65f);
                }
            }
            if (sandboxDelayedTimer <= 0f) abilitySandbox.NotifyStatus("ОТСРОЧКА // ПУСК ПО НОВОЙ ЦЕЛИ", ChargeColor);
        }

        private void ReverseSandboxCourses()
        {
            for (var i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                var radial = ((Vector2)p.transform.position).normalized;
                var tangent = new Vector2(-radial.y, radial.x);
                p.Velocity -= 2f * Vector2.Dot(p.Velocity, tangent) * tangent;
            }
            for (var i = 0; i < enemies.Count; i++)
                if (enemies[i].Kind == EnemyKind.ShadeClone) enemies[i].CloneOrbitDirection *= -1f;
            for (var i = 0; i < damageShards.Count; i++)
            {
                var radial = ((Vector2)damageShards[i].transform.position).normalized;
                var tangent = new Vector2(-radial.y, radial.x);
                damageShards[i].Velocity -= 2f * Vector2.Dot(damageShards[i].Velocity, tangent) * tangent;
            }
            targetAngle = playerAngle;
        }

        private void UpdateSandboxReversal(float dt)
        {
            if (sandboxReverseTimer <= 0f) return;
            sandboxReverseTimer = Mathf.Max(0f, sandboxReverseTimer - dt);
            var fade = Mathf.Min(1f, sandboxReverseTimer / .3f);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI / 3f - sandboxClock * .8f;
                sandboxMechanicsFx.Arc(Vector2.zero, 2.76f, a, .62f, Alpha(ReverseColor, fade * .6f), .032f);
                sandboxMechanicsFx.Chevron(OrbitPoint(a, 2.76f), a - Mathf.PI * .5f, .18f, Alpha(ReverseColor, fade));
            }
            if (sandboxReverseTimer <= 0f)
            {
                ReverseSandboxCourses();
                abilitySandbox.NotifyStatus("РЕВЕРС // ОБЫЧНЫЙ КУРС", ReverseColor);
                SandboxPulseAt(player.position, ReverseColor, .8f);
            }
        }

        private void UpdateSandboxWave(float dt)
        {
            if (sandboxWaveTimer <= 0f) return;
            sandboxWaveTimer = Mathf.Max(0f, sandboxWaveTimer - dt);
            var elapsed = 1.9f - sandboxWaveTimer;
            if (elapsed < .4f)
            {
                sandboxMechanicsFx.Ring(Vector2.zero, .4f + elapsed, Alpha(WaveColor, .75f), .045f);
                for (var i = 0; i < 8; i++) sandboxMechanicsFx.Chevron(OrbitPoint(i * Mathf.PI * .25f, .8f), i * Mathf.PI * .25f, .16f, WaveColor);
                return;
            }
            var radius = (elapsed - .4f) / 1.5f * 4.7f;
            var fade = Mathf.Clamp01(sandboxWaveTimer / .35f);
            sandboxMechanicsFx.Ring(Vector2.zero, radius, Alpha(WaveColor, fade * .16f), .18f);
            sandboxMechanicsFx.Ring(Vector2.zero, radius, Alpha(Color.Lerp(WaveColor, Color.white, .4f), fade), .045f);
            sandboxMechanicsFx.Ring(Vector2.zero, Mathf.Max(.01f, radius - .18f), Alpha(WaveColor, fade * .4f), .024f);
            if (!sandboxWaveHitPlayer && radius >= OrbitSettings.Radius)
            {
                sandboxWaveHitPlayer = true;
                var before = (Vector2)player.position;
                playerAngle += .38f; targetAngle = playerAngle; PositionOnOrbit();
                SandboxPulseAt(before, WaveColor, .8f);
                SandboxPulseAt(player.position, WaveColor, .9f);
                abilitySandbox.NotifyStatus("ВОЛНА // СДВИГ НА 22°", WaveColor);
            }
            for (var i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                if (sandboxWaveHits.Contains(p)) continue;
                var point = (Vector2)p.transform.position;
                var previousDistance = (point - p.Velocity * dt).magnitude - sandboxWavePreviousRadius;
                var distance = point.magnitude - radius;
                if (previousDistance * distance > 0f && Mathf.Abs(distance) > .12f) continue;
                sandboxWaveHits.Add(p);
                p.transform.position = Rotate(point, 14f);
                p.Velocity = Rotate(p.Velocity, 14f) + new Vector2(-point.y, point.x).normalized * .8f;
                SandboxPulseAt(p.transform.position, Alpha(WaveColor, .6f), .26f);
            }
            sandboxWavePreviousRadius = radius;
        }

        private void UpdateSandboxMines(float dt)
        {
            if (sandboxMineTimer <= 0f) return;
            sandboxMineTimer = Mathf.Max(0f, sandboxMineTimer - dt);
            var armed = sandboxMineTimer <= 8f;
            var fade = Mathf.Clamp01(sandboxMineTimer / .5f);
            var previousAngle = Mathf.Atan2(sandboxPreviousPlayer.y, sandboxPreviousPlayer.x);
            for (var i = 0; i < sandboxMines.Length; i++)
            {
                if (!sandboxMines[i]) continue;
                var angle = sandboxMineAngle + i * Mathf.PI * .25f;
                var point = OrbitPoint(angle, 2.36f);
                var sectorWidth = .105f + .02f * (sandboxGrowth - 1f);
                var delta = Mathf.Abs(Mathf.DeltaAngle(playerAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (armed && (delta < sectorWidth || SandboxCrossedAngle(previousAngle, playerAngle, angle)))
                {
                    sandboxMines[i] = false;
                    SandboxPulseAt(point, MineColor, 1.1f);
                    SandboxPulseAt(OrbitPoint(angle), MineColor, .7f);
                    SpawnImpactBurst(point, MineColor, 18, 2.3f, .5f);
                    SandboxPushProjectiles(point, 2f, 2.3f);
                    abilitySandbox.NotifyStatus("МИНЫ // СЕКТОР " + (i + 1) + " ПОДОРВАН", MineColor);
                    continue;
                }
                var color = Alpha(armed ? MineColor : ChargeColor, fade);
                sandboxMechanicsFx.Diamond(point, .16f + Mathf.Sin(sandboxClock * 7f + i) * .02f, sandboxClock * .6f, color);
                sandboxMechanicsFx.Diamond(point, .065f, -sandboxClock, Color.white);
                sandboxMechanicsFx.Arc(point, .27f, -sandboxClock * 2f, 4.7f, Alpha(color, .65f));
                sandboxMechanicsFx.Line(point, OrbitPoint(angle), Alpha(color, .14f), .012f);
                sandboxMechanicsFx.Arc(Vector2.zero, OrbitSettings.Radius, angle - sectorWidth, sectorWidth * 2f, Alpha(color, .65f), .05f);
            }
        }

        private void UpdateSandboxPolarity(float dt)
        {
            if (sandboxPolarityTimer <= 0f) return;
            sandboxPolarityTimer = Mathf.Max(0f, sandboxPolarityTimer - dt);
            var fade = Mathf.Clamp01(sandboxPolarityTimer / .45f);
            var point = (Vector2)player.position;
            sandboxMechanicsFx.Arc(point, .68f, sandboxClock * 1.4f, Mathf.PI, Alpha(ReplayColor, fade), .035f);
            sandboxMechanicsFx.Arc(point, .68f, sandboxClock * 1.4f + Mathf.PI, Mathf.PI, Alpha(ReverseColor, fade), .035f);
            for (var i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                if (!sandboxPolarities.TryGetValue(p, out var charge))
                {
                    charge = new SandboxCharge { Positive = sandboxPolaritySequence++ % 2 == 0, OriginalColor = p.Renderer.color };
                    sandboxPolarities.Add(p, charge);
                }
                var positive = charge.Positive;
                var delta = point - (Vector2)p.transform.position;
                var force = 4f / Mathf.Max(1f, delta.magnitude);
                p.Velocity = Vector2.ClampMagnitude(p.Velocity + delta.normalized * ((positive ? 1f : -1f) * force * dt), 5.5f);
                var color = positive ? ReplayColor : ReverseColor;
                p.Renderer.color = Color.Lerp(p.Renderer.color, color, dt * 14f);
                if (i >= 24) continue;
                var marker = (Vector2)p.transform.position + new Vector2(.12f, .17f);
                sandboxMechanicsFx.Line(marker - Vector2.right * .055f, marker + Vector2.right * .055f, Alpha(color, fade));
                if (positive) sandboxMechanicsFx.Line(marker - Vector2.up * .055f, marker + Vector2.up * .055f, Alpha(color, fade));
                sandboxMechanicsFx.Arc(p.transform.position, .18f, sandboxClock * (positive ? 2f : -2f), 3.2f, Alpha(color, fade * .7f), .017f);
            }
            if (sandboxPolarityTimer <= 0f)
            {
                ClearSandboxPolarities();
                SandboxPulseAt(point, ReplayColor, 1f);
            }
        }

        private void ClearSandboxPolarities()
        {
            foreach (var entry in sandboxPolarities)
                if (entry.Key != null && entry.Key.Renderer != null) entry.Key.Renderer.color = entry.Value.OriginalColor;
            sandboxPolarities.Clear();
            sandboxPolaritySequence = 0;
        }

        private void ForgetSandboxProjectile(Projectile projectile)
        {
            sandboxWaveHits.Remove(projectile);
            if (sandboxPolarities.TryGetValue(projectile, out var charge))
            {
                if (projectile.Renderer != null) projectile.Renderer.color = charge.OriginalColor;
                sandboxPolarities.Remove(projectile);
            }
        }

        private void UpdateSandboxGrowth(float dt)
        {
            var wasGrowing = sandboxGrowthTimer > 0f;
            sandboxGrowthTimer = Mathf.Max(0f, sandboxGrowthTimer - dt);
            var envelope = Mathf.Min(Mathf.Clamp01((5f - sandboxGrowthTimer) / .4f), Mathf.Clamp01(sandboxGrowthTimer / .65f));
            sandboxGrowth = 1f + 1.3f * Mathf.SmoothStep(0f, 1f, envelope);
            player.localScale = sandboxNormalPlayerScale * sandboxGrowth;
            if (sandboxGrowth > 1.01f)
            {
                var radius = .5f * sandboxGrowth;
                sandboxMechanicsFx.Arc(player.position, radius, sandboxClock, 2.4f, ChargeColor, .035f);
                sandboxMechanicsFx.Arc(player.position, radius, sandboxClock + Mathf.PI, 2.4f, ChargeColor, .035f);
                for (var i = 0; i < 4; i++)
                {
                    var a = i * Mathf.PI * .5f + sandboxClock * .3f;
                    sandboxMechanicsFx.Chevron((Vector2)player.position + OrbitPoint(a, radius + .14f), a, .13f, Alpha(ChargeColor, .7f));
                }
                for (var i = 0; i < projectiles.Count; i++)
                {
                    var p = projectiles[i];
                    if (p.FromPlayer) continue;
                    var delta = (Vector2)p.transform.position - (Vector2)player.position;
                    if (delta.sqrMagnitude > radius * radius || Vector2.Dot(p.Velocity, delta) >= 0f) continue;
                    p.Velocity = delta.normalized * Mathf.Max(2.5f, p.Velocity.magnitude);
                    SandboxPulseAt(p.transform.position, ChargeColor, .36f);
                }
            }
            if (wasGrowing && sandboxGrowthTimer <= 0f) SandboxPulseAt(player.position, ChargeColor, .7f);
        }

        private void UpdateSandboxGhost(float dt)
        {
            var enabled = abilitySandbox.HasPassive(AbilitySandboxAbilityId.GhostTrail);
            if (!enabled) { sandboxGhost.Clear(); sandboxGhostWasEnabled = false; return; }
            var point = (Vector2)player.position;
            if (!sandboxGhostWasEnabled) { sandboxGhostLastPoint = point; sandboxGhostClock = 0f; }
            sandboxGhostWasEnabled = true;
            sandboxGhostCooldown = Mathf.Max(0f, sandboxGhostCooldown - dt);
            sandboxMechanicsFx.Arc(point, .50f * sandboxGrowth, sandboxClock * -1.8f, 2f, Alpha(GhostColor, .8f));
            var moved = Vector2.Distance(point, sandboxPreviousPlayer);
            for (var i = sandboxGhost.Count - 1; i >= 0; i--)
            {
                var segment = sandboxGhost[i];
                var age = sandboxClock - segment.Time;
                if (age > 3f) { sandboxGhost.RemoveAt(i); continue; }
                // Young pieces and a stationary ship cannot retrigger their own emission point.
                if (age > .55f && moved > .003f && moved < 1.5f && sandboxGhostCooldown <= 0f &&
                    SandboxSegmentDistance(sandboxPreviousPlayer, point, segment.A, segment.B) < .13f * sandboxGrowth)
                {
                    sandboxGhost.RemoveAt(i); sandboxGhostCooldown = .45f;
                    SandboxPulseAt(point, GhostColor, 1.25f);
                    SpawnImpactBurst(point, GhostColor, 14, 2f, .45f);
                    SandboxPushProjectiles(point, 1.8f, 2.7f);
                    abilitySandbox.NotifyStatus("СЛЕД // ПОВТОРНОЕ ПЕРЕСЕЧЕНИЕ", GhostColor);
                    continue;
                }
                var fade = Mathf.Clamp01((3f - age) / .7f) * Mathf.Clamp01(age / .18f);
                sandboxMechanicsFx.Line(segment.A, segment.B, Alpha(GhostColor, fade * .13f), .15f);
                sandboxMechanicsFx.Line(segment.A, segment.B, Alpha(GhostColor, fade * .68f), .03f);
            }
            sandboxGhostClock += dt;
            if (sandboxGhostClock >= .065f && Vector2.Distance(point, sandboxGhostLastPoint) > .09f)
            {
                if (Vector2.Distance(point, sandboxGhostLastPoint) < 1.5f)
                {
                    if (sandboxGhost.Count >= 48) sandboxGhost.RemoveAt(0);
                    sandboxGhost.Add(new GhostSegment { A = sandboxGhostLastPoint, B = point, Time = sandboxClock });
                }
                sandboxGhostLastPoint = point; sandboxGhostClock = 0f;
            }
        }

        private void SandboxSeedProjectiles(int count, Color color, bool tangential)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = playerAngle + i * Mathf.PI * 2f / count;
                var point = OrbitPoint(angle, 1.65f);
                var direction = tangential ? new Vector2(-point.y, point.x).normalized : point.normalized;
                var before = projectiles.Count;
                ShootStyledHostile(point, direction * .75f, color, DamageElement.Kinetic, .17f, harrierShardProjectileSprite);
                if (projectiles.Count > before) projectiles[projectiles.Count - 1].Life = 6f;
            }
        }

        private void SandboxPushProjectiles(Vector2 center, float radius, float impulse)
        {
            for (var i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                var delta = (Vector2)p.transform.position - center;
                if (delta.sqrMagnitude < radius * radius)
                    p.Velocity += delta.normalized * impulse * (1f - delta.magnitude / radius);
            }
        }

        private void SandboxPulseAt(Vector2 point, Color color, float radius)
        {
            if (sandboxPulses.Count >= 24) sandboxPulses.RemoveAt(0);
            sandboxPulses.Add(new SandboxPulse { Center = point, Start = sandboxClock, Radius = radius, Color = color });
        }

        private void RenderSandboxPulses()
        {
            for (var i = sandboxPulses.Count - 1; i >= 0; i--)
            {
                var pulse = sandboxPulses[i];
                var t = (sandboxClock - pulse.Start) / .6f;
                if (t >= 1f) { sandboxPulses.RemoveAt(i); continue; }
                var radius = Mathf.Lerp(.05f, pulse.Radius, 1f - (1f - t) * (1f - t));
                sandboxMechanicsFx.Ring(pulse.Center, radius, Alpha(pulse.Color, (1f - t) * .7f), .035f * (1f - t) + .008f);
            }
        }

        private static Vector2 OrbitPoint(float angle) => OrbitPoint(angle, OrbitSettings.Radius);
        private static Vector2 OrbitPoint(float angle, float radius)
        { return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; }
        private static Color Alpha(Color color, float alpha) { color.a *= alpha; return color; }

        private static bool SandboxCrossedAngle(float previous, float current, float target)
        {
            var move = Mathf.DeltaAngle(previous * Mathf.Rad2Deg, current * Mathf.Rad2Deg);
            var gap = Mathf.DeltaAngle(previous * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            return Mathf.Abs(move) > .01f && Mathf.Sign(move) == Mathf.Sign(gap) && Mathf.Abs(gap) <= Mathf.Abs(move);
        }

        private static float SandboxSegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var r = b - a; var s = d - c;
            var cross = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(cross) > .000001f)
            {
                var ca = c - a;
                var t = (ca.x * s.y - ca.y * s.x) / cross;
                var u = (ca.x * r.y - ca.y * r.x) / cross;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return 0f;
            }
            return Mathf.Min(Mathf.Min(SandboxPointSegmentDistance(a, c, d), SandboxPointSegmentDistance(b, c, d)),
                Mathf.Min(SandboxPointSegmentDistance(c, a, b), SandboxPointSegmentDistance(d, a, b)));
        }

        private static float SandboxPointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a;
            return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude)));
        }
    }
}
