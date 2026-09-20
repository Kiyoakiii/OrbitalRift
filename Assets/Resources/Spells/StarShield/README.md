# Star Shield VFX profile

`Profiles/StarShield.asset` controls the purple-star shield pickup and its orbiting presentation.

- `Core`, `Glow`, `Trail` control the look of the pickup and each orbiting star.
- `PickupRadius`, `OrbitRadius`, `OrbitRadiusStep`, `OrbitSpeed` control collection and orbit.
- `ShieldHits` and `MaxShields` control the gameplay capacity.
- `DespawnRadius = 0` means a loose star is released when it leaves the camera view. Set a positive value to also cap its distance from the arena center.

The `ЭГИДА ОРБИТЫ` passive in the ability sandbox is the switch for this mechanic.
