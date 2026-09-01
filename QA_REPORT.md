# Orbital Rift — QA report

## Verified build path

- Unity: `6000.3.22f1` (`1c726e1fb402`).
- Android: IL2CPP, ARM64, minimum API 25, OpenGLES3.
- Package: `com.orbitalrift.studio`.
- Entry point: exported `com.unity3d.player.UnityPlayerActivity` with `MAIN` and `LAUNCHER` intent filters.
- Firebase: Anonymous Auth and Firestore leaderboards connected at runtime.

Final debug artifact from `2026-08-31`:

- file: `Builds/OrbitalRift-debug.apk`;
- app version: `1.0.1` (`versionCode 2`);
- size: `67,914,635` bytes;
- SHA-256: `F8AB9B75C0652366EEC6011139EE9EE654C68B13E519E9D95ACF41F474F552C4`;
- package manifest exposes `com.unity3d.player.UnityPlayerActivity` as the launchable activity;
- the previous debug build was installed and launched successfully in the Samsung S9 emulator; this refreshed
  artifact passed the same Android product validation and still requires the requested physical-device retest.

Final signed release artifact from `2026-08-31`:

- file: `Builds/OrbitalRift-release.aab`;
- app version: `1.0.1` (`versionCode 2`);
- size: `39,922,541` bytes;
- SHA-256: `0B912101F74E11C7B56D6B0475E3FEEAF465033D6FDE832331590B89B2824CB9`;
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
the v10 core looks smooth to a guest under real Relay latency and that both clients observe the same boss return.

## Emergent energy-tether P0 pass — 2026-08-30

Passed in the local two-pilot Unity Game View and deterministic rule validation:

- snapshot protocol `v10` carries active, heat, overload timer and monotonic tether event state;
- the QA bot intentionally approached the host, connected the line and produced a visible overload/backlash;
- tension pulls both trajectory angles, the line damages crossed threats and charges a crossed relay core;
- backlash reduced shared hull but the formula preserves the last point, preventing accidental tether-only defeat;
- normal and overload states have different line color/width, compact HUD labels, particles, haptics and a
  synthesized SFX played over the uninterrupted music source;
- the overload line was visually reduced after the first pass so it remains readable without covering the arena;
- local two-pilot QA ignores background pulse damage and remains available for long mechanics tests;
- gameplay validation covers connection distance/cooldown, segment geometry, pull direction and final-HP safety.

The local pass does not replace a two-phone Relay test. Both physical clients still need to confirm v10 line
timing, shared overload outcome and acceptable correction under real network latency.

## Friendly-shot redirection P0 pass — 2026-08-30

Passed deterministic Unity validation and iterative local Game View tuning:

- snapshot protocol `v10` replicates the authoritative redirect sequence, kind, source pilot, element and position;
- a precise unpowered interception never removes shared hull and applies a 48-degree trajectory spin instead;
- an energized tether creates a `1.80` magnetic shield radius near the ally and bends shots near the muzzle;
- energized ricochets adopt the ally element, deal `x1.65` source damage and participate in Resonance;
- the shared `0.95` second cooldown prevents layered auto-fire SFX and event-banner spam;
- clients render an elemental burst, haptic pulse, synthesized ricochet SFX and a persistent HUD counter;
- deterministic tests cover exact/missed/magnetic interception, muzzle bending, boosted damage, cooldown and spin.

The rule and presentation pass is local. A physical two-phone test must still confirm that both clients see the
same redirect count, element, spin correction and resulting threat health under real Relay latency.

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
- gameplay touch zones remain active but no longer draw control frames or direction labels over the arena;
- the nickname error disappears immediately after valid input;
- the MMR result delta is no longer drawn twice during its falling animation;
- the pause button occupies the gap between HUD panels and does not cover the shield counter.

## Expedition clarity and audio-exit pass — 2026-08-31

Passed in Unity Game View and Android product validation:

- each procedural room presents a temporary card with its room type, objective and explicit hull danger;
- combat, elite and boss HUD states name the center threat as the source of pulse damage and show `-1` or `-2` hull;
- a `3.4` second room-entry grace period prevents both player shots and threat pulses until the enemy is visible;
- cooperative/Expedition threats render larger and at a minimum `0.82` alpha while entering from the center;
- damaging pulses now burst at both the threat and affected ship, add light haptics and play shield impact feedback;
- enemy-destruction audio uses a continuous attack/release waveform with exact zero endpoints plus a short
  anti-stacking cooldown to remove the Android post-kill click;
- leaving Expedition/co-op explicitly stops and rewinds the music; Classic death uses the same stop path;
- gameplay rules, Android product-readiness validation and signed release AAB build passed;
- `jarsigner -verify` returned `jar verified`.

## Expedition visible-combat pass — 2026-08-31

Passed deterministic gameplay validation, Unity Game View inspection and Android debug build:

- the former hidden timer damage is now a telegraphed center volley with visible hostile projectiles;
- the volley locks each ship's trajectory angle for `0.48` seconds, so moving out of the firing arc avoids hull damage;
- normal, elite and boss threats fire one, two and three visible bolts respectively; boss hits remove two hull points;
- the last player shot and relay-core impact resolve before a pending threat hit, preventing simultaneous post-mortem damage;
- a threat at zero armor emits its destruction burst, is disabled immediately and leaves a visible `1.8` second room-clear transition;
- the menu now includes a complete Expedition room guide covering safe rooms, combat, elites, events, shops and bosses;
- the temporary room card was moved upward and explicitly explains objective, danger and the entry grace period;
- Android debug APK `1.0.2` (`versionCode 3`) built successfully for `com.orbitalrift.studio`.

Physical Android testing remains required for projectile readability and the dodge window at device frame rate.

## Projectile collision and threat-pattern pass — 2026-08-31

Passed deterministic rules validation and Unity Game View inspection:

- player shots now remain simulated projectiles with position, velocity and lifetime; they damage a threat only
  when their travelled segment intersects its current position, so moving threats can be missed;
- the former center pulse is now a deterministic attack director with four patterns: aimed bolt, wide Cleave wave,
  expanding ring with a safe gap and three orbiting mine markers;
- every pattern chooses one target pilot, locks the target position, shows its own telegraph and resolves only after
  a readable windup; the HUD names the pattern and target (`P1`/`P2`);
- boss attacks cycle all four patterns, elites use wave/ring/bolt, and combat rooms mix bolts with mines;
- the multiplayer snapshot advanced to `v11` and carries attack pattern, target pilot and ring-gap angle;
- Unity Game View showed real hull loss only after a visible attack telegraph and confirmed threat-clear transitions.

The new Android debug build is version `1.0.3` (`versionCode 4`). Physical Android testing remains required for
frame-paced projectile readability and multiplayer timing.

## Flagship defense, dock upgrades and plasma-cleave pass — 2026-09-01

Validated by deterministic Unity gameplay rules before the Android build:

- Cleave is now a multi-layer plasma cut: broad glow, bright core, echo ribbon, transient trail,
  seven travelling energy nodes and launch/travel sparks; pooled impact shards restore their normal
  sorting layer after the effect;
- the expanding ring now has a `1.65s` telegraph and three 40-degree safe gaps placed 120 degrees apart;
- standard projectiles use swept segment-to-circle collision, so an edge clip on a large boss registers
  at low device frame rates; expedition boss hit radius is `0.76` world units;
- the generated sector always puts its sole boss in the actual final sequential room, after side rooms;
- the new final Sentinel uses a large readable texture and a `1.18` world-unit presentation scale;
- menu mode **Defense Flagship** puts a large central ship under attack: enemies move inward, waves
  escalate and the run ends only when the flagship hull reaches zero;
- Solo Expedition shop rooms now visibly dock the player ship beneath the flagship and present one
  choice from six run upgrades: Rapid Coils, Plasma Drive, Reactor x2, Prism Splitter, Field Repair
  and Aegis Force.

## Lower flagship defense and visible dock pass — 2026-09-01

Implementation coverage pending an in-editor Unity compile (the project is currently open in the user's editor):

- the defense target is now a dedicated large flagship sprite positioned at the lower screen edge; its
  `1.16` world-unit contact zone deliberately exceeds the visible hull, keeping the objective readable and
  easy for attackers to hit;
- defenders still orbit the central rift, while every hostile contact spawns in the central/upper field and
  visibly commits to the lower flagship rather than collapsing into the center;
- an Expedition shop transition lasts `3.9` seconds: the pilot leaves the orbit along a curved flight path,
  reaches a staging point, clamps into the flagship dock, emits travel sparks, and only then opens the shop;
- the flight uses a compact lower status strip instead of the previous full-screen shop window, so the actual
  approach and docking remain visible;
- **Prism Splitter** is renamed to **Prismatic Fan** (`ПРИЗМЕННЫЙ ВЕЕР`): every rank now creates two symmetric,
  visible side shots in both the simulation and visual projectile emission. The Expedition HUD displays the
  accumulated module state, for example `ВЕЕР +4`.

## Round flagship and focused shop pass — 2026-09-01

Implementation coverage pending an in-editor Unity compile (the editor is currently open):

- the protected flagship uses a dedicated circular grey orbital-station sprite with cyan reactor rings and small
  orange warning lights; its defense position moved to `y = -4.02`, while its visual diameter and cyan glow were
  reduced to preserve the central combat field;
- its `0.98` world-unit interception radius remains intentionally forgiving, so the station is still an accessible
  target for incoming contacts without needing a huge visible hull;
- Expedition modules now display an explicit per-module counter from the first shop: `ВЗЯТО 0/4`, `0/5`, `0/6`,
  `0/2` or `0/3`, depending on that module's real maximum rank. Full modules remain visible but dim and cannot be
  chosen again;
- shop docking and shop selection use a dedicated minimal HUD. The previous room objective, threat details,
  trajectory, resonance, pulse, collision and room-intro labels are not rendered over this focused state.

## Remaining release gates

1. Install the final debug APK on at least one physical Android phone and one tablet.
2. Verify stable frame pacing, memory, vibration and simultaneous music/SFX on hardware.
3. Store the upload keystore and recovery record in a password manager and an offline encrypted backup.
4. Upload the signed AAB to Google Play internal testing and review pre-launch, crash and ANR reports.
5. Before competitive public release, move trusted MMR calculation to a backend or Cloud Function.
