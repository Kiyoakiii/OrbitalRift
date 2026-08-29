# Firebase production checklist

The Unity client uses anonymous authentication and one Firestore document per user:
`leaderboard/{firebaseAuthUid}`. Public reads power the score and MMR leaderboards;
only the authenticated owner may write their document.

## Deploy the included rules

1. Install the Firebase CLI and sign in with an account that can manage the
   `orbital-rift` project.
2. From the repository root run:

```powershell
firebase use orbital-rift
firebase deploy --only firestore:rules
```

The included `firebase/firestore.rules` validates field names, nickname length,
numeric ranges, non-decreasing best score, the maximum MMR delta used by the
current game balance, and the optional `lastRunId` / `lastRunHash` idempotency
fields written by current clients. Deletion is denied.

The Unity client retries dependency/bootstrap and anonymous sign-in every 20
seconds after a failed startup. When an established Firestore connection drops,
it reloads personal progress and both leaderboards while the local pending result
queue continues its independent eight-second upload retry.

## Important anti-cheat boundary

Firestore rules can validate the shape and size of a client write, but they
cannot prove that a submitted match was genuinely played. Before a competitive
public release, move score/MMR calculation to a trusted backend (for example a
callable Cloud Function), submit a signed match result, and make leaderboard
documents server-write-only.

## Release checks

- Anonymous Authentication is enabled.
- Firestore is created in production mode and the supplied rules are deployed.
- Composite indexes requested by the Firebase console are committed if queries
  evolve beyond the current single-field `score` and `mmr` ordering.
- Android package name remains `com.orbitalrift.studio` or a new matching
  `google-services.json` is downloaded.
- Test accounts cannot update or delete another UID's leaderboard document.
