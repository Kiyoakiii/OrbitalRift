# Orbital Rift Multiplayer 2.0

## Product pillar

Two phone players explore a seeded sector together. The replayable core is **Resonance**:
the ships, their elemental weapons and the environment follow one shared reaction system.
Players discover useful combinations instead of memorizing one scripted solution per room.

## Non-negotiable architecture

- Two players per session for the first release.
- Client-hosted session through Unity Multiplayer Services and Relay.
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
  by the guest. Remote input automatically returns to neutral if packets stop arriving. The same
  snapshot carries authoritative shot sequence counters, the current procedural room index and the
  active threat's angle, radius, health, room kind and defeat sequence; the host advances the route
  every eight seconds only after the threat is cleared, until the generated boss room.
- Host combat applies ship-element resistances and a short Resonance window. Coordinated pairs resolve
  Ignition, Cryotoxin, Steam, Shatter or Overcharge and add reaction damage; the reaction and its
  sequence are replicated so both clients can present the same combat event.
- Threat pulses are host-authored and replicated with an element, timer and sequence. The HUD flashes
  an element-specific warning and reports whether the latest 20 Hz snapshot is live or stale, making
  packet loss visible during a mobile playtest.
- `MultiplayerSessionController` listens to the service session state and automatically calls
  `ISession.ReconnectAsync()` after a disconnect with backoff. The party screen exposes the retry
  counter and keeps leaving the party available while recovery is in progress.
- Firebase progress writes now carry a unique `runId` in `lastRunId`; a retried upload of the same
  run is acknowledged and cleared without applying a second result.
- In the Editor, the unlinked-cloud fallback exposes `ПРЕВЬЮ 2 ПИЛОТА`: it runs the same two-ship
  presentation locally with a deterministic seed, independent fire cadence, route map, threat health
  bar and Resonance feedback in the touch-zone HUD, so UI and input can be reviewed before Relay
  credentials are configured.
- The Unity Cloud Project is not linked yet; cloud calls fail with a user-facing diagnostic
  while the solo game continues to work normally.
