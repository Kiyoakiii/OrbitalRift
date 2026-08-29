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
    /// First authoritative multiplayer slice. Clients send compact input only; the host advances
    /// both ships and broadcasts snapshots. Combat state can be added to the same host-owned tick.
    /// </summary>
    public sealed class CoopSimulationBridge : MonoBehaviour
    {
        private const string InputMessage = "orbital_rift/input/v1";
        private const string SnapshotMessage = "orbital_rift/snapshot/v1";
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
                if (RunStarted)
                {
                    HostAngleDegrees = CoopSimulationRules.StepAngle(HostAngleDegrees, command.OrbitDirection, Time.unscaledDeltaTime);
                    GuestAngleDegrees = CoopSimulationRules.StepAngle(GuestAngleDegrees, remoteDirection, Time.unscaledDeltaTime);
                    UpdateAuthoritativeFire(Time.unscaledDeltaTime);
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
            hostFireTimer = 0f;
            guestFireTimer = 0f;
            roomAdvanceTimer = 0f;
            remoteDirection = 0;
        }

        private void SendSnapshot(NetworkManager manager)
        {
            if (manager.ConnectedClientsIds == null || manager.ConnectedClientsIds.Count < 2) return;
            using (var writer = new FastBufferWriter(32, Allocator.Temp))
            {
                writer.WriteValueSafe(HostAngleDegrees);
                writer.WriteValueSafe(GuestAngleDegrees);
                writer.WriteValueSafe(HostShotSequence);
                writer.WriteValueSafe(GuestShotSequence);
                writer.WriteValueSafe(++snapshotSequence);
                writer.WriteValueSafe(ActiveRunSeed);
                writer.WriteValueSafe(ActiveRoomIndex);
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
            reader.ReadValueSafe(out byte runStarted);
            if (runStarted != 0 && !RunStarted) ResetRunCounters(runSeed);
            HostShotSequence = hostShots;
            GuestShotSequence = guestShots;
            ActiveRoomIndex = Mathf.Max(0, roomIndex);
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
            hostFireTimer = .25f;
            guestFireTimer = .48f;
            roomAdvanceTimer = 0f;
            snapshotSequence = 0;
            RunStarted = true;
        }

        private void AdvanceAuthoritativeRoom(float deltaTime)
        {
            roomAdvanceTimer += Mathf.Max(0f, deltaTime);
            if (roomAdvanceTimer < 8f || sessions == null || sessions.CurrentSector == null) return;
            roomAdvanceTimer = 0f;
            ActiveRoomIndex = Mathf.Min(ActiveRoomIndex + 1, sessions.CurrentSector.Rooms.Count - 1);
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
                hostFireTimer += BalanceSettings.PlayerFireInterval(1, false) * hostLoadout.FireIntervalMultiplier;
            }
            if (guestFireTimer <= 0f)
            {
                GuestShotSequence++;
                guestFireTimer += BalanceSettings.PlayerFireInterval(1, false) * guestLoadout.FireIntervalMultiplier;
            }
        }
    }
}
