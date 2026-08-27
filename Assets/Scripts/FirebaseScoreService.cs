using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

namespace OrbitalRift
{
    public readonly struct LeaderboardEntry
    {
        public readonly string Nickname;
        public readonly int Score;

        public LeaderboardEntry(string nickname, int score)
        {
            Nickname = nickname;
            Score = score;
        }
    }

    /// <summary>
    /// Starts an anonymous Firebase session and synchronises the public top scores.
    /// A failed network request never stops the offline game from running.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class FirebaseScoreService : MonoBehaviour
    {
        private const string LeaderboardCollection = "leaderboard";
        private const int LeaderboardSize = 5;

        private readonly List<LeaderboardEntry> entries = new List<LeaderboardEntry>(LeaderboardSize);
        private FirebaseAuth auth;
        private FirebaseFirestore database;
        private FirebaseUser user;
        private bool ready;
        private int pendingScore = -1;
        private string pendingNickname;

        public event Action<int> PersonalBestLoaded;
        public event Action<IReadOnlyList<LeaderboardEntry>> LeaderboardLoaded;

        private void Awake()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted || task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning("Firebase is unavailable. Orbital Rift will keep using local scores.");
                    return;
                }

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
                        Debug.LogWarning("Anonymous Firebase sign-in failed. Orbital Rift will keep using local scores.");
                        return;
                    }

                    SetReady(signInTask.Result.User);
                });
            });
        }

        public void SubmitBestScore(int score, string nickname)
        {
            if (score < 0 || string.IsNullOrWhiteSpace(nickname)) return;

            pendingScore = Mathf.Max(pendingScore, score);
            pendingNickname = nickname.Trim();
            if (ready) SavePendingScore();
        }

        public void RefreshLeaderboard()
        {
            if (!ready) return;

            database.Collection(LeaderboardCollection)
                .OrderByDescending("score")
                .Limit(LeaderboardSize)
                .GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted) return;

                    entries.Clear();
                    foreach (var snapshot in task.Result.Documents)
                    {
                        if (!snapshot.Exists ||
                            !snapshot.TryGetValue("nickname", out string nickname) ||
                            !snapshot.TryGetValue("score", out long remoteScore)) continue;

                        entries.Add(new LeaderboardEntry(nickname, Mathf.Clamp((int)Math.Min(remoteScore, int.MaxValue), 0, int.MaxValue)));
                    }

                    LeaderboardLoaded?.Invoke(entries);
                });
        }

        private void SetReady(FirebaseUser authenticatedUser)
        {
            if (ready) return;

            user = authenticatedUser;
            ready = user != null;
            if (!ready) return;

            LoadPersonalBest();
            RefreshLeaderboard();
            SavePendingScore();
        }

        private void LoadPersonalBest()
        {
            database.Collection(LeaderboardCollection).Document(user.UserId).GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted || !task.Result.Exists ||
                        !task.Result.TryGetValue("score", out long storedScore)) return;

                    PersonalBestLoaded?.Invoke(Mathf.Clamp((int)Math.Min(storedScore, int.MaxValue), 0, int.MaxValue));
                });
        }

        private void SavePendingScore()
        {
            if (!ready || pendingScore < 0 || string.IsNullOrWhiteSpace(pendingNickname)) return;

            var scoreToSave = pendingScore;
            var nicknameToSave = pendingNickname;
            pendingScore = -1;
            pendingNickname = null;

            var document = database.Collection(LeaderboardCollection).Document(user.UserId);
            document.GetSnapshotAsync().ContinueWithOnMainThread(readTask =>
            {
                if (readTask.IsCanceled || readTask.IsFaulted)
                {
                    pendingScore = Mathf.Max(pendingScore, scoreToSave);
                    pendingNickname = nicknameToSave;
                    return;
                }

                long savedScore = 0;
                if (readTask.Result.Exists) readTask.Result.TryGetValue("score", out savedScore);
                var bestScore = Mathf.Clamp(
                    (int)Math.Min(Math.Max(savedScore, (long)scoreToSave), 100000000L),
                    0,
                    100000000);
                var data = new Dictionary<string, object>
                {
                    { "nickname", nicknameToSave },
                    { "score", bestScore },
                    { "updatedAt", FieldValue.ServerTimestamp }
                };

                document.SetAsync(data).ContinueWithOnMainThread(writeTask =>
                {
                    if (writeTask.IsCanceled || writeTask.IsFaulted)
                    {
                        pendingScore = Mathf.Max(pendingScore, bestScore);
                        pendingNickname = nicknameToSave;
                        return;
                    }

                    RefreshLeaderboard();
                });
            });
        }
    }
}
