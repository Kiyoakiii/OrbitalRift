# Orbital Rift Multiplayer 2.0

## Product pillar

Two phone players explore a seeded sector together. The replayable core is **Resonance**:
the ships, their elemental weapons and the environment follow one shared reaction system.
Players discover useful combinations instead of memorizing one scripted solution per room.

## Non-negotiable architecture

- Two players per session for the first release.
- Client-hosted authoritative simulation through Unity Multiplayer Services and Relay. Relay transports
  packets but is not a dedicated gameplay server; Google Play also does not execute game simulation.
- Netcode for GameObjects over Unity Transport.
- The host owns the run seed, procedural layout, enemies, damage, drops and boss AI.
- Clients send compact `PlayerCommandFrame` input; clients never author damage or rewards.
- Physics is synchronized from the host. Do not use deterministic lockstep with Unity physics.
- Firebase remains responsible for profiles, MMR, leaderboards and persistent progression.
- A disconnect must never submit a second MMR result for the same run id.

## Resonance rules

Initial elements:

| Source A | Source B | Reaction | Tactical use |
| --- | --- | --- | --- |
| Fire | Poison | Ignition | Explodes poison clouds and spreads fire |
| Cold | Poison | Cryotoxin | Slow field that increases stagger damage |
| Fire | Cold | Steam | Breaks target lock but reduces visibility |
| Cold | Kinetic | Shatter | Frozen targets emit damaging shards |
| Two matching cores | Same element | Overcharge | Strong effect with heat/instability cost |

The same reactions must affect enemies, bosses, hazards and interactive room objects.
Boss resistance is a multiplier, not permanent immunity, so neither player becomes useless.

## Milestones

### M0 - Preserve the solo game

- Keep the current boot flow and Android build operational.
- Validate balance and product rules in batch mode.
- Add network code without automatically initializing cloud services in solo mode.

### M1 - Connection slice

- Link the Unity Cloud Project and enable Relay/Lobby services.
- Add a Coop screen: create party, display code, join by code, leave.
- Verify host plus one client on two physical Android devices.
- Spawn two placeholder network ships in a small test sector.

Exit criterion: both devices see the same two ships and connection state.

### M2 - Authoritative combat slice

- Host consumes `PlayerCommandFrame` for both ships.
- Synchronize movement, shots, enemies, health and pickups.
- Add client interpolation and latency diagnostics.
- Reject duplicated hit/reward events.

Exit criterion: both players can complete one wave and receive the same result.

### M3 - Elements and Resonance

- Data-driven ship hull, weapon and element definitions.
- Fire, cold, poison and kinetic damage types.
- Five initial reactions plus two cooperative room interactions.
- One boss whose armor requires a coordinated reaction.

Exit criterion: the same encounter supports several valid emergent solutions.

### M4 - Seeded procedural sector

- Generate a validated room graph from a host-owned seed.
- Use handcrafted room modules, connectors, encounter budgets and a boss arena.
- Stream nearby room chunks on mobile.
- Store and display run id plus seed for debugging and daily challenges.

Exit criterion: 100 automated seeds contain a valid start-to-boss route and reproduce exactly.

### M5 - Production beta

- Reconnect and interrupted-session recovery.
- Idempotent rewards and server-verifiable result payloads.
- Android thermal, memory and network-loss testing.
- Cooperative onboarding, accessibility and balance telemetry.

## Current implementation

- Unity Editor: `6000.3.22f1`.
- `com.unity.netcode.gameobjects`: `2.13.1`.
- `com.unity.services.multiplayer`: `2.1.1`.
- Local input is isolated behind `IPlayerCommandSource`.
- `MultiplayerSessionController` supports initialization, two-player Relay party creation,
  joining by short code and leaving the session.
- The main menu contains a responsive Coop screen with create/join/leave flows,
  party code display, copy action and player count.
- Projectiles carry a `DamageElement`; the first boss uses non-zero elemental
  resistance multipliers while kinetic damage preserves the solo balance.
- The shared Resonance matrix already defines Ignition, Cryotoxin, Steam,
  Shatter and same-element Overcharge and is covered by gameplay rule tests.
- Four selectable ship archetypes now change element, fire cadence, projectile speed and damage.
  The choice is stored on device and published as a member-only player property when joining a party.
- The host publishes a member-only run seed with the party. Both devices derive the same validated
  sector graph locally; 100 automated seeds are checked for connectivity and exact reproducibility.
- `CoopSimulationBridge` is the first host-authoritative runtime tick: each client sends only a
  normalized orbit command, the host advances both ship angles, and 20 Hz snapshots are interpolated
  by the guest. The guest now predicts its own orbit command every rendered frame and reconciles with
  the next authoritative snapshot, so its touch response no longer waits for a full Relay round trip.
  Discrete collision events still snap to the host result. The compact HUD reports transport RTT and
  marks the guest path as `GUEST PREDICT`, making VPN/region latency measurable during a playtest.
  Remote input automatically returns to neutral if packets stop arriving. The same
  snapshot carries authoritative shot sequence counters, the current procedural room index and the
  active threat's angle, radius, health, room kind and defeat sequence; the host advances the route
  every eight seconds only after the threat is cleared, until the generated boss room.
- Host combat applies ship-element resistances and a short Resonance window. Coordinated pairs resolve
  Ignition, Cryotoxin, Steam, Shatter or Overcharge and add reaction damage; the reaction and its
  sequence are replicated so both clients can present the same combat event.
- Room types now change the encounter rules on both clients: events and supply rooms slow threat pulses
  and amplify coordinated Resonance (+50% / +25%), elite rooms increase health and pulse tempo, and the
  boss arena keeps elemental resistances. These modifiers are pure functions of the shared room type,
  so procedural branches create different tactical routes without extra bandwidth.
- Each room type also exposes one shared objective label and deterministic reward value. The score uses
  those values directly (elite +120, event +80, supply +60, boss +520), so route choice has a readable
  payoff and later onboarding can surface the same data without another network message.
- Threat pulses are host-authored and replicated with an element, timer and sequence. The HUD flashes
  an element-specific warning and reports whether the latest 20 Hz snapshot is live or stale, making
  packet loss visible during a mobile playtest.
- The cooperative run now has a shared eight-point team hull. Event and supply rooms are safe,
  standard and elite pulses chip the hull on a cooldown, and boss pulses deal two points. Hull damage
  is applied only by the host and replicated in the snapshot; when it reaches zero the host freezes the
  simulation, increments a failure sequence and both clients show the red `СЕКТОР ПОТЕРЯН` result.
  Failed runs produce a zero-score result and the normal bounded MMR loss, with the same run-id
  idempotency rules as a successful completion.
- The host now publishes a run-completion flag and monotonic completion sequence in the snapshot. After
  the final boss is cleared and the room settle timer elapses, both players stop the simulation on the
  same frame and see the shared `СЕКТОР ОЧИЩЕН` result state; the Editor preview follows the same rule.
- Party creation publishes a member-visible `run_id` alongside the procedural seed. Both clients derive
  the same deterministic sector score from that layout; completion applies each player's local MMR delta
  while submitting the shared run id to Firebase, so reconnects or duplicate completion callbacks cannot
  award the same run twice. A compact FNV-1a `lastRunHash` (run id + seed/layout signature + score) is
  stored with the record for diagnostics and later server-side verification. The completion panel shows
  the common score and the resulting personal MMR.
- `MultiplayerSessionController` listens to the service session state and automatically calls
  `ISession.ReconnectAsync()` after a disconnect with backoff. The party screen exposes the retry
  counter and keeps leaving the party available while recovery is in progress.
- Firebase progress writes now carry a unique `runId` in `lastRunId`; a retried upload of the same
  run is acknowledged and cleared without applying a second result.
- Firebase bootstrap, anonymous authentication, personal progress and both leaderboards now recover
  automatically after an offline start or connection loss. Pending score/MMR writes keep their local
  eight-second retry while connection recovery uses a separate twenty-second cadence.
- In the Editor, the unlinked-cloud fallback exposes `ПРЕВЬЮ 2 ПИЛОТА`: it runs the same two-ship
  presentation locally with a deterministic seed, independent fire cadence, route map, threat health
  bar and Resonance feedback in the touch-zone HUD, so UI and input can be reviewed before Relay
  credentials are configured.
- The cooperative arena now has one host-authored morphing trajectory. It holds a circle, ellipse and
  Gerono figure-eight in sequence, blends between them without teleporting either ship, and replicates
  trajectory time in the authoritative snapshot. The guest predicts between 20 Hz updates and corrects
  smoothly; reconnecting restores the host's current form. The HUD announces the next change and live
  morph percentage, while the local two-pilot preview uses the identical path implementation. The
  preview is exposed as a dedicated button in Editor, Windows Development and Android Development
  builds even when the Unity Cloud project is linked, enabling repeatable no-USB desktop QA.
- Cooperative runs now begin on a stable Gerono figure-eight rather than the solo-sized circle. The
  co-op radius is `4.25` versus the solo radius `3.2`, threat approach distance scales with that arena,
  and the orthographic camera smoothly follows the current path extent. The figure-eight therefore
  uses almost the full safe portrait width while the later ellipse automatically receives extra room.
- The same procedural presentation now powers a real offline `Solo Expedition` mode. It creates a new
  seed and idempotent run id, keeps only the selected local ship, removes bot fire/collisions/automatic
  same-element Resonance, uses a five-point hull and persists the final score/MMR through the normal
  Firebase pipeline. The deterministic two-pilot local preview remains QA-only and never submits results.
- Two ships now collide against their actual morphing-trajectory positions. The host owns the overlap
  test, applies a wide angular bounce to both pilots and replicates a monotonic collision sequence and
  impact point; both clients play the same pixel burst, camera shake, haptic pulse and rubbery bump sound.
- Room presentation now derives a visual signature from the shared run seed, room index and room type.
  Start, combat, elite, event, supply and boss nodes produce different tinted washes and deterministic
  geometric motifs without adding snapshot bandwidth. The cooperative HUD was compressed into a top
  status strip, small route line and side-by-side threat/team bars so the arena remains unobstructed.
- Snapshot `v8` adds an authoritative unstable relay core to selected combat rooms and every elite/boss
  room. Ships physically bump it; auto-fire rays passing nearby push and elementally charge it up to
  three levels. A fast/charged hit deals up to 10 threat damage. A surviving boss returns the magenta
  core toward the nearest pilot, where contact removes one team-hull point before making it safe again.
  Position, velocity, charge, element, dangerous state and a monotonic impact event are all replicated
  in the existing 20 Hz snapshot. Solo Expedition mirrors the same rule functions locally, including
  its trail, compact charge HUD and mobile-safe elliptical physics boundary.
- Unity Cloud Project `529475cc-bb57-448b-af13-ca33ed2f5e39` is linked to organization
  `unity_72b64e5f7a72b07ad367`; the Relay party screen is now available in the Editor and Android build.
  Keep the project linked when opening the repository on another workstation, then sign in to the same
  Unity organization before creating or joining a party.
