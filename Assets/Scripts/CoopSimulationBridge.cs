using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OrbitalRift
{
    public static class CoopSimulationRules
    {
        public const float OrbitDegreesPerSecond = 115f;

        public static float StepAngle(float angleDegrees, int direction, float deltaTime)
        {
            direction = Mathf.Clamp(direction, -1, 1);
            return Mathf.Repeat(angleDegrees + direction * OrbitDegreesPerSecond * Mathf.Max(0f, deltaTime), 360f);
        }
    }

    /// <summary>
    /// Shared room modifiers. They are derived from the seeded layout on every
    /// device, so the host and guest never need another network message for them.
    /// </summary>
    public static class CoopRoomRules
    {
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
        private const string SnapshotMessage = "orbital_rift/snapshot/v4";
        private const string StartRunMessage = "orbital_rift/start/v1";
        private const float NetworkInterval = 1f / 20f;
        private const float RemoteInputTimeout = .25f;

        public float HostAngleDegrees { get; private set; } = 210f;
        public float GuestAngleDegrees { get; private set; } = 330f;
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
        private uint snapshotSequence;
        private float hostFireTimer;
        private float guestFireTimer;
        private float roomAdvanceTimer;
        private float threatPulseTimer;
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

            if (manager.IsServer)
            {
                if (Time.unscaledTime - lastRemoteInputAt > RemoteInputTimeout) remoteDirection = 0;
                if (RunStarted && !RunCompleted)
                {
                    HostAngleDegrees = CoopSimulationRules.StepAngle(HostAngleDegrees, command.OrbitDirection, Time.unscaledDeltaTime);
                    GuestAngleDegrees = CoopSimulationRules.StepAngle(GuestAngleDegrees, remoteDirection, Time.unscaledDeltaTime);
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
                GuestAngleDegrees = Mathf.LerpAngle(GuestAngleDegrees, targetGuestAngle, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
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
            ActiveRoomIndex = 0;
            CoopEnemyAngle = 90f;
            CoopEnemyRadius = .45f;
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
            SnapshotAgeSeconds = 99f;
            RunCompleted = false;
            RunCompletionSequence = 0;
            hasLastElement = false;
            lastElement = DamageElement.Kinetic;
            lastElementAge = 0f;
            hostFireTimer = 0f;
            guestFireTimer = 0f;
            roomAdvanceTimer = 0f;
            remoteDirection = 0;
        }

        private void SendSnapshot(NetworkManager manager)
        {
            if (manager.ConnectedClientsIds == null || manager.ConnectedClientsIds.Count < 2) return;
            using (var writer = new FastBufferWriter(80, Allocator.Temp))
            {
                writer.WriteValueSafe(HostAngleDegrees);
                writer.WriteValueSafe(GuestAngleDegrees);
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
            reader.ReadValueSafe(out targetHostAngle);
            reader.ReadValueSafe(out targetGuestAngle);
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
            if (runStarted != 0 && !RunStarted) ResetRunCounters(runSeed);
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
            threatPulseTimer = 0f;
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
            if (RunCompleted || roomAdvanceTimer < 8f || CoopEnemyHealth > 0 || sessions == null || sessions.CurrentSector == null) return;
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
            CoopEnemyRadius = .42f;
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
            CoopEnemyRadius = Mathf.MoveTowards(CoopEnemyRadius, 2.55f, Mathf.Max(0f, deltaTime) * .34f);
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
