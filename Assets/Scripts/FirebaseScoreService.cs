using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

namespace OrbitalRift
{
    public enum FirebaseConnectionState { Connecting, Online, Offline }

    public readonly struct LeaderboardEntry
    {
        public readonly string Nickname;
        public readonly int Value;

        public LeaderboardEntry(string nickname, int value)
        {
            Nickname = nickname;
            Value = value;
        }
    }

    /// <summary>
    /// Starts an anonymous Firebase session and synchronises public score and MMR tables.
    /// A failed network request never stops the offline game from running.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class FirebaseScoreService : MonoBehaviour
    {
        private const string LeaderboardCollection = "leaderboard";
        private const int LeaderboardSize = 5;
        private const string PendingScoreKey = "orbital_rift_pending_score";
        private const string PendingMmrKey = "orbital_rift_pending_mmr";
        private const string PendingNicknameKey = "orbital_rift_pending_nickname";
        private const string PendingRunIdKey = "orbital_rift_pending_run_id";
        private const string PendingResultHashKey = "orbital_rift_pending_result_hash";
        private const float RetryIntervalSeconds = 8f;

        private readonly List<LeaderboardEntry> scoreEntries = new List<LeaderboardEntry>(LeaderboardSize);
        private readonly List<LeaderboardEntry> mmrEntries = new List<LeaderboardEntry>(LeaderboardSize);
        private FirebaseAuth auth;
        private FirebaseFirestore database;
        private FirebaseUser user;
        private bool ready;
        private int pendingScore = -1;
        private int pendingMmr = -1;
        private string pendingNickname;
        private string pendingRunId;
        private string pendingResultHash;
        private int pendingRevision;
        private bool uploadInFlight;
        private float retryAt;

        public event Action<int> PersonalBestLoaded;
        public event Action<int> PersonalMmrLoaded;
        public event Action<IReadOnlyList<LeaderboardEntry>> ScoreLeaderboardLoaded;
        public event Action<IReadOnlyList<LeaderboardEntry>> MmrLeaderboardLoaded;
        public event Action<FirebaseConnectionState> ConnectionStateChanged;
        public FirebaseConnectionState ConnectionState { get; private set; } = FirebaseConnectionState.Connecting;

        private void Awake()
        {
            RestorePendingProgress();
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted || task.Result != DependencyStatus.Available)
                {
                    SetConnectionState(FirebaseConnectionState.Offline);
                    Debug.LogWarning("Firebase is unavailable. Orbital Rift will keep using local scores.");
                    return;
                }

                // Firestore does not use FirebaseOptions.DatabaseUrl. The SDK
                // otherwise emits a Realtime Database warning on every editor
                // launch, so keep Firebase logs focused on actionable errors.
                FirebaseApp.LogLevel = LogLevel.Error;
                auth = FirebaseAuth.DefaultInstance;
                database = FirebaseFirestore.DefaultInstance;
                if (auth.CurrentUser != null)
                {
                    SetReady(auth.CurrentUser);
                    return;
                }

                auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(signInTask =>
                {
                    if (signInTask.IsCanceled || signInTask.IsFaulted)
                    {
                        SetConnectionState(FirebaseConnectionState.Offline);
                        Debug.LogWarning("Anonymous Firebase sign-in failed. Orbital Rift will keep using local scores.");
                        return;
                    }

                    SetReady(signInTask.Result.User);
                });
            });
        }

        private void Update()
        {
            if (!ready || uploadInFlight || pendingScore < 0 || Time.unscaledTime < retryAt) return;
            retryAt = Time.unscaledTime + RetryIntervalSeconds;
            SavePendingProgress();
        }

        public void SubmitProgress(int score, int mmr, string nickname)
        {
            SubmitProgress(score, mmr, nickname, "legacy-" + DateTime.UtcNow.Ticks, string.Empty);
        }

        public void SubmitProgress(int score, int mmr, string nickname, string runId)
        {
            SubmitProgress(score, mmr, nickname, runId, string.Empty);
        }

        public void SubmitProgress(int score, int mmr, string nickname, string runId, string resultHash)
        {
            if (score < 0 || mmr < 0 || string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(runId)) return;

            pendingScore = Mathf.Max(pendingScore, score);
            pendingMmr = mmr;
            pendingNickname = nickname.Trim();
            if (pendingNickname.Length > 16) pendingNickname = pendingNickname.Substring(0, 16);
            pendingRunId = runId.Trim();
            if (pendingRunId.Length > 64) pendingRunId = pendingRunId.Substring(0, 64);
            pendingResultHash = string.IsNullOrWhiteSpace(resultHash) ? string.Empty : resultHash.Trim();
            if (pendingResultHash.Length > 32) pendingResultHash = pendingResultHash.Substring(0, 32);
            pendingRevision++;
            PersistPendingProgress();
            if (ready) SavePendingProgress();
        }

        public void RefreshLeaderboards()
        {
            if (!ready) return;

            LoadLeaderboard("score", scoreEntries, ScoreLeaderboardLoaded);
            LoadLeaderboard("mmr", mmrEntries, MmrLeaderboardLoaded);
        }

        private void LoadLeaderboard(string sortField, List<LeaderboardEntry> target, Action<IReadOnlyList<LeaderboardEntry>> callback)
        {
            database.Collection(LeaderboardCollection)
                .OrderByDescending(sortField)
                .Limit(LeaderboardSize)
                .GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        SetConnectionState(FirebaseConnectionState.Offline);
                        return;
                    }

                    SetConnectionState(FirebaseConnectionState.Online);
                    target.Clear();
                    foreach (var snapshot in task.Result.Documents)
                    {
                        if (!snapshot.Exists ||
                            !snapshot.TryGetValue("nickname", out string nickname) ||
                            !snapshot.TryGetValue(sortField, out long remoteValue)) continue;

                        target.Add(new LeaderboardEntry(nickname, Mathf.Clamp((int)Math.Min(remoteValue, int.MaxValue), 0, int.MaxValue)));
                    }

                    callback?.Invoke(target);
                });
        }

        private void SetReady(FirebaseUser authenticatedUser)
        {
            if (ready) return;

            user = authenticatedUser;
            ready = user != null;
            if (!ready) { SetConnectionState(FirebaseConnectionState.Offline); return; }

            SetConnectionState(FirebaseConnectionState.Online);
            LoadPersonalProgress();
            RefreshLeaderboards();
            SavePendingProgress();
        }

        private void LoadPersonalProgress()
        {
            database.Collection(LeaderboardCollection).Document(user.UserId).GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        SetConnectionState(FirebaseConnectionState.Offline);
                        return;
                    }
                    if (!task.Result.Exists) return;

                    SetConnectionState(FirebaseConnectionState.Online);
                    if (task.Result.TryGetValue("score", out long storedScore))
                        PersonalBestLoaded?.Invoke(Mathf.Clamp((int)Math.Min(storedScore, int.MaxValue), 0, int.MaxValue));
                    if (task.Result.TryGetValue("mmr", out long storedMmr))
                        PersonalMmrLoaded?.Invoke(Mathf.Clamp((int)Math.Min(storedMmr, int.MaxValue), 0, int.MaxValue));
                });
        }

        private void SavePendingProgress()
        {
            if (!ready || uploadInFlight || pendingScore < 0 || pendingMmr < 0 || string.IsNullOrWhiteSpace(pendingNickname)) return;

            var scoreToSave = pendingScore;
            var mmrToSave = pendingMmr;
            var nicknameToSave = pendingNickname;
            var runIdToSave = pendingRunId;
            var resultHashToSave = pendingResultHash;
            var revisionToSave = pendingRevision;
            uploadInFlight = true;

            var document = database.Collection(LeaderboardCollection).Document(user.UserId);
            document.GetSnapshotAsync().ContinueWithOnMainThread(readTask =>
            {
                if (readTask.IsCanceled || readTask.IsFaulted)
                {
                    uploadInFlight = false;
                    retryAt = Time.unscaledTime + RetryIntervalSeconds;
                    SetConnectionState(FirebaseConnectionState.Offline);
                    return;
                }

                long savedScore = 0;
                if (readTask.Result.Exists)
                {
                    readTask.Result.TryGetValue("score", out savedScore);
                    if (readTask.Result.TryGetValue("lastRunId", out string storedRunId) && storedRunId == runIdToSave)
                    {
                        uploadInFlight = false;
                        if (pendingRevision == revisionToSave)
                        {
                            pendingScore = -1;
                            pendingMmr = -1;
                            pendingNickname = null;
                            pendingRunId = null;
                            pendingResultHash = null;
                            ClearPendingProgress();
                        }
                        return;
                    }
                }
                var bestScore = Mathf.Clamp(
                    (int)Math.Min(Math.Max(savedScore, (long)scoreToSave), 100000000L),
                    0,
                    100000000);
                var data = new Dictionary<string, object>
                {
                    { "nickname", nicknameToSave },
                    { "score", bestScore },
                    { "mmr", Mathf.Clamp(mmrToSave, 0, 100000000) },
                    { "lastRunId", runIdToSave },
                    { "lastRunHash", resultHashToSave ?? string.Empty },
                    { "updatedAt", FieldValue.ServerTimestamp }
                };

                document.SetAsync(data).ContinueWithOnMainThread(writeTask =>
                {
                    uploadInFlight = false;
                    if (writeTask.IsCanceled || writeTask.IsFaulted)
                    {
                        retryAt = Time.unscaledTime + RetryIntervalSeconds;
                        SetConnectionState(FirebaseConnectionState.Offline);
                        return;
                    }

                    SetConnectionState(FirebaseConnectionState.Online);
                    if (pendingRevision == revisionToSave)
                    {
                        pendingScore = -1;
                        pendingMmr = -1;
                        pendingNickname = null;
                        pendingRunId = null;
                        pendingResultHash = null;
                        ClearPendingProgress();
                    }
                    PersonalBestLoaded?.Invoke(bestScore);
                    PersonalMmrLoaded?.Invoke(mmrToSave);
                    RefreshLeaderboards();
                    SavePendingProgress();
                });
            });
        }

        private void RestorePendingProgress()
        {
            pendingScore = PlayerPrefs.GetInt(PendingScoreKey, -1);
            pendingMmr = PlayerPrefs.GetInt(PendingMmrKey, -1);
            pendingNickname = PlayerPrefs.GetString(PendingNicknameKey, string.Empty);
            pendingRunId = PlayerPrefs.GetString(PendingRunIdKey, string.Empty);
            pendingResultHash = PlayerPrefs.GetString(PendingResultHashKey, string.Empty);
            if (pendingScore >= 0 && pendingMmr >= 0 && !string.IsNullOrWhiteSpace(pendingNickname))
            {
                pendingRevision = 1;
                if (string.IsNullOrWhiteSpace(pendingRunId)) pendingRunId = "legacy-pending";
            }
            else
            {
                pendingScore = -1;
                pendingMmr = -1;
                pendingNickname = null;
                pendingRunId = null;
                pendingResultHash = null;
            }
        }

        private void PersistPendingProgress()
        {
            PlayerPrefs.SetInt(PendingScoreKey, pendingScore);
            PlayerPrefs.SetInt(PendingMmrKey, pendingMmr);
            PlayerPrefs.SetString(PendingNicknameKey, pendingNickname ?? string.Empty);
            PlayerPrefs.SetString(PendingRunIdKey, pendingRunId ?? string.Empty);
            PlayerPrefs.SetString(PendingResultHashKey, pendingResultHash ?? string.Empty);
            PlayerPrefs.Save();
        }

        private static void ClearPendingProgress()
        {
            PlayerPrefs.DeleteKey(PendingScoreKey);
            PlayerPrefs.DeleteKey(PendingMmrKey);
            PlayerPrefs.DeleteKey(PendingNicknameKey);
            PlayerPrefs.DeleteKey(PendingRunIdKey);
            PlayerPrefs.DeleteKey(PendingResultHashKey);
            PlayerPrefs.Save();
        }

        private void SetConnectionState(FirebaseConnectionState state)
        {
            if (ConnectionState == state) return;
            ConnectionState = state;
            ConnectionStateChanged?.Invoke(state);
        }
    }
}
