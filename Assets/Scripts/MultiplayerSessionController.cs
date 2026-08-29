using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace OrbitalRift
{
    public enum PartyConnectionState
    {
        Offline,
        Initializing,
        Ready,
        Hosting,
        Joining,
        Reconnecting,
        InParty,
        Leaving,
        Error
    }

    /// <summary>
    /// Owns the Unity Multiplayer Services session. Gameplay code must only depend on
    /// the compact player commands and authoritative state, not on service APIs.
    /// </summary>
    public sealed class MultiplayerSessionController : MonoBehaviour
    {
        private const string SessionType = "orbital-rift-coop-v1";
        private const ushort NetworkProtocolVersion = 1;
        private const string RunSeedProperty = "run_seed";
        private const string RunIdProperty = "run_id";
        private const string CallsignProperty = "callsign";
        private const string ShipProperty = "ship";

        public event Action StateChanged;

        public PartyConnectionState State { get; private set; } = PartyConnectionState.Offline;
        public ISession CurrentSession { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public string PartyCode => CurrentSession?.Code ?? string.Empty;
        public int PlayerCount => CurrentSession?.PlayerCount ?? 0;
        public bool IsHost => CurrentSession != null && CurrentSession.IsHost;
        public int RunSeed { get; private set; }
        public SectorLayout CurrentSector { get; private set; }
        public ShipArchetype HostShip => ReadShip(true);
        public ShipArchetype GuestShip => ReadShip(false);
        public string HostCallsign => ReadCallsign(true);
        public string GuestCallsign => ReadCallsign(false);
        public Unity.Services.Multiplayer.SessionState NetworkSessionState { get; private set; } = Unity.Services.Multiplayer.SessionState.None;
        public int ReconnectAttempts { get; private set; }
        public bool IsBusy => State == PartyConnectionState.Initializing ||
                              State == PartyConnectionState.Hosting ||
                              State == PartyConnectionState.Joining ||
                              State == PartyConnectionState.Reconnecting ||
                              State == PartyConnectionState.Leaving;
        public bool HasUnityCloudProject => !string.IsNullOrWhiteSpace(Application.cloudProjectId);
        /// <summary>Stable id shared by both members of the current party run.</summary>
        public string RunId { get; private set; } = string.Empty;

        private bool reconnectInFlight;
        private float reconnectAt;

        private void Update()
        {
            if (CurrentSession == null || NetworkSessionState != Unity.Services.Multiplayer.SessionState.Disconnected ||
                reconnectInFlight || Time.unscaledTime < reconnectAt) return;
            _ = TryReconnectAsync();
        }

        public async Task<bool> InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance.IsSignedIn)
            {
                SetState(PartyConnectionState.Ready);
                return true;
            }

            if (!HasUnityCloudProject)
                return Fail("Unity Cloud Project не привязан. Открой Edit > Project Settings > Services и привяжи проект.");

            try
            {
                LastError = string.Empty;
                SetState(PartyConnectionState.Initializing);
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                SetState(PartyConnectionState.Ready);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Fail("Не удалось подключить сетевые сервисы: " + exception.Message);
            }
        }

        public async Task<bool> CreatePartyAsync(string hostName, ShipArchetype ship)
        {
            if (IsBusy || CurrentSession != null) return false;
            if (!await InitializeAsync()) return false;

            try
            {
                LastError = string.Empty;
                SetState(PartyConnectionState.Hosting);
                EnsureNetworkManager();
                RunSeed = Guid.NewGuid().GetHashCode();
                RunId = "coop-" + Guid.NewGuid().ToString("N");
                var options = new SessionOptions
                {
                    Type = SessionType,
                    Name = string.IsNullOrWhiteSpace(hostName) ? "Orbital Rift Party" : hostName.Trim() + " Party",
                    MaxPlayers = 2,
                    IsPrivate = true,
                    PlayerProperties = BuildPlayerProperties(hostName, ship),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { RunSeedProperty, new SessionProperty(RunSeed.ToString(), VisibilityPropertyOptions.Member) },
                        { RunIdProperty, new SessionProperty(RunId, VisibilityPropertyOptions.Member) }
                    }
                }.WithRelayNetwork();

                SetSession(await MultiplayerService.Instance.CreateSessionAsync(options));
                SetState(PartyConnectionState.InParty);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShutdownNetwork();
                return Fail("Не удалось создать пати: " + exception.Message);
            }
        }

        public async Task<bool> JoinPartyAsync(string partyCode, string playerName, ShipArchetype ship)
        {
            if (IsBusy || CurrentSession != null) return false;
            var normalizedCode = string.IsNullOrWhiteSpace(partyCode)
                ? string.Empty
                : partyCode.Trim().ToUpperInvariant();
            if (normalizedCode.Length < 4) return Fail("Введи корректный код пати.");
            if (!await InitializeAsync()) return false;

            try
            {
                LastError = string.Empty;
                SetState(PartyConnectionState.Joining);
                EnsureNetworkManager();
                var options = new JoinSessionOptions
                {
                    Type = SessionType,
                    PlayerProperties = BuildPlayerProperties(playerName, ship)
                };
                SetSession(await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode, options));
                SetState(PartyConnectionState.InParty);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShutdownNetwork();
                return Fail("Не удалось войти в пати: " + exception.Message);
            }
        }

        public async Task LeavePartyAsync()
        {
            if (CurrentSession == null || State == PartyConnectionState.Leaving) return;
            SetState(PartyConnectionState.Leaving);
            try
            {
                if (CurrentSession != null) await CurrentSession.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Party leave failed: " + exception.Message);
            }
            finally
            {
                SetSession(null);
                RunSeed = 0;
                RunId = string.Empty;
                CurrentSector = null;
                ReconnectAttempts = 0;
                NetworkSessionState = Unity.Services.Multiplayer.SessionState.None;
                // Relay's Multiplayer Services network handler owns NGO shutdown
                // for a live session. Calling NetworkManager.Shutdown() again here
                // races its async StopAsync and can dispose SceneManager twice.
                SetState(PartyConnectionState.Ready);
            }
        }

        public async Task<bool> ReconnectNowAsync()
        {
            if (CurrentSession == null || reconnectInFlight) return false;
            reconnectAt = 0f;
            await TryReconnectAsync();
            return NetworkSessionState == Unity.Services.Multiplayer.SessionState.Connected;
        }

        private static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            var networkObject = new GameObject("Orbital Rift Network Manager");
            networkObject.SetActive(false);
            var transport = networkObject.AddComponent<UnityTransport>();
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ProtocolVersion = NetworkProtocolVersion,
                TickRate = 30,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                PlayerPrefab = null
            };
            DontDestroyOnLoad(networkObject);
            networkObject.SetActive(true);
        }

        private void SetSession(ISession session)
        {
            if (CurrentSession != null)
            {
                CurrentSession.Changed -= NotifyStateChanged;
                CurrentSession.StateChanged -= HandleSessionStateChanged;
            }
            CurrentSession = session;
            NetworkSessionState = CurrentSession == null ? Unity.Services.Multiplayer.SessionState.None : CurrentSession.State;
            if (CurrentSession != null)
            {
                CurrentSession.Changed += NotifyStateChanged;
                CurrentSession.StateChanged += HandleSessionStateChanged;
            }
            RefreshRunMetadata();
            NotifyStateChanged();
        }

        private void OnDestroy()
        {
            if (CurrentSession != null)
            {
                CurrentSession.Changed -= NotifyStateChanged;
                CurrentSession.StateChanged -= HandleSessionStateChanged;
            }
        }

        private void HandleSessionStateChanged(Unity.Services.Multiplayer.SessionState state)
        {
            NetworkSessionState = state;
            if (state == Unity.Services.Multiplayer.SessionState.Connected)
            {
                reconnectAttempts = 0;
                ReconnectAttempts = 0;
                if (State == PartyConnectionState.Reconnecting) SetState(PartyConnectionState.InParty);
            }
            else if (state == Unity.Services.Multiplayer.SessionState.Disconnected && CurrentSession != null)
            {
                SetState(PartyConnectionState.Reconnecting);
                reconnectAt = Time.unscaledTime + 1f;
            }
            else if (state == Unity.Services.Multiplayer.SessionState.Deleted)
            {
                Fail("Пати было закрыто. Создай новую комнату.");
            }
            else
            {
                NotifyStateChanged();
            }
        }

        private int reconnectAttempts;

        private async Task TryReconnectAsync()
        {
            var session = CurrentSession;
            if (session == null || reconnectInFlight || NetworkSessionState != Unity.Services.Multiplayer.SessionState.Disconnected) return;
            reconnectInFlight = true;
            reconnectAttempts++;
            ReconnectAttempts = reconnectAttempts;
            SetState(PartyConnectionState.Reconnecting);
            try
            {
                // Keep a stable reference across the await. Leaving the party or
                // a service callback can clear CurrentSession while the request is
                // in flight; dereferencing the property afterwards used to produce
                // a NullReferenceException and an endless reconnect loop.
                await session.ReconnectAsync();
                if (!ReferenceEquals(CurrentSession, session)) return;
                NetworkSessionState = session.State;
                if (NetworkSessionState == Unity.Services.Multiplayer.SessionState.Connected)
                {
                    reconnectAttempts = 0;
                    ReconnectAttempts = 0;
                    LastError = string.Empty;
                    SetState(PartyConnectionState.InParty);
                }
                else
                {
                    reconnectAt = Time.unscaledTime + Mathf.Min(12f, 1.5f + reconnectAttempts * .8f);
                }
            }
            catch (Exception exception)
            {
                if (!ReferenceEquals(CurrentSession, session)) return;
                LastError = "Сеть потеряна, переподключение " + reconnectAttempts + "/∞";
                Debug.LogWarning("Party reconnect failed: " + exception.Message);
                reconnectAt = Time.unscaledTime + Mathf.Min(12f, 1.5f + reconnectAttempts * .8f);
                NotifyStateChanged();
            }
            finally
            {
                reconnectInFlight = false;
            }
        }

        private void SetState(PartyConnectionState state)
        {
            State = state;
            NotifyStateChanged();
        }

        private bool Fail(string message)
        {
            LastError = message;
            SetState(PartyConnectionState.Error);
            return false;
        }

        private void NotifyStateChanged()
        {
            RefreshRunMetadata();
            StateChanged?.Invoke();
        }

        private void RefreshRunMetadata()
        {
            if (CurrentSession == null || CurrentSession.Properties == null) return;

            if (CurrentSession.Properties.TryGetValue(RunIdProperty, out var runIdProperty) &&
                !string.IsNullOrWhiteSpace(runIdProperty.Value))
                RunId = runIdProperty.Value.Trim();

            if (!CurrentSession.Properties.TryGetValue(RunSeedProperty, out var seedProperty) ||
                !int.TryParse(seedProperty.Value, out var parsedSeed)) return;
            if (RunSeed == parsedSeed && CurrentSector != null) return;
            RunSeed = parsedSeed;
            CurrentSector = SectorGenerator.Generate(RunSeed);
        }

        private static Dictionary<string, PlayerProperty> BuildPlayerProperties(string callsign, ShipArchetype ship)
        {
            return new Dictionary<string, PlayerProperty>
            {
                { CallsignProperty, new PlayerProperty(string.IsNullOrWhiteSpace(callsign) ? "PILOT" : callsign.Trim(), VisibilityPropertyOptions.Member) },
                { ShipProperty, new PlayerProperty(((int)ship).ToString(), VisibilityPropertyOptions.Member) }
            };
        }

        private ShipArchetype ReadShip(bool host)
        {
            var player = FindPlayer(host);
            if (player?.Properties != null && player.Properties.TryGetValue(ShipProperty, out var property) &&
                int.TryParse(property.Value, out var value)) return ShipLoadoutSettings.Clamp(value);
            return host ? ShipArchetype.Vanguard : ShipArchetype.Interceptor;
        }

        private string ReadCallsign(bool host)
        {
            var player = FindPlayer(host);
            if (player?.Properties != null && player.Properties.TryGetValue(CallsignProperty, out var property) &&
                !string.IsNullOrWhiteSpace(property.Value)) return property.Value.Trim();
            return host ? "HOST" : "GUEST";
        }

        private IReadOnlyPlayer FindPlayer(bool host)
        {
            if (CurrentSession?.Players == null) return null;
            for (var i = 0; i < CurrentSession.Players.Count; i++)
            {
                var player = CurrentSession.Players[i];
                if ((player.Id == CurrentSession.Host) == host) return player;
            }
            return null;
        }

        private static void ShutdownNetwork()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
    }
}
