using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OrbitalRift
{
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
    }

    /// <summary>
    /// First authoritative multiplayer slice. Clients send compact input only; the host advances
    /// both ships and broadcasts snapshots. Combat state can be added to the same host-owned tick.
    /// </summary>
    public sealed class CoopSimulationBridge : MonoBehaviour
    {
        private const string InputMessage = "orbital_rift/input/v1";
        private const string SnapshotMessage = "orbital_rift/snapshot/v7";
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
        private float threatPulseTimer;
        private float teamDamageCooldown;
        private float shipCollisionCooldown;
        private float rttRefreshTimer;
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
                    UpdateAuthoritativeFire(Time.unscaledDeltaTime);
                    UpdateAuthoritativeResonance(Time.unscaledDeltaTime);
                    UpdateAuthoritativeEnemy(Time.unscaledDeltaTime);
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
            CoopTeamHealth = CoopRoomRules.TeamMaxHealth;
            CoopTeamMaxHealth = CoopRoomRules.TeamMaxHealth;
            RunFailed = false;
            RunFailureSequence = 0;
            ShipCollisionSequence = 0;
            ShipCollisionPosition = Vector2.zero;
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
            teamDamageCooldown = 0f;
            shipCollisionCooldown = 0f;
            rttRefreshTimer = 0f;
            remoteDirection = 0;
        }

        private void SendSnapshot(NetworkManager manager)
        {
            if (manager.ConnectedClientsIds == null || manager.ConnectedClientsIds.Count < 2) return;
            using (var writer = new FastBufferWriter(160, Allocator.Temp))
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
            RunCompleted = runCompleted != 0;
            RunCompletionSequence = runCompletionSequence;
            CoopTeamHealth = Mathf.Clamp(teamHealth, 0, CoopRoomRules.TeamMaxHealth);
            CoopTeamMaxHealth = Mathf.Clamp(teamMaxHealth, 1, CoopRoomRules.TeamMaxHealth);
            RunFailed = runFailed != 0;
            RunFailureSequence = runFailureSequence;
            var collisionChanged = collisionSequence != ShipCollisionSequence;
            ShipCollisionSequence = collisionSequence;
            ShipCollisionPosition = new Vector2(collisionX, collisionY);
            if (collisionChanged)
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
            HostAngleDegrees = targetHostAngle = 210f;
            GuestAngleDegrees = targetGuestAngle = 330f;
            TrajectoryTimeSeconds = targetTrajectoryTime = CoopTrajectorySettings.InitialElapsedSeconds;
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
            CoopTeamHealth = CoopRoomRules.TeamMaxHealth;
            CoopTeamMaxHealth = CoopRoomRules.TeamMaxHealth;
            RunFailed = false;
            RunFailureSequence = 0;
            ShipCollisionSequence = 0;
            ShipCollisionPosition = Vector2.zero;
            threatPulseTimer = 0f;
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
            roomAdvanceTimer += Mathf.Max(0f, deltaTime);
            if (RunCompleted || RunFailed || roomAdvanceTimer < 8f || CoopEnemyHealth > 0 || sessions == null || sessions.CurrentSector == null) return;
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
        }

        private void UpdateAuthoritativeEnemy(float deltaTime)
        {
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

        private void UpdateAuthoritativeThreatPulse(float deltaTime)
        {
            if (CoopEnemyHealth <= 0) return;
            teamDamageCooldown = Mathf.Max(0f, teamDamageCooldown - Mathf.Max(0f, deltaTime));
            threatPulseTimer -= Mathf.Max(0f, deltaTime);
            CoopThreatPulseTimer = Mathf.Max(0f, CoopThreatPulseTimer - Mathf.Max(0f, deltaTime));
            if (threatPulseTimer > 0f) return;

            var roomType = (SectorRoomType)Mathf.Clamp(CoopEnemyKind, 0, (int)SectorRoomType.Boss);
            var interval = CoopRoomRules.ThreatPulseInterval(roomType);
            threatPulseTimer = interval;
            CoopThreatPulseTimer = .42f;
            CoopThreatPulseSequence++;
            CoopThreatPulseElement = roomType == SectorRoomType.Boss
                ? (DamageElement)(ActiveRoomIndex % 3 + 1)
                : (DamageElement)(ActiveRoomIndex % 4);

            var damage = CoopRoomRules.ThreatDamage(roomType);
            if (damage > 0 && teamDamageCooldown <= 0f)
            {
                CoopTeamHealth = Mathf.Max(0, CoopTeamHealth - damage);
                teamDamageCooldown = CoopRoomRules.TeamDamageCooldown(roomType);
                if (CoopTeamHealth == 0)
                {
                    RunFailed = true;
                    RunFailureSequence++;
                }
            }
        }

        private void UpdateAuthoritativeFire(float deltaTime)
        {
            var hostLoadout = ShipLoadoutSettings.Get(sessions.HostShip);
            var guestLoadout = ShipLoadoutSettings.Get(sessions.GuestShip);
            hostFireTimer -= deltaTime;
            guestFireTimer -= deltaTime;
            if (hostFireTimer <= 0f)
            {
                HostShotSequence++;
                ApplyAuthoritativeShot(sessions.HostShip);
                hostFireTimer += BalanceSettings.PlayerFireInterval(1, false) * hostLoadout.FireIntervalMultiplier;
            }
            if (guestFireTimer <= 0f)
            {
                GuestShotSequence++;
                ApplyAuthoritativeShot(sessions.GuestShip);
                guestFireTimer += BalanceSettings.PlayerFireInterval(1, false) * guestLoadout.FireIntervalMultiplier;
            }
        }

        private void ApplyAuthoritativeShot(ShipArchetype ship)
        {
            if (CoopEnemyHealth <= 0) return;
            var loadout = ShipLoadoutSettings.Get(ship);
            var resistance = CoopEnemyKind == (byte)SectorRoomType.Boss ? BossSettings.Resistance(loadout.Element) : 1f;
            var damage = Mathf.Max(1, Mathf.RoundToInt(ElementalCombat.ApplyResistance(loadout.DamageMultiplier, resistance)));
            if (hasLastElement && lastElementAge <= ResonanceWindow)
            {
                var reaction = ElementalCombat.ResolveReaction(lastElement, loadout.Element);
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
            lastElement = loadout.Element;
            lastElementAge = 0f;
            hasLastElement = true;
            if (CoopEnemyHealth == 0) CoopEnemyDefeatedSequence++;
        }
    }
}
