# Music ring seam regression

Validated 2026-09-07 in Unity 6000.3.22f1, NVIDIA RTX 3060 Laptop GPU.

The old full-screen shader reproduced the user's horizontal discontinuity in a static render
texture, without display presentation, sprites or a LineRenderer. This isolates the reproduced
defect from VSync, cloud quads, and implicit line closure.

`atan2` wraps from +PI to -PI. Linear-angle noise, fractional angular sine frequencies, and the
colour phase `sin(angle * 1.2 + phase)` did not agree across that wrap. Both plasma rims and the
fill therefore changed radius and colour abruptly. Noise now follows unit-circle coordinates;
wave frequencies blend integer harmonics; the colour uses a whole harmonic. CPU ring wobble
uses the same periodic construction and native loop joins. Signed squared shader distances
also use multiplication instead of pow to avoid backend-dependent non-finite output.

## Evidence

- `Evidence/seam-comparison.png`: old shader left, fixed shader right, identical inputs. RGB
  brightness multiplied by four in BOTH crops to reveal the seam; no spatial filtering.
- D3D11 and OpenGL Core: 24 GPU-rendered inputs each (8 time/music states at 1024x1024,
  720x1280, 1280x720). All outputs finite. Maximum adjacent-row RGB difference across the
  scanned left half: 0.04828069 before, 0.004838467 after. In the comparison image's frame:
  0.04828069 before, 0.001498286 after. Remaining differences include smooth background and
  particle gradients, not only the ring.
- 1000 continuity checks on the production CPU PeriodicWave method at both 0/2PI and -PI/PI
  passed: max value error 0.000004589558, max numerical tangent error 0.001862526.
- Main project's C# response-file compilation passed. These are isolated graphics tests;
  Android hardware and a newly built APK were not run in this change.

## Repeat

From the project root:

```powershell
Tools\GraphicsRegression\Run-MusicRingRegression.ps1 -BeforeShader Tools\GraphicsRegression\Evidence\Before.shader.txt -GraphicsApi d3d11
Tools\GraphicsRegression\Run-MusicRingRegression.ps1 -BeforeShader Tools\GraphicsRegression\Evidence\Before.shader.txt -GraphicsApi glcore
```

Each invocation starts a separate minimal Unity project and prints its temp path and process
ID. It does not open or modify the user's scene. Once that Unity process exits, inspect
`qa.log` and `Results/report.txt` in the printed directory. Exit 0 means all assertions passed.
GPU rendering must stay enabled (do not pass `-nographics`). The previous shader fixture is
outside Assets and cannot ship in a game build.

## Flight presentation regression

Run `Tools\GraphicsRegression\Run-TravelPresentationRegression.ps1` from the project root.
The runner extracts the production `UpdateSpaceTravel` method into an isolated fixture.
It tests classic cruise, boss arrival/victory, expedition combat/shop, network-state combat,
pause and run failure/exit. This is state-logic coverage, not a two-device networking test.
It also renders 24 jump/music/aspect combinations on D3D11 and checks finite, visible output.
`Results` contains portrait/landscape cruise and full-jump PNGs; `qa.log` contains assertions.

Tuning lives in `Assets/Scripts/StarStreamSettings.cs`: `CombatTravelSpeed` (0 stops white
stars), `JumpTravelSpeed`, `JumpDuration`, and the four `FarTrail`/`NearTrail` constants.
Purple shield pickup speed is deliberately unchanged. `OrbitSettings.ShowTrajectory` only
hides the line; the ship movement path and collision logic remain intact.
