# Orbital Rift — QA report

## Verified build path

- Unity: `6000.3.22f1` (`1c726e1fb402`).
- Android: IL2CPP, ARM64, minimum API 25, OpenGLES3.
- Package: `com.orbitalrift.studio`.
- Entry point: exported `com.unity3d.player.UnityPlayerActivity` with `MAIN` and `LAUNCHER` intent filters.
- Firebase: Anonymous Auth and Firestore leaderboards connected at runtime.

Final debug artifact from `2026-08-30`:

- file: `Builds/OrbitalRift-debug.apk`;
- size: `76,735,646` bytes;
- SHA-256: `C898F6E515D65BB02503FED7C73B0513A5D669CE698F0FD17BF25DFBD0F5C8CE`;
- installed and launched successfully in the Samsung S9 emulator.

## Samsung S9 emulator pass — 2026-08-29

Environment: Android API 37 AVD, `1440x2960`, density 570, portrait, headless SwiftShader renderer.

Passed:

- clean install and launch from Android;
- full-screen portrait layout and orbit fitting inside the visible play area;
- mandatory call sign flow;
- menu, settings, left/right touch zones, gameplay, result screen and return to menu;
- compact pause button and the paused-state overlay;
- a single non-overlapping MMR delta on the result screen;
- Firebase `ONLINE`, score leaderboard and MMR leaderboard;
- no fatal Unity or Firebase exception in filtered Logcat;
- runtime memory during gameplay measured at approximately `589 MB PSS` in a development IL2CPP build.

The emulator rendered roughly `12–25 FPS` depending on the scene. This number is a SwiftShader software-rendering diagnostic, not a physical Samsung S9 performance claim. The Android runtime now explicitly disables VSync and MSAA and targets 60 FPS; physical hardware remains the release gate.

## Live two-client Relay pass

Clients: Android host and Windows development client.

Passed:

- host party creation and join-code display;
- Windows join by party code and `2/2` player state on both clients;
- host-authoritative sector start;
- identical procedural seed and room state;
- guest movement received by the host simulation;
- shared team hull and damage state;
- completion of all `14/14` rooms and sector result;
- closing the Windows client and rejoining the same party code after relaunch.

## Mobile UI fixes from this pass

- leaderboard call signs are compacted instead of shrinking into unreadable text;
- current MMR label starts after the rank badge;
- bottom control hints are split across the left and right halves;
- the nickname error disappears immediately after valid input;
- the MMR result delta is no longer drawn twice during its falling animation;
- the pause button occupies the gap between HUD panels and does not cover the shield counter.

## Remaining release gates

1. Install the final debug APK on at least one physical Android phone and one tablet.
2. Verify stable frame pacing, memory, vibration and simultaneous music/SFX on hardware.
3. Configure the owner's upload keystore and build the release AAB.
4. Upload that AAB to Google Play internal testing and review pre-launch, crash and ANR reports.
5. Before competitive public release, move trusted MMR calculation to a backend or Cloud Function.
