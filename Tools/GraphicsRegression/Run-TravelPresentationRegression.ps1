param([string]$Unity = 'A:\GameDev\Unity\Editors\6000.3.22f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qaRoot = Join-Path $env:TEMP ('OrbitalRift-TravelQA-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$qaRoot\Assets\Editor", "$qaRoot\Assets\Resources", "$qaRoot\Packages", "$qaRoot\ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$project\Assets\Resources\MusicSpaceWaterDistortion.shader" -Destination "$qaRoot\Assets\Resources\After.shader"
Copy-Item -LiteralPath "$project\ProjectSettings\ProjectVersion.txt" -Destination "$qaRoot\ProjectSettings\ProjectVersion.txt"
Copy-Item -LiteralPath "$PSScriptRoot\TravelPresentationRegression.cs" -Destination "$qaRoot\Assets\Editor\TravelPresentationRegression.cs"
foreach ($file in @('StarStreamSettings.cs','OrbitSettings.cs','CoopTrajectorySettings.cs','LivingCosmosRunState.cs','SectorGenerator.cs')) {
    Copy-Item -LiteralPath "$project\Assets\Scripts\$file" -Destination "$qaRoot\Assets\Editor\$file"
}
$source = Get-Content -LiteralPath "$project\Assets\Scripts\GameManager.cs" -Raw
$method = [regex]::Match($source, '(?s)private void UpdateSpaceTravel\(.*?\n        \}').Value
if (!$method) { throw 'Production UpdateSpaceTravel method not found.' }
$fixture = @'
using UnityEngine;
using OrbitalRift;
namespace OrbitalRift { public static class CoopRoomRules { public const float RoomClearDelay=1.8f; } }
public sealed class SimulationUnderTest {
 public bool RunCompleted, RunFailed;
 public byte CoopEnemyKind;
 public float CoopEnemyHealth, TrajectoryTimeSeconds;
}
public sealed class DirectorUnderTest {
 public float jump; public bool active;
 public void SetSpaceTravel(float speed, float jump, bool active, bool paused, float reach) { this.jump=jump; this.active=active; }
 public void SetLivingRegion(bool active, int region, int next, float blend) { }
}
public sealed class TravelUnderTest {
 public LivingCosmosRunState livingCosmos;
 public bool LivingCosmosActive => coopPlaying && livingCosmos != null;
 public bool LivingRouteChoice => LivingCosmosActive && livingCosmos.Phase==LivingEncounterPhase.RouteChoice;
 public bool playing, showResults, defensePlaying, defenseRunOver, coopPlaying, coopLocalPreview, coopPreviewCompleted, coopPreviewFailed, paused, bossAlive;
 public byte coopPreviewEnemyKind;
 public float coopPreviewEnemyHealth, coopPreviewTrajectoryTime;
 public float spaceTravelSpeed=1f, backgroundTravelTime, jumpTimer;
 public bool wasSpaceRun, wasSpaceCombat;
 public SimulationUnderTest coopSimulation;
 public DirectorUnderTest musicReactiveVisuals = new DirectorUnderTest();
 private object ActiveBoss() { return bossAlive ? this : null; }
'@
$fixture += $method.Replace('private void','public void') + ' }'
[IO.File]::WriteAllText("$qaRoot\Assets\Editor\TravelUnderTest.cs", $fixture)
[IO.File]::WriteAllText("$qaRoot\Packages\manifest.json", '{"dependencies":{"com.unity.modules.imageconversion":"1.0.0","com.unity.modules.imgui":"1.0.0"}}')
$arguments = "-batchmode -force-d3d11 -projectPath `"$qaRoot`" -executeMethod TravelPresentationRegression.Run -logFile `"$qaRoot\qa.log`""
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WorkingDirectory $qaRoot -WindowStyle Hidden -PassThru
Write-Output "QA project: $qaRoot"
Write-Output "Unity PID: $($process.Id)"
