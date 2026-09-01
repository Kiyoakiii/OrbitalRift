using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OrbitalRift
{
    public enum CoopThreatPattern : byte
    {
        Bolt,
        Cleave,
        RingGate,
        Mines
    }

    public struct CoopPlayerShotState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public DamageElement Element;
        public int Damage;
        public float Life;
    }

    public static class CoopPlayerShotRules
    {
        public const float Lifetime = 2.2f;

        public static float HitRadius(SectorRoomType roomType)
        {
            switch (roomType)
            {
                // Boss sprites are intentionally large, so the full visible
                // silhouette (including the edge armour) is a valid hit.
                case SectorRoomType.Boss: return .76f;
                case SectorRoomType.Elite: return .38f;
                default: return .33f;
            }
        }

        public static CoopPlayerShotState Create(Vector2 origin, Vector2 target, float speed,
            DamageElement element, int damage)
        {
            var direction = target - origin;
            if (direction.sqrMagnitude < .001f) direction = Vector2.up;
            return new CoopPlayerShotState
            {
                Position = origin,
                Velocity = direction.normalized * Mathf.Max(.1f, speed),
                Element = element,
                Damage = Mathf.Max(0, damage),
                Life = Lifetime
            };
        }

        public static bool Step(ref CoopPlayerShotState shot, float deltaTime, Vector2 enemyPosition,
            SectorRoomType roomType)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            var next = shot.Position + shot.Velocity * deltaTime;
            var hit = CoopTetherRules.DistanceToSegment(enemyPosition, shot.Position, next) <= HitRadius(roomType);
            shot.Position = next;
            shot.Life -= deltaTime;
            return hit;
        }
    }

    public static class CoopThreatAttackRules
    {
        public static CoopThreatPattern PatternFor(SectorRoomType roomType, int roomIndex, uint sequence)
        {
            var cycle = Mathf.Abs(roomIndex + (int)sequence - 1);
            switch (roomType)
            {
                case SectorRoomType.Boss: return (CoopThreatPattern)(cycle % 4);
                case SectorRoomType.Elite: return (CoopThreatPattern)(1 + cycle % 3);
                case SectorRoomType.Combat: return cycle % 3 == 0 ? CoopThreatPattern.Mines : CoopThreatPattern.Bolt;
                default: return CoopThreatPattern.Bolt;
            }
        }

        public static float Windup(CoopThreatPattern pattern)
        {
            switch (pattern)
            {
                case CoopThreatPattern.Cleave: return .76f;
                // Three generous gaps and a slower expansion keep the ring a
                // readable repositioning challenge instead of a surprise hit.
                case CoopThreatPattern.RingGate: return 1.65f;
                case CoopThreatPattern.Mines: return 1.15f;
                default: return .52f;
            }
        }

        public static float PatternAngle(float targetAngle, CoopThreatPattern pattern, uint sequence)
        {
            if (pattern != CoopThreatPattern.RingGate) return Mathf.Repeat(targetAngle, 360f);
            return Mathf.Repeat(targetAngle + (sequence % 2 == 0 ? 62f : -62f), 360f);
        }

        public static bool Hits(CoopThreatPattern pattern, float lockedTargetAngle, float patternAngle,
            float currentAngle)
        {
            switch (pattern)
            {
                case CoopThreatPattern.Cleave:
                    return Mathf.Abs(Mathf.DeltaAngle(lockedTargetAngle, currentAngle)) <= 34f;
                case CoopThreatPattern.RingGate:
                    for (var gap = 0; gap < 3; gap++)
                    {
                        var gapAngle = patternAngle + gap * 120f;
                        if (Mathf.Abs(Mathf.DeltaAngle(gapAngle, currentAngle)) <= 20f) return false;
                    }
                    return true;
                case CoopThreatPattern.Mines:
                    return Mathf.Abs(Mathf.DeltaAngle(lockedTargetAngle, currentAngle)) <= 22f;
                default:
                    return Mathf.Abs(Mathf.DeltaAngle(lockedTargetAngle, currentAngle)) <= 15f;
            }
        }

        public static string Label(CoopThreatPattern pattern)
        {
            switch (pattern)
            {
                case CoopThreatPattern.Cleave: return "РАССЕКАЮЩАЯ ВОЛНА // УЙДИ С ЛИНИИ";
                case CoopThreatPattern.RingGate: return "УДАРНОЕ КОЛЬЦО // 3 РАЗРЫВА";
                case CoopThreatPattern.Mines: return "ОРБИТАЛЬНЫЕ МИНЫ // ПОКИНЬ МЕТКУ";
                default: return "ИГОЛЬЧАТЫЙ ЗАЛП // СМЕНИ ПОЗИЦИЮ";
            }
        }
    }

    public static class CoopSimulationRules
    {
        public const float OrbitDegreesPerSecond = 115f;
        public const float ShipCollisionDistance = .68f;
        public const float ShipCollisionBounceDegrees = 132f;
        public const float ShipCollisionCooldown = .85f;

        public static float StepAngle(float angleDegrees, int direction, float deltaTime)
        {
            direction = Mathf.Clamp(direction, -1, 1);
            return Mathf.Repeat(angleDegrees + direction * OrbitDegreesPerSecond * Mathf.Max(0f, deltaTime), 360f);
        }

        public static bool TryBounceShips(ref float hostAngle, ref float guestAngle, float trajectoryTime, out Vector2 impactPosition)
        {
            var hostPosition = CoopTrajectorySettings.Position(hostAngle, trajectoryTime);
            var guestPosition = CoopTrajectorySettings.Position(guestAngle, trajectoryTime);
            impactPosition = (hostPosition + guestPosition) * .5f;
            if ((hostPosition - guestPosition).sqrMagnitude > ShipCollisionDistance * ShipCollisionDistance)
                return false;

            var angleDelta = Mathf.DeltaAngle(hostAngle, guestAngle);
            var direction = Mathf.Abs(angleDelta) < .01f ? 1f : Mathf.Sign(angleDelta);
            hostAngle = Mathf.Repeat(hostAngle - direction * ShipCollisionBounceDegrees, 360f);
            guestAngle = Mathf.Repeat(guestAngle + direction * ShipCollisionBounceDegrees, 360f);
            return true;
        }
    }

    /// <summary>
    /// Deterministic physics for the unstable relay core. The host runs these
    /// rules and snapshots the result; Solo Expedition uses the same methods.
    /// </summary>
    public static class CoopRelayCoreRules
    {
        public const int MaxCharge = 3;
        public const float ShotCaptureRadius = .58f;
        public const float ShotImpulse = 2.15f;
        public const float ShipContactRadius = .72f;
        public const float ShipImpulse = 3.35f;
        public const float EnemyContactRadius = .72f;
        public const float ArenaBoundary = 4.9f;
        public const float ArenaVerticalBoundary = 3.25f;
        public const float MaxSpeed = 7.2f;
        public const float BossReturnSpeed = 6.35f;
        public const float MinimumImpactSpeed = 2.25f;

        public static bool ShouldSpawn(int runSeed, int roomIndex, SectorRoomType roomType)
        {
            if (roomType == SectorRoomType.Start || roomType == SectorRoomType.Shop) return false;
            if (roomType == SectorRoomType.Elite || roomType == SectorRoomType.Boss) return true;
            unchecked
            {
                var hash = runSeed * 486187739 + roomIndex * 16777619 + (int)roomType * 7919;
                return (hash & 3) != 0;
            }
        }

        public static Vector2 SpawnPosition(int runSeed, int roomIndex)
        {
            unchecked
            {
                var hash = runSeed * 1103515245 + roomIndex * 12345;
                var angle = (hash & 1023) / 1023f * Mathf.PI * 2f;
                var radius = .55f + ((hash >> 10) & 255) / 255f * .85f;
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        public static bool TryGetShotImpulse(Vector2 origin, Vector2 target, Vector2 corePosition,
            out Vector2 impulse)
        {
            impulse = Vector2.zero;
            var ray = target - origin;
            var length = ray.magnitude;
            if (length < .001f) return false;
            var direction = ray / length;
            var projection = Vector2.Dot(corePosition - origin, direction);
            if (projection < 0f || projection > length + .8f) return false;
            var closest = origin + direction * projection;
            if ((corePosition - closest).sqrMagnitude > ShotCaptureRadius * ShotCaptureRadius) return false;
            impulse = direction * ShotImpulse;
            return true;
        }

        public static void Step(ref Vector2 position, ref Vector2 velocity, float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            velocity *= Mathf.Exp(-.42f * deltaTime);
            velocity = Vector2.ClampMagnitude(velocity, MaxSpeed);
            position += velocity * deltaTime;
            var ellipseDistance = Mathf.Sqrt(
                position.x * position.x / (ArenaBoundary * ArenaBoundary) +
                position.y * position.y / (ArenaVerticalBoundary * ArenaVerticalBoundary));
            if (ellipseDistance <= 1f) return;
            position /= ellipseDistance;
            var normal = new Vector2(
                position.x / (ArenaBoundary * ArenaBoundary),
                position.y / (ArenaVerticalBoundary * ArenaVerticalBoundary)).normalized;
            if (normal.sqrMagnitude < .001f) normal = Vector2.up;
            velocity = Vector2.Reflect(velocity, normal) * .82f;
        }

        public static int ImpactDamage(int charge, float speed)
        {
            if (charge <= 0 && speed < MinimumImpactSpeed) return 0;
            return Mathf.Clamp(2 + Mathf.Clamp(charge, 0, MaxCharge) * 2 + Mathf.FloorToInt(speed * .35f), 2, 10);
        }
    }

    /// <summary>
    /// Shared deterministic rules for the two-pilot energy tether. It is a
    /// positional tool: slack is safe, tension pulls both pilots, and an
    /// overloaded line discharges into whatever crosses it.
    /// </summary>
    public static class CoopTetherRules
    {
        public const float ActivationDistance = 2.85f;
        public const float SoftLength = 2.15f;
        public const float BreakDistance = 5.35f;
        public const float PullDegreesPerSecond = 38f;
        public const float CutRadius = .42f;
        public const float DamageInterval = .55f;
        public const float CoreChargeInterval = .9f;
        public const float ReconnectCooldown = 2.6f;
        public const float OverloadDuration = .82f;
        public const int OverloadDamage = 6;

        public static float Tension(float distance)
        {
            return Mathf.InverseLerp(SoftLength, BreakDistance, Mathf.Max(0f, distance));
        }

        public static bool CanConnect(float distance, float cooldown)
        {
            return cooldown <= 0f && distance <= ActivationDistance;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < .0001f) return Vector2.Distance(point, start);
            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        public static void PullAngles(ref float hostAngle, ref float guestAngle,
            float trajectoryTime, float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            var hostPosition = CoopTrajectorySettings.Position(hostAngle, trajectoryTime);
            var guestPosition = CoopTrajectorySettings.Position(guestAngle, trajectoryTime);
            var tension = Tension(Vector2.Distance(hostPosition, guestPosition));
            if (tension <= 0f || deltaTime <= 0f) return;
            var step = PullDegreesPerSecond * tension * deltaTime;
            hostAngle = Mathf.Repeat(hostAngle + BestDirection(hostAngle, guestPosition, trajectoryTime) * step, 360f);
            guestAngle = Mathf.Repeat(guestAngle + BestDirection(guestAngle, hostPosition, trajectoryTime) * step, 360f);
        }

        public static int ApplySafeBacklash(int health)
        {
            return health <= 0 ? 0 : Mathf.Max(1, health - 1);
        }

        public static int DirectionToward(float angle, Vector2 target, float trajectoryTime)
        {
            return BestDirection(angle, target, trajectoryTime) >= 0f ? 1 : -1;
        }

        private static float BestDirection(float angle, Vector2 target, float trajectoryTime)
        {
            const float probeDegrees = 1.5f;
            var plus = CoopTrajectorySettings.Position(angle + probeDegrees, trajectoryTime);
            var minus = CoopTrajectorySettings.Position(angle - probeDegrees, trajectoryTime);
            return Vector2.SqrMagnitude(plus - target) <= Vector2.SqrMagnitude(minus - target) ? 1f : -1f;
        }
    }

    /// <summary>
    /// A cooperative shot crossing the other pilot becomes a toy instead of
    /// friendly fire. An energized tether turns the interception into a
    /// stronger elemental ricochet; without it the ally is only spun away.
    /// </summary>
    public static class CoopFriendlyRedirectRules
    {
        public const float CaptureRadius = .62f;
        public const float EnergizedCaptureRadius = 1.80f;
        public const float RedirectCooldown = .95f;
        public const float RedirectDamageMultiplier = 1.65f;
        public const float ComicSpinDegrees = 48f;

        public static bool TryIntercept(Vector2 origin, Vector2 target, Vector2 allyPosition,
            out float pathProgress, float captureRadius = CaptureRadius)
        {
            pathProgress = 0f;
            var segment = target - origin;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < .001f) return false;
            pathProgress = Vector2.Dot(allyPosition - origin, segment) / lengthSquared;
            captureRadius = Mathf.Max(0f, captureRadius);
            var energized = captureRadius > CaptureRadius + .01f;
            if (energized)
            {
                if (pathProgress < -.35f || pathProgress > 1.05f) return false;
            }
            else if (pathProgress <= .08f || pathProgress >= .94f) return false;
            var closest = origin + segment * Mathf.Clamp01(pathProgress);
            return (allyPosition - closest).sqrMagnitude <= captureRadius * captureRadius;
        }

        public static int RedirectDamage(float sourceDamage, float resistance)
        {
            var boosted = Mathf.Max(0f, sourceDamage) * RedirectDamageMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(ElementalCombat.ApplyResistance(boosted, resistance)));
        }

        public static float ApplyComicSpin(float allyAngle, bool clockwise)
        {
            return Mathf.Repeat(allyAngle + (clockwise ? ComicSpinDegrees : -ComicSpinDegrees), 360f);
        }
    }

    /// <summary>
    /// Shared room modifiers. They are derived from the seeded layout on every
    /// device, so the host and guest never need another network message for them.
    /// </summary>
    public static class CoopRoomRules
    {
        /// <summary>
        /// Shared hull reserve for a two-player run. The host owns the value and
        /// broadcasts it with the same snapshot as the room combat state.
        /// </summary>
        public const int TeamMaxHealth = 8;
        public const int SoloExpeditionMaxHealth = 5;
        // New rooms first announce their threat and let it become visible before
        // either side can deal damage. This removes "invisible" room starts.
        public const float RoomEntryGraceDuration = 3.4f;
        public const float RoomClearDelay = 1.8f;
        public const float ThreatShotWindup = .48f;

        public static float ThreatHitArc(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Boss: return 27f;
                case SectorRoomType.Elite: return 21f;
                default: return 16f;
            }
        }

        public static bool ThreatShotHits(float lockedAngle, float currentAngle, SectorRoomType type)
        {
            return Mathf.Abs(Mathf.DeltaAngle(lockedAngle, currentAngle)) <= ThreatHitArc(type);
        }

        public static int EnemyHealth(SectorRoom room)
        {
            var threat = room == null ? 5 : Mathf.Clamp(room.Threat, 1, 20);
            var type = room == null ? SectorRoomType.Combat : room.Type;
            switch (type)
            {
                case SectorRoomType.Event: return Mathf.Max(2, Mathf.RoundToInt((3 + threat) * .65f));
                case SectorRoomType.Shop: return Mathf.Max(1, 2 + Mathf.RoundToInt(threat * .35f));
                case SectorRoomType.Elite: return 5 + Mathf.RoundToInt(threat * 1.35f);
                case SectorRoomType.Boss: return 26 + threat;
                default: return 3 + threat;
            }
        }

        public static float EnemyOrbitSpeed(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Event: return 16f;
                case SectorRoomType.Shop: return 11f;
                case SectorRoomType.Elite: return 38f;
                case SectorRoomType.Boss: return 34f;
                default: return 26f;
            }
        }

        public static float ThreatPulseInterval(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Event: return 3.4f;
                case SectorRoomType.Shop: return 4.2f;
                case SectorRoomType.Elite: return 1.45f;
                case SectorRoomType.Boss: return 1.05f;
                case SectorRoomType.Start: return 3.2f;
                default: return 2.15f;
            }
        }

        public static float ReactionBonusMultiplier(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Event: return 1.5f;
                case SectorRoomType.Shop: return 1.25f;
                default: return 1f;
            }
        }

        public static int ThreatDamage(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Event:
                case SectorRoomType.Shop:
                case SectorRoomType.Start: return 0;
                case SectorRoomType.Elite: return 1;
                case SectorRoomType.Boss: return 2;
                default: return 1;
            }
        }

        public static float TeamDamageCooldown(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Elite: return 5.25f;
                case SectorRoomType.Boss: return 4.75f;
                default: return 6f;
            }
        }

        public static int RewardAmount(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Elite: return 120;
                case SectorRoomType.Event: return 80;
                case SectorRoomType.Shop: return 60;
                case SectorRoomType.Boss: return 520;
                default: return 0;
            }
        }

        public static string ObjectiveLabel(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Start: return "НАСТРОЙКА ОРБИТЫ";
                case SectorRoomType.Combat: return "РАЗРУШЬ УГРОЗУ";
                case SectorRoomType.Elite: return "ПЕРЕЖИВИ ЭЛИТНЫЙ ПУЛЬС";
                case SectorRoomType.Event: return "СОБЕРИ РЕЗОНАНС";
                case SectorRoomType.Shop: return "ПОДГОТОВЬ СНАРЯЖЕНИЕ";
                case SectorRoomType.Boss: return "СЛОМАЙ БРОНЮ БОССА";
                default: return "ВЫПОЛНИ ЦЕЛЬ СЕКТОРА";
            }
        }

        public static string ModifierLabel(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Event: return "ТИХИЙ КОРИДОР // РЕЗОНАНС +50%";
                case SectorRoomType.Shop: return "СНАБЖЕНИЕ // РЕЗОНАНС +25%";
                case SectorRoomType.Elite: return "ЭЛИТНЫЙ КОНТУР // ПУЛЬС УСКОРЕН";
                case SectorRoomType.Boss: return "БОСС-АРЕНА // СОПРОТИВЛЕНИЯ АКТИВНЫ";
                default: return "СТАНДАРТНЫЙ КОНТУР";
            }
        }

        public static string DangerDescription(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Start: return "БЕЗОПАСНО // НАСТРОЙ ОРБИТУ";
                case SectorRoomType.Event: return "БЕЗОПАСНО // СОБИРАЙ РЕЗОНАНС";
                case SectorRoomType.Shop: return "БЕЗОПАСНО // ПЕРЕДЫШКА И НАГРАДА";
                case SectorRoomType.Elite: return "ЭЛИТА // ВОЛНА И КОЛЬЦО // -1 КОРПУС";
                case SectorRoomType.Boss: return "БОСС // 4 ТИПА АТАК // -2 КОРПУСА";
                default: return "УГРОЗА // ЗАЛП ИЛИ МИНЫ // -1 КОРПУС";
            }
        }
    }

    /// <summary>
    /// First authoritative multiplayer slice. Clients send compact input only; the host advances
    /// both ships and broadcasts snapshots. Combat state can be added to the same host-owned tick.
    /// </summary>
    public sealed class CoopSimulationBridge : MonoBehaviour
    {
        private const string InputMessage = "orbital_rift/input/v1";
        private const string SnapshotMessage = "orbital_rift/snapshot/v11";
        private const string StartRunMessage = "orbital_rift/start/v1";
        private const float NetworkInterval = 1f / 20f;
        private const float RemoteInputTimeout = .25f;

        public float HostAngleDegrees { get; private set; } = 210f;
        public float GuestAngleDegrees { get; private set; } = 330f;
        public float TrajectoryTimeSeconds { get; private set; }
        public uint HostShotSequence { get; private set; }
        public uint GuestShotSequence { get; private set; }
        public bool RunStarted { get; private set; }
        public int ActiveRunSeed { get; private set; }
        public int ActiveRoomIndex { get; private set; }
        public float CoopEnemyAngle { get; private set; }
        public float CoopEnemyRadius { get; private set; }
        public int CoopEnemyHealth { get; private set; }
        public int CoopEnemyMaxHealth { get; private set; }
        public byte CoopEnemyKind { get; private set; }
        public uint CoopEnemyDefeatedSequence { get; private set; }
        public ElementalReaction CoopResonance { get; private set; }
        public float CoopResonanceTimer { get; private set; }
        public uint CoopResonanceSequence { get; private set; }
        public DamageElement CoopThreatPulseElement { get; private set; }
        public float CoopThreatPulseTimer { get; private set; }
        public uint CoopThreatPulseSequence { get; private set; }
        public CoopThreatPattern CoopThreatPattern { get; private set; }
        public bool CoopThreatTargetsHost { get; private set; } = true;
        public float CoopThreatPatternAngle { get; private set; }
        public float SnapshotAgeSeconds { get; private set; } = 99f;
        public bool SnapshotHealthy => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) || SnapshotAgeSeconds <= .35f;
        public bool RunCompleted { get; private set; }
        public uint RunCompletionSequence { get; private set; }
        public int CoopTeamHealth { get; private set; } = CoopRoomRules.TeamMaxHealth;
        public int CoopTeamMaxHealth { get; private set; } = CoopRoomRules.TeamMaxHealth;
        public bool RunFailed { get; private set; }
        public uint RunFailureSequence { get; private set; }
        public uint ShipCollisionSequence { get; private set; }
        public Vector2 ShipCollisionPosition { get; private set; }
        public bool RelayCoreActive { get; private set; }
        public Vector2 RelayCorePosition { get; private set; }
        public Vector2 RelayCoreVelocity { get; private set; }
        public byte RelayCoreCharge { get; private set; }
        public DamageElement RelayCoreElement { get; private set; }
        public bool RelayCoreDangerous { get; private set; }
        public uint RelayCoreEventSequence { get; private set; }
        public byte RelayCoreEventKind { get; private set; }
        public Vector2 RelayCoreEventPosition { get; private set; }
        public bool TetherActive { get; private set; }
        public float TetherHeat { get; private set; }
        public float TetherOverloadTimer { get; private set; }
        public uint TetherEventSequence { get; private set; }
        public byte TetherEventKind { get; private set; }
        public Vector2 TetherEventPosition { get; private set; }
        public uint FriendlyRedirectSequence { get; private set; }
        public byte FriendlyRedirectKind { get; private set; }
        public Vector2 FriendlyRedirectPosition { get; private set; }
        public bool FriendlyRedirectFromHost { get; private set; }
        public DamageElement FriendlyRedirectElement { get; private set; }
        public ulong RoundTripTimeMilliseconds { get; private set; }
        public bool IsNetworkReady => registered && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private MultiplayerSessionController sessions;
        private IPlayerCommandSource localInput;
        private NetworkManager registeredManager;
        private bool registered;
        private int remoteDirection;
        private float lastRemoteInputAt;
        private float sendTimer;
        private float targetHostAngle = 210f;
        private float targetGuestAngle = 330f;
        private float targetTrajectoryTime;
        private uint snapshotSequence;
        private float hostFireTimer;
        private float guestFireTimer;
        private float roomAdvanceTimer;
        private float roomEntryGraceTimer;
        private float threatPulseTimer;
        private float threatAttackWindupTimer;
        private float threatTargetHostAngle;
        private float threatTargetGuestAngle;
        private bool threatTargetsHost = true;
        private readonly List<CoopPlayerShotState> activePlayerShots = new List<CoopPlayerShotState>(48);
        private float teamDamageCooldown;
        private float shipCollisionCooldown;
        private float rttRefreshTimer;
        private Vector2 targetRelayCorePosition;
        private float relayCoreContactCooldown;
        private float tetherDamageTimer;
        private float tetherCoreTimer;
        private float tetherReconnectCooldown;
        private float friendlyRedirectCooldown;
        private bool hasLastElement;
        private DamageElement lastElement;
        private float lastElementAge;
        private const float ResonanceWindow = 1.2f;

        private void Awake()
        {
            sessions = GetComponent<MultiplayerSessionController>();
            localInput = new LocalPlayerCommandSource();
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            var shouldRun = sessions != null && sessions.CurrentSession != null && manager != null && manager.IsListening;
            if (!shouldRun)
            {
                UnregisterHandlers();
                ResetLocalRunState();
                return;
            }

            if (RunStarted && sessions.PlayerCount < 2)
            {
                ResetLocalRunState();
            }

            if (!registered || registeredManager != manager) RegisterHandlers(manager);
            var command = localInput.ReadFrame();
            sendTimer -= Time.unscaledDeltaTime;
            UpdateRoundTripTime(manager, Time.unscaledDeltaTime);

            if (manager.IsServer)
            {
                if (Time.unscaledTime - lastRemoteInputAt > RemoteInputTimeout) remoteDirection = 0;
                if (RunStarted && !RunCompleted && !RunFailed)
                {
                    TrajectoryTimeSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);
                    HostAngleDegrees = CoopSimulationRules.StepAngle(HostAngleDegrees, command.OrbitDirection, Time.unscaledDeltaTime);
                    GuestAngleDegrees = CoopSimulationRules.StepAngle(GuestAngleDegrees, remoteDirection, Time.unscaledDeltaTime);
                    UpdateAuthoritativeShipCollision(Time.unscaledDeltaTime);
                    UpdateAuthoritativeTether(Time.unscaledDeltaTime);
                    UpdateAuthoritativeFire(Time.unscaledDeltaTime);
                    UpdateAuthoritativeResonance(Time.unscaledDeltaTime);
                    UpdateAuthoritativeEnemy(Time.unscaledDeltaTime);
                    UpdateAuthoritativePlayerShots(Time.unscaledDeltaTime);
                    UpdateAuthoritativeRelayCore(Time.unscaledDeltaTime);
                    UpdateAuthoritativeThreatPulse(Time.unscaledDeltaTime);
                    AdvanceAuthoritativeRoom(Time.unscaledDeltaTime);
                }
                if (sendTimer <= 0f)
                {
                    sendTimer = NetworkInterval;
                    SendSnapshot(manager);
                }
            }
            else
            {
                SnapshotAgeSeconds += Time.unscaledDeltaTime;
                CoopThreatPulseTimer = Mathf.Max(0f, CoopThreatPulseTimer - Time.unscaledDeltaTime);
                TetherOverloadTimer = Mathf.Max(0f, TetherOverloadTimer - Time.unscaledDeltaTime);
                if (sendTimer <= 0f)
                {
                    sendTimer = NetworkInterval;
                    SendInput(manager, command.OrbitDirection);
                }
                HostAngleDegrees = Mathf.LerpAngle(HostAngleDegrees, targetHostAngle, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                if (RunStarted && !RunCompleted && !RunFailed)
                {
                    // Guest-side prediction: local touch moves immediately instead of waiting
                    // for the command to reach the phone host and return in a snapshot.
                    GuestAngleDegrees = CoopSimulationRules.StepAngle(GuestAngleDegrees, command.OrbitDirection, Time.unscaledDeltaTime);
                    var correctionError = Mathf.Abs(Mathf.DeltaAngle(GuestAngleDegrees, targetGuestAngle));
                    if (command.OrbitDirection == 0 || correctionError > 70f)
                    {
                        var correctionSpeed = correctionError > 70f ? 10f : 5f;
                        GuestAngleDegrees = Mathf.LerpAngle(GuestAngleDegrees, targetGuestAngle,
                            1f - Mathf.Exp(-correctionSpeed * Time.unscaledDeltaTime));
                    }
                    TrajectoryTimeSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);
                    targetTrajectoryTime += Mathf.Max(0f, Time.unscaledDeltaTime);
                }
                else
                {
                    GuestAngleDegrees = Mathf.LerpAngle(GuestAngleDegrees, targetGuestAngle,
                        1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                }
                TrajectoryTimeSeconds = Mathf.Lerp(TrajectoryTimeSeconds, targetTrajectoryTime,
                    1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                if (RelayCoreActive)
                {
                    RelayCorePosition += RelayCoreVelocity * Mathf.Max(0f, Time.unscaledDeltaTime);
                    RelayCorePosition = Vector2.Lerp(RelayCorePosition, targetRelayCorePosition,
                        1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                }
            }
        }

        private void UpdateRoundTripTime(NetworkManager manager, float deltaTime)
        {
            rttRefreshTimer -= Mathf.Max(0f, deltaTime);
            if (rttRefreshTimer > 0f || manager == null || manager.NetworkConfig?.NetworkTransport == null) return;
            rttRefreshTimer = .35f;
            ulong clientId = NetworkManager.ServerClientId;
            if (manager.IsServer)
            {
                clientId = manager.LocalClientId;
                if (manager.ConnectedClientsIds != null)
                    for (var i = 0; i < manager.ConnectedClientsIds.Count; i++)
                        if (manager.ConnectedClientsIds[i] != manager.LocalClientId)
                        {
                            clientId = manager.ConnectedClientsIds[i];
                            break;
                        }
                if (clientId == manager.LocalClientId) { RoundTripTimeMilliseconds = 0; return; }
            }
            RoundTripTimeMilliseconds = manager.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
        }

        private void UpdateAuthoritativeShipCollision(float deltaTime)
        {
            shipCollisionCooldown = Mathf.Max(0f, shipCollisionCooldown - Mathf.Max(0f, deltaTime));
            if (shipCollisionCooldown > 0f) return;
            var hostAngle = HostAngleDegrees;
            var guestAngle = GuestAngleDegrees;
            if (!CoopSimulationRules.TryBounceShips(ref hostAngle, ref guestAngle,
                    TrajectoryTimeSeconds, out var impactPosition)) return;
            HostAngleDegrees = hostAngle;
            GuestAngleDegrees = guestAngle;
            ShipCollisionPosition = impactPosition;
            ShipCollisionSequence++;
            shipCollisionCooldown = CoopSimulationRules.ShipCollisionCooldown;
        }

        private void UpdateAuthoritativeTether(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            tetherReconnectCooldown = Mathf.Max(0f, tetherReconnectCooldown - deltaTime);
            tetherDamageTimer = Mathf.Max(0f, tetherDamageTimer - deltaTime);
            tetherCoreTimer = Mathf.Max(0f, tetherCoreTimer - deltaTime);
            TetherOverloadTimer = Mathf.Max(0f, TetherOverloadTimer - deltaTime);

            var hostPosition = CoopTrajectorySettings.Position(HostAngleDegrees, TrajectoryTimeSeconds);
            var guestPosition = CoopTrajectorySettings.Position(GuestAngleDegrees, TrajectoryTimeSeconds);
            var distance = Vector2.Distance(hostPosition, guestPosition);
            if (!TetherActive)
            {
                TetherHeat = Mathf.MoveTowards(TetherHeat, 0f, deltaTime * .52f);
                if (!CoopTetherRules.CanConnect(distance, tetherReconnectCooldown)) return;
                TetherActive = true;
                TetherHeat = Mathf.Min(TetherHeat, .18f);
                TetherEventKind = 1;
                TetherEventPosition = (hostPosition + guestPosition) * .5f;
                TetherEventSequence++;
            }

            var hostAngle = HostAngleDegrees;
            var guestAngle = GuestAngleDegrees;
            CoopTetherRules.PullAngles(ref hostAngle, ref guestAngle, TrajectoryTimeSeconds, deltaTime);
            HostAngleDegrees = hostAngle;
            GuestAngleDegrees = guestAngle;
            hostPosition = CoopTrajectorySettings.Position(HostAngleDegrees, TrajectoryTimeSeconds);
            guestPosition = CoopTrajectorySettings.Position(GuestAngleDegrees, TrajectoryTimeSeconds);
            distance = Vector2.Distance(hostPosition, guestPosition);
            var tension = CoopTetherRules.Tension(distance);
            if (tension < .14f)
                TetherHeat = Mathf.MoveTowards(TetherHeat, 0f, deltaTime * .20f);
            else
                TetherHeat = Mathf.Clamp01(TetherHeat + deltaTime * (.07f + tension * .62f));

            var enemyRadians = CoopEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * CoopEnemyRadius;
            if (CoopEnemyHealth > 0 && tetherDamageTimer <= 0f &&
                CoopTetherRules.DistanceToSegment(enemyPosition, hostPosition, guestPosition) <= CoopTetherRules.CutRadius)
            {
                CoopEnemyHealth = Mathf.Max(0, CoopEnemyHealth - 1);
                if (CoopEnemyHealth == 0) CoopEnemyDefeatedSequence++;
                TetherEventKind = 2;
                TetherEventPosition = enemyPosition;
                TetherEventSequence++;
                tetherDamageTimer = CoopTetherRules.DamageInterval;
            }

            if (RelayCoreActive && tetherCoreTimer <= 0f &&
                CoopTetherRules.DistanceToSegment(RelayCorePosition, hostPosition, guestPosition) <=
                CoopTetherRules.CutRadius + .14f)
            {
                var coreDirection = (enemyPosition - RelayCorePosition).normalized;
                if (coreDirection.sqrMagnitude < .001f) coreDirection = (guestPosition - hostPosition).normalized;
                RelayCoreVelocity = Vector2.ClampMagnitude(RelayCoreVelocity + coreDirection * 1.45f,
                    CoopRelayCoreRules.MaxSpeed);
                RelayCoreCharge = (byte)Mathf.Min(CoopRelayCoreRules.MaxCharge, RelayCoreCharge + 1);
                RelayCoreElement = DamageElement.Kinetic;
                RelayCoreDangerous = false;
                TetherEventKind = 5;
                TetherEventPosition = RelayCorePosition;
                TetherEventSequence++;
                tetherCoreTimer = CoopTetherRules.CoreChargeInterval;
            }

            if (distance > CoopTetherRules.BreakDistance || TetherHeat >= 1f)
                DischargeAuthoritativeTether(hostPosition, guestPosition, enemyPosition);
        }

        private void DischargeAuthoritativeTether(Vector2 hostPosition, Vector2 guestPosition, Vector2 enemyPosition)
        {
            var hitsEnemy = CoopEnemyHealth > 0 &&
                            CoopTetherRules.DistanceToSegment(enemyPosition, hostPosition, guestPosition) <=
                            CoopTetherRules.CutRadius + .32f;
            if (hitsEnemy)
            {
                CoopEnemyHealth = Mathf.Max(0, CoopEnemyHealth - CoopTetherRules.OverloadDamage);
                if (CoopEnemyHealth == 0) CoopEnemyDefeatedSequence++;
                TetherEventKind = 3;
                TetherEventPosition = enemyPosition;
            }
            else
            {
                CoopTeamHealth = CoopTetherRules.ApplySafeBacklash(CoopTeamHealth);
                TetherEventKind = 4;
                TetherEventPosition = (hostPosition + guestPosition) * .5f;
            }
            TetherActive = false;
            TetherHeat = 1f;
            TetherOverloadTimer = CoopTetherRules.OverloadDuration;
            TetherEventSequence++;
            tetherReconnectCooldown = CoopTetherRules.ReconnectCooldown;
            tetherDamageTimer = CoopTetherRules.DamageInterval;
            tetherCoreTimer = CoopTetherRules.CoreChargeInterval;
        }

        private void RegisterHandlers(NetworkManager manager)
        {
            UnregisterHandlers();
            registeredManager = manager;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(InputMessage, HandleInput);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshot);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(StartRunMessage, HandleStartRun);
            registered = true;
            sendTimer = 0f;
        }

        private void UnregisterHandlers()
        {
            if (!registered || registeredManager == null)
            {
                registered = false;
                registeredManager = null;
                return;
            }
            registeredManager.CustomMessagingManager?.UnregisterNamedMessageHandler(InputMessage);
            registeredManager.CustomMessagingManager?.UnregisterNamedMessageHandler(SnapshotMessage);
            registeredManager.CustomMessagingManager?.UnregisterNamedMessageHandler(StartRunMessage);
            registered = false;
            registeredManager = null;
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        private static void SendInput(NetworkManager manager, int direction)
        {
            using (var writer = new FastBufferWriter(sizeof(sbyte), Allocator.Temp))
            {
                writer.WriteValueSafe((sbyte)Mathf.Clamp(direction, -1, 1));
                manager.CustomMessagingManager.SendNamedMessage(InputMessage, NetworkManager.ServerClientId, writer,
                    NetworkDelivery.UnreliableSequenced);
            }
        }

        public bool HostStartRun()
        {
            var manager = NetworkManager.Singleton;
            if (sessions == null || sessions.CurrentSession == null || sessions.PlayerCount < 2 ||
                manager == null || !manager.IsServer || !manager.IsListening) return false;

            ResetRunCounters(sessions.RunSeed);
            using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(ActiveRunSeed);
                for (var i = 0; i < manager.ConnectedClientsIds.Count; i++)
                {
                    var clientId = manager.ConnectedClientsIds[i];
                    if (clientId != manager.LocalClientId)
                        manager.CustomMessagingManager.SendNamedMessage(StartRunMessage, clientId, writer,
                            NetworkDelivery.ReliableSequenced);
                }
            }
            return true;
        }

        public void ResetLocalRunState()
        {
            RunStarted = false;
            ActiveRunSeed = 0;
            HostShotSequence = 0;
            GuestShotSequence = 0;
            TrajectoryTimeSeconds = targetTrajectoryTime = CoopTrajectorySettings.InitialElapsedSeconds;
            ActiveRoomIndex = 0;
            CoopEnemyAngle = 90f;
            CoopEnemyRadius = CoopTrajectorySettings.ThreatSpawnRadius;
            CoopEnemyHealth = 0;
            CoopEnemyMaxHealth = 0;
            CoopEnemyKind = 0;
            CoopEnemyDefeatedSequence = 0;
            CoopResonance = ElementalReaction.None;
            CoopResonanceTimer = 0f;
            CoopResonanceSequence = 0;
            CoopThreatPulseElement = DamageElement.Kinetic;
            CoopThreatPulseTimer = 0f;
            CoopThreatPulseSequence = 0;
            CoopThreatPattern = CoopThreatPattern.Bolt;
            CoopThreatTargetsHost = true;
            CoopThreatPatternAngle = 0f;
            activePlayerShots.Clear();
            CoopTeamHealth = CoopRoomRules.TeamMaxHealth;
            CoopTeamMaxHealth = CoopRoomRules.TeamMaxHealth;
            RunFailed = false;
            RunFailureSequence = 0;
            ShipCollisionSequence = 0;
            ShipCollisionPosition = Vector2.zero;
            ResetRelayCoreState();
            ResetTetherState();
            ResetFriendlyRedirectState();
            RoundTripTimeMilliseconds = 0;
            SnapshotAgeSeconds = 99f;
            RunCompleted = false;
            RunCompletionSequence = 0;
            hasLastElement = false;
            lastElement = DamageElement.Kinetic;
            lastElementAge = 0f;
            hostFireTimer = 0f;
            guestFireTimer = 0f;
            roomAdvanceTimer = 0f;
            roomEntryGraceTimer = 0f;
            teamDamageCooldown = 0f;
            shipCollisionCooldown = 0f;
            rttRefreshTimer = 0f;
            remoteDirection = 0;
        }

        private void SendSnapshot(NetworkManager manager)
        {
            if (manager.ConnectedClientsIds == null || manager.ConnectedClientsIds.Count < 2) return;
            using (var writer = new FastBufferWriter(320, Allocator.Temp))
            {
                writer.WriteValueSafe(HostAngleDegrees);
                writer.WriteValueSafe(GuestAngleDegrees);
                writer.WriteValueSafe(TrajectoryTimeSeconds);
                writer.WriteValueSafe(HostShotSequence);
                writer.WriteValueSafe(GuestShotSequence);
                writer.WriteValueSafe(++snapshotSequence);
                writer.WriteValueSafe(ActiveRunSeed);
                writer.WriteValueSafe(ActiveRoomIndex);
                writer.WriteValueSafe(CoopEnemyAngle);
                writer.WriteValueSafe(CoopEnemyRadius);
                writer.WriteValueSafe(CoopEnemyHealth);
                writer.WriteValueSafe(CoopEnemyMaxHealth);
                writer.WriteValueSafe(CoopEnemyKind);
                writer.WriteValueSafe(CoopEnemyDefeatedSequence);
                writer.WriteValueSafe((byte)CoopResonance);
                writer.WriteValueSafe(CoopResonanceTimer);
                writer.WriteValueSafe(CoopResonanceSequence);
                writer.WriteValueSafe((byte)CoopThreatPulseElement);
                writer.WriteValueSafe(CoopThreatPulseTimer);
                writer.WriteValueSafe(CoopThreatPulseSequence);
                writer.WriteValueSafe((byte)CoopThreatPattern);
                writer.WriteValueSafe((byte)(CoopThreatTargetsHost ? 1 : 0));
                writer.WriteValueSafe(CoopThreatPatternAngle);
                writer.WriteValueSafe((byte)(RunCompleted ? 1 : 0));
                writer.WriteValueSafe(RunCompletionSequence);
                writer.WriteValueSafe((byte)(RunStarted ? 1 : 0));
                writer.WriteValueSafe(CoopTeamHealth);
                writer.WriteValueSafe(CoopTeamMaxHealth);
                writer.WriteValueSafe((byte)(RunFailed ? 1 : 0));
                writer.WriteValueSafe(RunFailureSequence);
                writer.WriteValueSafe(ShipCollisionSequence);
                writer.WriteValueSafe(ShipCollisionPosition.x);
                writer.WriteValueSafe(ShipCollisionPosition.y);
                writer.WriteValueSafe((byte)(RelayCoreActive ? 1 : 0));
                writer.WriteValueSafe(RelayCorePosition.x);
                writer.WriteValueSafe(RelayCorePosition.y);
                writer.WriteValueSafe(RelayCoreVelocity.x);
                writer.WriteValueSafe(RelayCoreVelocity.y);
                writer.WriteValueSafe(RelayCoreCharge);
                writer.WriteValueSafe((byte)RelayCoreElement);
                writer.WriteValueSafe((byte)(RelayCoreDangerous ? 1 : 0));
                writer.WriteValueSafe(RelayCoreEventSequence);
                writer.WriteValueSafe(RelayCoreEventKind);
                writer.WriteValueSafe(RelayCoreEventPosition.x);
                writer.WriteValueSafe(RelayCoreEventPosition.y);
                writer.WriteValueSafe((byte)(TetherActive ? 1 : 0));
                writer.WriteValueSafe(TetherHeat);
                writer.WriteValueSafe(TetherOverloadTimer);
                writer.WriteValueSafe(TetherEventSequence);
                writer.WriteValueSafe(TetherEventKind);
                writer.WriteValueSafe(TetherEventPosition.x);
                writer.WriteValueSafe(TetherEventPosition.y);
                writer.WriteValueSafe(FriendlyRedirectSequence);
                writer.WriteValueSafe(FriendlyRedirectKind);
                writer.WriteValueSafe(FriendlyRedirectPosition.x);
                writer.WriteValueSafe(FriendlyRedirectPosition.y);
                writer.WriteValueSafe((byte)(FriendlyRedirectFromHost ? 1 : 0));
                writer.WriteValueSafe((byte)FriendlyRedirectElement);
                for (var i = 0; i < manager.ConnectedClientsIds.Count; i++)
                {
                    var clientId = manager.ConnectedClientsIds[i];
                    if (clientId != manager.LocalClientId)
                        manager.CustomMessagingManager.SendNamedMessage(SnapshotMessage, clientId, writer,
                            NetworkDelivery.UnreliableSequenced);
                }
            }
        }

        private void HandleInput(ulong senderClientId, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || senderClientId == manager.LocalClientId) return;
            reader.ReadValueSafe(out sbyte receivedDirection);
            remoteDirection = Mathf.Clamp(receivedDirection, -1, 1);
            lastRemoteInputAt = Time.unscaledTime;
        }

        private void HandleSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId) return;
            var snapshotWasStale = SnapshotAgeSeconds > 1f;
            reader.ReadValueSafe(out targetHostAngle);
            reader.ReadValueSafe(out targetGuestAngle);
            reader.ReadValueSafe(out float receivedTrajectoryTime);
            reader.ReadValueSafe(out uint hostShots);
            reader.ReadValueSafe(out uint guestShots);
            reader.ReadValueSafe(out snapshotSequence);
            reader.ReadValueSafe(out int runSeed);
            reader.ReadValueSafe(out int roomIndex);
            reader.ReadValueSafe(out float enemyAngle);
            reader.ReadValueSafe(out float enemyRadius);
            reader.ReadValueSafe(out int enemyHealth);
            reader.ReadValueSafe(out int enemyMaxHealth);
            reader.ReadValueSafe(out byte enemyKind);
            reader.ReadValueSafe(out uint enemyDefeatedSequence);
            reader.ReadValueSafe(out byte resonance);
            reader.ReadValueSafe(out float resonanceTimer);
            reader.ReadValueSafe(out uint resonanceSequence);
            reader.ReadValueSafe(out byte threatPulseElement);
            reader.ReadValueSafe(out float threatPulseTimerValue);
            reader.ReadValueSafe(out uint threatPulseSequence);
            reader.ReadValueSafe(out byte threatPattern);
            reader.ReadValueSafe(out byte threatTargetsHost);
            reader.ReadValueSafe(out float threatPatternAngle);
            reader.ReadValueSafe(out byte runCompleted);
            reader.ReadValueSafe(out uint runCompletionSequence);
            reader.ReadValueSafe(out byte runStarted);
            reader.ReadValueSafe(out int teamHealth);
            reader.ReadValueSafe(out int teamMaxHealth);
            reader.ReadValueSafe(out byte runFailed);
            reader.ReadValueSafe(out uint runFailureSequence);
            reader.ReadValueSafe(out uint collisionSequence);
            reader.ReadValueSafe(out float collisionX);
            reader.ReadValueSafe(out float collisionY);
            reader.ReadValueSafe(out byte relayActive);
            reader.ReadValueSafe(out float relayX);
            reader.ReadValueSafe(out float relayY);
            reader.ReadValueSafe(out float relayVelocityX);
            reader.ReadValueSafe(out float relayVelocityY);
            reader.ReadValueSafe(out byte relayCharge);
            reader.ReadValueSafe(out byte relayElement);
            reader.ReadValueSafe(out byte relayDangerous);
            reader.ReadValueSafe(out uint relayEventSequence);
            reader.ReadValueSafe(out byte relayEventKind);
            reader.ReadValueSafe(out float relayEventX);
            reader.ReadValueSafe(out float relayEventY);
            reader.ReadValueSafe(out byte tetherActive);
            reader.ReadValueSafe(out float tetherHeat);
            reader.ReadValueSafe(out float tetherOverloadTimer);
            reader.ReadValueSafe(out uint tetherEventSequence);
            reader.ReadValueSafe(out byte tetherEventKind);
            reader.ReadValueSafe(out float tetherEventX);
            reader.ReadValueSafe(out float tetherEventY);
            reader.ReadValueSafe(out uint friendlyRedirectSequence);
            reader.ReadValueSafe(out byte friendlyRedirectKind);
            reader.ReadValueSafe(out float friendlyRedirectX);
            reader.ReadValueSafe(out float friendlyRedirectY);
            reader.ReadValueSafe(out byte friendlyRedirectFromHost);
            reader.ReadValueSafe(out byte friendlyRedirectElement);
            if (runStarted != 0 && !RunStarted) ResetRunCounters(runSeed);
            targetTrajectoryTime = Mathf.Max(0f, receivedTrajectoryTime);
            if (snapshotWasStale || Mathf.Abs(TrajectoryTimeSeconds - targetTrajectoryTime) > 1f)
                TrajectoryTimeSeconds = targetTrajectoryTime;
            HostShotSequence = hostShots;
            GuestShotSequence = guestShots;
            ActiveRoomIndex = Mathf.Max(0, roomIndex);
            CoopEnemyAngle = Mathf.Repeat(enemyAngle, 360f);
            CoopEnemyRadius = Mathf.Max(.2f, enemyRadius);
            CoopEnemyHealth = Mathf.Max(0, enemyHealth);
            CoopEnemyMaxHealth = Mathf.Max(0, enemyMaxHealth);
            CoopEnemyKind = enemyKind;
            CoopEnemyDefeatedSequence = enemyDefeatedSequence;
            CoopResonance = (ElementalReaction)Mathf.Clamp(resonance, 0, (int)ElementalReaction.Overcharge);
            CoopResonanceTimer = Mathf.Clamp(resonanceTimer, 0f, 2f);
            CoopResonanceSequence = resonanceSequence;
            CoopThreatPulseElement = (DamageElement)Mathf.Clamp(threatPulseElement, 0, (int)DamageElement.Poison);
            CoopThreatPulseTimer = Mathf.Clamp(threatPulseTimerValue, 0f, 1f);
            CoopThreatPulseSequence = threatPulseSequence;
            CoopThreatPattern = (CoopThreatPattern)Mathf.Clamp(threatPattern, 0, (int)CoopThreatPattern.Mines);
            CoopThreatTargetsHost = threatTargetsHost != 0;
            CoopThreatPatternAngle = Mathf.Repeat(threatPatternAngle, 360f);
            RunCompleted = runCompleted != 0;
            RunCompletionSequence = runCompletionSequence;
            CoopTeamHealth = Mathf.Clamp(teamHealth, 0, CoopRoomRules.TeamMaxHealth);
            CoopTeamMaxHealth = Mathf.Clamp(teamMaxHealth, 1, CoopRoomRules.TeamMaxHealth);
            RunFailed = runFailed != 0;
            RunFailureSequence = runFailureSequence;
            var collisionChanged = collisionSequence != ShipCollisionSequence;
            ShipCollisionSequence = collisionSequence;
            ShipCollisionPosition = new Vector2(collisionX, collisionY);
            RelayCoreActive = relayActive != 0;
            targetRelayCorePosition = new Vector2(relayX, relayY);
            if (snapshotWasStale || !RelayCoreActive) RelayCorePosition = targetRelayCorePosition;
            RelayCoreVelocity = Vector2.ClampMagnitude(new Vector2(relayVelocityX, relayVelocityY), CoopRelayCoreRules.MaxSpeed);
            RelayCoreCharge = (byte)Mathf.Clamp(relayCharge, 0, CoopRelayCoreRules.MaxCharge);
            RelayCoreElement = (DamageElement)Mathf.Clamp(relayElement, 0, (int)DamageElement.Poison);
            RelayCoreDangerous = relayDangerous != 0;
            RelayCoreEventSequence = relayEventSequence;
            RelayCoreEventKind = relayEventKind;
            RelayCoreEventPosition = new Vector2(relayEventX, relayEventY);
            TetherActive = tetherActive != 0;
            TetherHeat = Mathf.Clamp01(tetherHeat);
            TetherOverloadTimer = Mathf.Clamp(tetherOverloadTimer, 0f, CoopTetherRules.OverloadDuration);
            TetherEventSequence = tetherEventSequence;
            TetherEventKind = tetherEventKind;
            TetherEventPosition = new Vector2(tetherEventX, tetherEventY);
            var friendlyRedirectChanged = friendlyRedirectSequence != FriendlyRedirectSequence;
            FriendlyRedirectSequence = friendlyRedirectSequence;
            FriendlyRedirectKind = friendlyRedirectKind;
            FriendlyRedirectPosition = new Vector2(friendlyRedirectX, friendlyRedirectY);
            FriendlyRedirectFromHost = friendlyRedirectFromHost != 0;
            FriendlyRedirectElement = (DamageElement)Mathf.Clamp(friendlyRedirectElement, 0, (int)DamageElement.Poison);
            if (collisionChanged || friendlyRedirectChanged && FriendlyRedirectKind == 2)
            {
                // A bounce is a discrete host event, so prediction must accept it immediately.
                HostAngleDegrees = targetHostAngle;
                GuestAngleDegrees = targetGuestAngle;
            }
            SnapshotAgeSeconds = 0f;
        }

        private void HandleStartRun(ulong senderClientId, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out int runSeed);
            ResetRunCounters(runSeed);
        }

        private void ResetRunCounters(int runSeed)
        {
            ActiveRunSeed = runSeed;
            var trajectoryEntryAngle = CoopTrajectorySettings.InitialAngleOffsetForRun(runSeed);
            HostAngleDegrees = targetHostAngle = Mathf.Repeat(210f + trajectoryEntryAngle, 360f);
            GuestAngleDegrees = targetGuestAngle = Mathf.Repeat(330f + trajectoryEntryAngle, 360f);
            TrajectoryTimeSeconds = targetTrajectoryTime = CoopTrajectorySettings.InitialElapsedForRun(runSeed);
            HostShotSequence = 0;
            GuestShotSequence = 0;
            ActiveRoomIndex = 0;
            CoopEnemyDefeatedSequence = 0;
            CoopResonance = ElementalReaction.None;
            CoopResonanceTimer = 0f;
            CoopResonanceSequence = 0;
            CoopThreatPulseElement = DamageElement.Kinetic;
            CoopThreatPulseTimer = 0f;
            CoopThreatPulseSequence = 0;
            CoopThreatPattern = CoopThreatPattern.Bolt;
            CoopThreatTargetsHost = true;
            CoopThreatPatternAngle = 0f;
            activePlayerShots.Clear();
            CoopTeamHealth = CoopRoomRules.TeamMaxHealth;
            CoopTeamMaxHealth = CoopRoomRules.TeamMaxHealth;
            RunFailed = false;
            RunFailureSequence = 0;
            ShipCollisionSequence = 0;
            ShipCollisionPosition = Vector2.zero;
            ResetRelayCoreState();
            ResetTetherState();
            ResetFriendlyRedirectState();
            threatPulseTimer = 0f;
            threatAttackWindupTimer = 0f;
            teamDamageCooldown = 0f;
            shipCollisionCooldown = 0f;
            SnapshotAgeSeconds = 99f;
            RunCompleted = false;
            RunCompletionSequence = 0;
            hasLastElement = false;
            lastElement = DamageElement.Kinetic;
            lastElementAge = 0f;
            hostFireTimer = .25f;
            guestFireTimer = .48f;
            roomAdvanceTimer = 0f;
            snapshotSequence = 0;
            ResetAuthoritativeEnemy();
            RunStarted = true;
        }

        private void AdvanceAuthoritativeRoom(float deltaTime)
        {
            if (RunCompleted || RunFailed || sessions == null || sessions.CurrentSector == null) return;
            if (CoopEnemyHealth > 0)
            {
                roomAdvanceTimer = 0f;
                return;
            }
            roomAdvanceTimer += Mathf.Max(0f, deltaTime);
            if (roomAdvanceTimer < CoopRoomRules.RoomClearDelay) return;
            roomAdvanceTimer = 0f;
            if (ActiveRoomIndex >= sessions.CurrentSector.Rooms.Count - 1)
            {
                RunCompleted = true;
                RunCompletionSequence++;
                return;
            }
            ActiveRoomIndex = Mathf.Min(ActiveRoomIndex + 1, sessions.CurrentSector.Rooms.Count - 1);
            ResetAuthoritativeEnemy();
        }

        private void ResetAuthoritativeEnemy()
        {
            var room = sessions?.CurrentSector != null && ActiveRoomIndex >= 0 && ActiveRoomIndex < sessions.CurrentSector.Rooms.Count
                ? sessions.CurrentSector.Rooms[ActiveRoomIndex] : null;
            var threat = room == null ? 5 : Mathf.Clamp(room.Threat, 1, 20);
            CoopEnemyKind = room == null ? (byte)1 : (byte)room.Type;
            CoopEnemyMaxHealth = CoopRoomRules.EnemyHealth(room);
            CoopEnemyHealth = CoopEnemyMaxHealth;
            CoopEnemyAngle = Mathf.Repeat(91f + ActiveRoomIndex * 47f, 360f);
            CoopEnemyRadius = CoopTrajectorySettings.ThreatSpawnRadius;
            CoopResonance = ElementalReaction.None;
            CoopResonanceTimer = 0f;
            hasLastElement = false;
            lastElementAge = 0f;
            CoopThreatPulseElement = DamageElement.Kinetic;
            CoopThreatPulseTimer = 0f;
            threatPulseTimer = 0f;
            threatAttackWindupTimer = 0f;
            CoopThreatPattern = CoopThreatPattern.Bolt;
            CoopThreatTargetsHost = true;
            CoopThreatPatternAngle = 0f;
            activePlayerShots.Clear();
            roomEntryGraceTimer = CoopRoomRules.RoomEntryGraceDuration;
            ResetAuthoritativeRelayCore(room);
        }

        private void ResetRelayCoreState()
        {
            RelayCoreActive = false;
            RelayCorePosition = targetRelayCorePosition = Vector2.zero;
            RelayCoreVelocity = Vector2.zero;
            RelayCoreCharge = 0;
            RelayCoreElement = DamageElement.Kinetic;
            RelayCoreDangerous = false;
            RelayCoreEventSequence = 0;
            RelayCoreEventKind = 0;
            RelayCoreEventPosition = Vector2.zero;
            relayCoreContactCooldown = 0f;
        }

        private void ResetTetherState()
        {
            TetherActive = false;
            TetherHeat = 0f;
            TetherOverloadTimer = 0f;
            TetherEventSequence = 0;
            TetherEventKind = 0;
            TetherEventPosition = Vector2.zero;
            tetherDamageTimer = 0f;
            tetherCoreTimer = 0f;
            tetherReconnectCooldown = .8f;
        }

        private void ResetFriendlyRedirectState()
        {
            FriendlyRedirectSequence = 0;
            FriendlyRedirectKind = 0;
            FriendlyRedirectPosition = Vector2.zero;
            FriendlyRedirectFromHost = false;
            FriendlyRedirectElement = DamageElement.Kinetic;
            friendlyRedirectCooldown = 0f;
        }

        private void ResetAuthoritativeRelayCore(SectorRoom room)
        {
            var roomType = room == null ? SectorRoomType.Combat : room.Type;
            RelayCoreActive = CoopRelayCoreRules.ShouldSpawn(ActiveRunSeed, ActiveRoomIndex, roomType);
            RelayCorePosition = targetRelayCorePosition = CoopRelayCoreRules.SpawnPosition(ActiveRunSeed, ActiveRoomIndex);
            RelayCoreVelocity = Vector2.zero;
            RelayCoreCharge = 0;
            RelayCoreElement = DamageElement.Kinetic;
            RelayCoreDangerous = false;
            RelayCoreEventKind = 0;
            RelayCoreEventPosition = RelayCorePosition;
            relayCoreContactCooldown = .35f;
        }

        private void UpdateAuthoritativeEnemy(float deltaTime)
        {
            roomEntryGraceTimer = Mathf.Max(0f, roomEntryGraceTimer - Mathf.Max(0f, deltaTime));
            if (CoopEnemyHealth <= 0) return;
            var roomType = (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss);
            CoopEnemyAngle = Mathf.Repeat(CoopEnemyAngle + Mathf.Max(0f, deltaTime) * CoopRoomRules.EnemyOrbitSpeed(roomType), 360f);
            CoopEnemyRadius = Mathf.MoveTowards(CoopEnemyRadius, CoopTrajectorySettings.ThreatOrbitRadius,
                Mathf.Max(0f, deltaTime) * .44f);
        }

        private void UpdateAuthoritativeResonance(float deltaTime)
        {
            lastElementAge += Mathf.Max(0f, deltaTime);
            if (lastElementAge > ResonanceWindow) hasLastElement = false;
            CoopResonanceTimer = Mathf.Max(0f, CoopResonanceTimer - Mathf.Max(0f, deltaTime));
        }

        private void UpdateAuthoritativeRelayCore(float deltaTime)
        {
            if (!RelayCoreActive) return;
            relayCoreContactCooldown = Mathf.Max(0f, relayCoreContactCooldown - Mathf.Max(0f, deltaTime));
            var relayPosition = RelayCorePosition;
            var relayVelocity = RelayCoreVelocity;
            CoopRelayCoreRules.Step(ref relayPosition, ref relayVelocity, deltaTime);
            RelayCorePosition = relayPosition;
            RelayCoreVelocity = relayVelocity;

            var hostPosition = CoopTrajectorySettings.Position(HostAngleDegrees, TrajectoryTimeSeconds);
            var guestPosition = CoopTrajectorySettings.Position(GuestAngleDegrees, TrajectoryTimeSeconds);
            if (relayCoreContactCooldown <= 0f &&
                (TryHandleRelayCoreShipContact(hostPosition) || TryHandleRelayCoreShipContact(guestPosition)))
                relayCoreContactCooldown = .28f;

            if (relayCoreContactCooldown > 0f || CoopEnemyHealth <= 0) return;
            var enemyRadians = CoopEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * CoopEnemyRadius;
            if ((RelayCorePosition - enemyPosition).sqrMagnitude >
                CoopRelayCoreRules.EnemyContactRadius * CoopRelayCoreRules.EnemyContactRadius) return;

            var speed = RelayCoreVelocity.magnitude;
            var damage = CoopRelayCoreRules.ImpactDamage(RelayCoreCharge, speed);
            if (damage <= 0) return;
            CoopEnemyHealth = Mathf.Max(0, CoopEnemyHealth - damage);
            RelayCoreEventPosition = RelayCorePosition;
            var roomType = (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss);
            if (roomType == SectorRoomType.Boss && CoopEnemyHealth > 0)
            {
                var target = Vector2.SqrMagnitude(hostPosition - RelayCorePosition) <=
                             Vector2.SqrMagnitude(guestPosition - RelayCorePosition) ? hostPosition : guestPosition;
                var returnDirection = (target - RelayCorePosition).normalized;
                if (returnDirection.sqrMagnitude < .001f) returnDirection = Vector2.down;
                RelayCoreVelocity = returnDirection * CoopRelayCoreRules.BossReturnSpeed;
                RelayCoreDangerous = true;
                RelayCoreEventKind = 3;
            }
            else
            {
                var bounceDirection = (RelayCorePosition - enemyPosition).normalized;
                if (bounceDirection.sqrMagnitude < .001f) bounceDirection = Vector2.up;
                RelayCoreVelocity = bounceDirection * Mathf.Max(3.8f, speed * .82f);
                RelayCoreDangerous = false;
                RelayCoreEventKind = 2;
            }
            RelayCoreCharge = 0;
            RelayCoreEventSequence++;
            relayCoreContactCooldown = .42f;
            if (CoopEnemyHealth == 0) CoopEnemyDefeatedSequence++;
        }

        private bool TryHandleRelayCoreShipContact(Vector2 shipPosition)
        {
            var offset = RelayCorePosition - shipPosition;
            if (offset.sqrMagnitude > CoopRelayCoreRules.ShipContactRadius * CoopRelayCoreRules.ShipContactRadius)
                return false;
            var direction = offset.sqrMagnitude > .001f ? offset.normalized : Vector2.up;
            if (RelayCoreDangerous)
            {
                CoopTeamHealth = Mathf.Max(0, CoopTeamHealth - 1);
                RelayCoreDangerous = false;
                RelayCoreCharge = 0;
                RelayCoreEventKind = 4;
                RelayCoreEventPosition = shipPosition;
                RelayCoreEventSequence++;
                if (CoopTeamHealth == 0)
                {
                    RunFailed = true;
                    RunFailureSequence++;
                }
            }
            RelayCoreVelocity = Vector2.ClampMagnitude(
                RelayCoreVelocity * .35f + direction * CoopRelayCoreRules.ShipImpulse,
                CoopRelayCoreRules.MaxSpeed);
            return true;
        }

        private void PushRelayCoreByShot(ShipArchetype ship, float shipAngle)
        {
            if (!RelayCoreActive || CoopEnemyHealth <= 0) return;
            var origin = CoopTrajectorySettings.Position(shipAngle, TrajectoryTimeSeconds);
            var enemyRadians = CoopEnemyAngle * Mathf.Deg2Rad;
            var target = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * CoopEnemyRadius;
            if (!CoopRelayCoreRules.TryGetShotImpulse(origin, target, RelayCorePosition, out var impulse)) return;
            RelayCoreVelocity = Vector2.ClampMagnitude(RelayCoreVelocity + impulse, CoopRelayCoreRules.MaxSpeed);
            RelayCoreCharge = (byte)Mathf.Min(CoopRelayCoreRules.MaxCharge, RelayCoreCharge + 1);
            RelayCoreElement = ShipLoadoutSettings.Get(ship).Element;
            RelayCoreDangerous = false;
        }

        private void UpdateAuthoritativeThreatPulse(float deltaTime)
        {
            if (CoopEnemyHealth <= 0) return;
            if (roomEntryGraceTimer > 0f) return;
            teamDamageCooldown = Mathf.Max(0f, teamDamageCooldown - Mathf.Max(0f, deltaTime));
            if (threatAttackWindupTimer > 0f)
            {
                threatAttackWindupTimer -= Mathf.Max(0f, deltaTime);
                CoopThreatPulseTimer = Mathf.Max(0f, threatAttackWindupTimer);
                if (threatAttackWindupTimer > 0f) return;

                var pendingRoomType = (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss);
                var lockedTargetAngle = threatTargetsHost ? threatTargetHostAngle : threatTargetGuestAngle;
                var currentTargetAngle = threatTargetsHost ? HostAngleDegrees : GuestAngleDegrees;
                var hitTarget = CoopThreatAttackRules.Hits(CoopThreatPattern, lockedTargetAngle,
                    CoopThreatPatternAngle, currentTargetAngle);
                var pendingDamage = CoopRoomRules.ThreatDamage(pendingRoomType);
                if (pendingDamage > 0 && teamDamageCooldown <= 0f && hitTarget)
                {
                    CoopTeamHealth = Mathf.Max(0, CoopTeamHealth - pendingDamage);
                    teamDamageCooldown = CoopRoomRules.TeamDamageCooldown(pendingRoomType);
                    if (CoopTeamHealth == 0)
                    {
                        RunFailed = true;
                        RunFailureSequence++;
                    }
                }
                return;
            }
            threatPulseTimer -= Mathf.Max(0f, deltaTime);
            CoopThreatPulseTimer = Mathf.Max(0f, CoopThreatPulseTimer - Mathf.Max(0f, deltaTime));
            if (threatPulseTimer > 0f) return;

            var roomType = (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss);
            var interval = CoopRoomRules.ThreatPulseInterval(roomType);
            threatPulseTimer = interval;
            var nextSequence = CoopThreatPulseSequence + 1u;
            CoopThreatPattern = CoopThreatAttackRules.PatternFor(roomType, ActiveRoomIndex, nextSequence);
            threatTargetsHost = (ActiveRoomIndex + (int)nextSequence) % 2 == 0;
            CoopThreatTargetsHost = threatTargetsHost;
            threatTargetHostAngle = HostAngleDegrees;
            threatTargetGuestAngle = GuestAngleDegrees;
            var selectedAngle = threatTargetsHost ? threatTargetHostAngle : threatTargetGuestAngle;
            CoopThreatPatternAngle = CoopThreatAttackRules.PatternAngle(selectedAngle, CoopThreatPattern, nextSequence);
            threatAttackWindupTimer = CoopThreatAttackRules.Windup(CoopThreatPattern);
            CoopThreatPulseTimer = threatAttackWindupTimer;
            CoopThreatPulseSequence = nextSequence;
            CoopThreatPulseElement = roomType == SectorRoomType.Boss
                ? (DamageElement)(ActiveRoomIndex % 3 + 1)
                : (DamageElement)(ActiveRoomIndex % 4);

        }

        private void UpdateAuthoritativeFire(float deltaTime)
        {
            friendlyRedirectCooldown = Mathf.Max(0f, friendlyRedirectCooldown - Mathf.Max(0f, deltaTime));
            var hostLoadout = ShipLoadoutSettings.Get(sessions.HostShip);
            var guestLoadout = ShipLoadoutSettings.Get(sessions.GuestShip);
            hostFireTimer -= deltaTime;
            guestFireTimer -= deltaTime;
            if (hostFireTimer <= 0f)
            {
                HostShotSequence++;
                ApplyAuthoritativeShot(sessions.HostShip, HostAngleDegrees, true);
                hostFireTimer += BalanceSettings.PlayerFireInterval(1, false) * hostLoadout.FireIntervalMultiplier;
            }
            if (guestFireTimer <= 0f)
            {
                GuestShotSequence++;
                ApplyAuthoritativeShot(sessions.GuestShip, GuestAngleDegrees, false);
                guestFireTimer += BalanceSettings.PlayerFireInterval(1, false) * guestLoadout.FireIntervalMultiplier;
            }
        }

        private void ApplyAuthoritativeShot(ShipArchetype ship, float shipAngle, bool fromHost)
        {
            if (CoopEnemyHealth <= 0 || roomEntryGraceTimer > 0f) return;
            var loadout = ShipLoadoutSettings.Get(ship);
            var origin = CoopTrajectorySettings.Position(shipAngle, TrajectoryTimeSeconds);
            var allyAngle = fromHost ? GuestAngleDegrees : HostAngleDegrees;
            var allyPosition = CoopTrajectorySettings.Position(allyAngle, TrajectoryTimeSeconds);
            var enemyRadians = CoopEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * CoopEnemyRadius;
            var redirectRadius = TetherActive ? CoopFriendlyRedirectRules.EnergizedCaptureRadius :
                CoopFriendlyRedirectRules.CaptureRadius;
            if (friendlyRedirectCooldown <= 0f &&
                CoopFriendlyRedirectRules.TryIntercept(origin, enemyPosition, allyPosition, out _, redirectRadius))
            {
                friendlyRedirectCooldown = CoopFriendlyRedirectRules.RedirectCooldown;
                FriendlyRedirectFromHost = fromHost;
                FriendlyRedirectPosition = allyPosition;
                FriendlyRedirectSequence++;
                if (!TetherActive)
                {
                    FriendlyRedirectKind = 2;
                    FriendlyRedirectElement = loadout.Element;
                    if (fromHost)
                        GuestAngleDegrees = CoopFriendlyRedirectRules.ApplyComicSpin(GuestAngleDegrees, true);
                    else
                        HostAngleDegrees = CoopFriendlyRedirectRules.ApplyComicSpin(HostAngleDegrees, false);
                    targetHostAngle = HostAngleDegrees;
                    targetGuestAngle = GuestAngleDegrees;
                    return;
                }

                var allyShip = fromHost ? sessions.GuestShip : sessions.HostShip;
                var allyLoadout = ShipLoadoutSettings.Get(allyShip);
                FriendlyRedirectKind = 1;
                FriendlyRedirectElement = allyLoadout.Element;
                var redirectResistance = CoopEnemyKind == (byte)SectorRoomType.Boss
                    ? BossSettings.Resistance(allyLoadout.Element) : 1f;
                ApplyAuthoritativeDamage(allyLoadout.Element,
                    CoopFriendlyRedirectRules.RedirectDamage(loadout.DamageMultiplier, redirectResistance));
                return;
            }

            PushRelayCoreByShot(ship, shipAngle);
            var resistance = CoopEnemyKind == (byte)SectorRoomType.Boss ? BossSettings.Resistance(loadout.Element) : 1f;
            var damage = Mathf.Max(1, Mathf.RoundToInt(ElementalCombat.ApplyResistance(loadout.DamageMultiplier, resistance)));
            var speed = BalanceSettings.PlayerProjectileSpeed(1) * loadout.ProjectileSpeedMultiplier;
            activePlayerShots.Add(CoopPlayerShotRules.Create(origin, enemyPosition, speed, loadout.Element, damage));
        }

        private void UpdateAuthoritativePlayerShots(float deltaTime)
        {
            if (CoopEnemyHealth <= 0)
            {
                activePlayerShots.Clear();
                return;
            }
            var enemyRadians = CoopEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * CoopEnemyRadius;
            var roomType = (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss);
            for (var i = activePlayerShots.Count - 1; i >= 0; i--)
            {
                var shot = activePlayerShots[i];
                var hit = CoopPlayerShotRules.Step(ref shot, deltaTime, enemyPosition, roomType);
                if (hit)
                {
                    activePlayerShots.RemoveAt(i);
                    if (CoopEnemyHealth > 0) ApplyAuthoritativeDamage(shot.Element, shot.Damage);
                }
                else if (shot.Life <= 0f)
                    activePlayerShots.RemoveAt(i);
                else
                    activePlayerShots[i] = shot;
            }
        }

        private void ApplyAuthoritativeDamage(DamageElement element, int damage)
        {
            if (hasLastElement && lastElementAge <= ResonanceWindow)
            {
                var reaction = ElementalCombat.ResolveReaction(lastElement, element);
                var bonus = ElementalCombat.ReactionBonus(reaction);
                if (bonus > 0)
                {
                    CoopResonance = reaction;
                    CoopResonanceTimer = 1.35f;
                    CoopResonanceSequence++;
                    damage += Mathf.RoundToInt(bonus * CoopRoomRules.ReactionBonusMultiplier(
                        (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss)));
                }
            }
            CoopEnemyHealth = Mathf.Max(0, CoopEnemyHealth - damage);
            lastElement = element;
            lastElementAge = 0f;
            hasLastElement = true;
            if (CoopEnemyHealth == 0) CoopEnemyDefeatedSequence++;
        }
    }
}
