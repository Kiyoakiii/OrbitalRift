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

Final signed release artifact from `2026-08-30`:

- file: `Builds/OrbitalRift-release.aab`;
- size: `39,912,348` bytes;
- SHA-256: `B6C59895A83DB40F8E36F5D2A014E3436398B5858A213B2AFB36050F9948898E`;
- `jarsigner -verify` result: `jar verified`;
- signing certificate fingerprint: `E7:F9:C5:93:F8:47:8A:BC:F3:11:23:56:42:73:23:6D:AE:F9:2C:D3:7F:B5:7D:9B:B4:FF:92:DE:E6:FA:A4:F6`.

## Windows local co-op trajectory pass — 2026-08-30

Passed in the final Windows Development build:

- `ЛОКАЛЬНЫЙ ТЕСТ // 2 ПИЛОТА` remains available while the Unity Cloud project is linked;
- the arena holds and smoothly cycles through circle, ellipse and figure-eight forms;
- both ships remain attached to the changing line and continue firing toward the active threat;
- the HUD shows the current form/countdown and keeps room details below the route map without overlap;
- the static solo orbit is restored after leaving cooperative play.

## Co-op latency, collision and room-presentation pass — 2026-08-30

Passed in a fresh Windows Development build at a portrait `578x864` window:

- Unity product validation and gameplay rule tests compile and pass with the snapshot protocol at v7;
- the guest path has frame-local command prediction, host reconciliation and a live transport RTT label;
- automated geometry checks hit the figure-eight crossing, apply a separating bounce and reject a false circle collision;
- the compact header, route, objective and twin health bars leave the center of the arena visible;
- deterministic standard and elite rooms visibly switch their color wash and geometric background motifs;
- the shared trajectory was observed progressing through circle, ellipse and figure-eight without leaving the portrait frame;
- Windows Development build completed successfully at `Builds/OrbitalRift.exe`.

Not claimed by this desktop pass: a new two-physical-device high-latency Relay measurement. The reported
US-VPN delay should be repeated without VPN; prediction removes local steering lag, while authoritative
combat and collision confirmation still depend on real RTT to the phone host through Relay.

## Large co-op figure-eight pass — 2026-08-30

Passed in a fresh Windows Development build at a portrait `578x864` window:

- both pilots spawn directly on opposite lobes of a stable figure-eight;
- HUD reports `ТРАЕКТОРИЯ // ВОСЬМЕРКА` from the first rendered gameplay frame;
- cooperative radius is `4.25`, more than 32% larger than the solo `3.2` orbit;
- threat spawn, approach radius and fade presentation scale with the larger arena;
- shape-aware camera zoom keeps the large figure-eight close to the safe horizontal edges without clipping ships, markers, HUD or touch controls;
- automated gameplay rules and the final Windows Development build pass.

## Dual solo-mode pass — 2026-08-30

Passed in a fresh Windows Development build at a portrait `578x864` window:

- the main menu cleanly exposes `СОЛО // КЛАССИКА`, `СОЛО // ЭКСПЕДИЦИЯ` and `КООП // 2 ИГРОКА` without overlap;
- Solo Expedition starts offline on a large figure-eight with one visible ship, a random hexadecimal seed and `КОРПУС 5/5`;
- the bot ship, bot marker, bot fire, ship collision and free same-element Resonance are absent from Solo Expedition;
- leaving Expedition restores the menu and Classic Solo still starts the unchanged original phase-based game;
- rules validation and Windows Development compilation pass.

The persistence path is compile-checked but this visual pass intentionally exited before a ranked result,
so it did not submit a synthetic QA score to the live Firebase leaderboard.

## Emergent relay-core P0 pass — 2026-08-30

Passed in Unity Game View and deterministic build-time validation:

- snapshot protocol `v8` carries the host-authoritative core position, velocity, charge, element,
  dangerous-return state and monotonic impact event;
- Solo Expedition visibly spawned the same unstable core with a cyan trail and compact `ЯДРО 0/3` HUD;
- the core remained inside a mobile-safe elliptical boundary instead of entering the bottom touch controls;
- rules tests cover deterministic room activation, shot-ray capture, speed clamping, horizontal/vertical
  boundary reflection and charged-impact damage;
- gameplay rules and Android product-readiness validators both passed in Unity `6000.3.22f1`;
- the final signed release AAB was rebuilt, and `jarsigner -verify` returned `jar verified` with exit code 0.

The desktop visual pass validated offline physics/presentation. A two-phone pass must still confirm that
the v8 core looks smooth to a guest under real Relay latency and that both clients observe the same boss return.

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
3. Store the upload keystore and recovery record in a password manager and an offline encrypted backup.
4. Upload the signed AAB to Google Play internal testing and review pre-launch, crash and ANR reports.
5. Before competitive public release, move trusted MMR calculation to a backend or Cloud Function.
