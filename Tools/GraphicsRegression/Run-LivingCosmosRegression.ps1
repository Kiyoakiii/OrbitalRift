param([string]$Unity = 'A:\GameDev\Unity\Editors\6000.3.22f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qaRoot = Join-Path $env:TEMP ('OrbitalRift-LivingQA-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$qaRoot\Assets\Editor", "$qaRoot\Assets\Resources", "$qaRoot\Assets\Scripts", "$qaRoot\Packages", "$qaRoot\ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$project\ProjectSettings\ProjectVersion.txt" -Destination "$qaRoot\ProjectSettings\ProjectVersion.txt"
foreach ($file in @('LivingCosmosRunState.cs','LivingCosmosCheckpoint.cs','SectorGenerator.cs','MmrSettings.cs','StarStreamSettings.cs','OrbitSettings.cs','CoopTrajectorySettings.cs','MusicSpaceDistortion.cs','SpaceVisualProfile.cs','SpaceVisualProfile.cs.meta','UI\ExpeditionModeChoiceView.cs','UI\TempoRewardChoiceView.cs','UI\CosmosRouteMapView.cs','UI\CosmosRouteGraphic.cs')) {
    Copy-Item -LiteralPath "$project\Assets\Scripts\$file" -Destination "$qaRoot\Assets\Scripts\"
}
foreach ($file in @('MusicSpaceWaterDistortion.shader','SpaceBlueFrontier.asset','SpaceAmberFront.asset')) {
    Copy-Item -LiteralPath "$project\Assets\Resources\$file" -Destination "$qaRoot\Assets\Resources\$file"
}
Copy-Item -LiteralPath "$project\Assets\Resources\Fonts" -Destination "$qaRoot\Assets\Resources\Fonts" -Recurse
Copy-Item -LiteralPath "$project\Assets\TextMesh Pro" -Destination "$qaRoot\Assets\TextMesh Pro" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot\LivingCosmosRegression.cs" -Destination "$qaRoot\Assets\Editor\LivingCosmosRegression.cs"
Copy-Item -LiteralPath "$PSScriptRoot\TempoRulesRegression.cs" -Destination "$qaRoot\Assets\Scripts\TempoRulesRegression.cs"
Copy-Item -LiteralPath "$PSScriptRoot\PairedLensRulesRegression.cs" -Destination "$qaRoot\Assets\Scripts\PairedLensRulesRegression.cs"
Copy-Item -LiteralPath "$project\Assets\Scripts\TempoRewardRules.cs" -Destination "$qaRoot\Assets\Scripts\TempoRewardRules.cs"
Copy-Item -LiteralPath "$project\Assets\Scripts\PairedLensRules.cs" -Destination "$qaRoot\Assets\Scripts\PairedLensRules.cs"
$source = Get-Content -LiteralPath "$project\Assets\Scripts\GameManager.cs" -Raw
$outcome = [regex]::Match($source, '(?s)private void RecordCoopOutcome\(.*?\n        \}').Value
if (!$outcome) { throw 'Production outcome method not found' }
$fixture = @'
using System;
using UnityEngine;
using OrbitalRift;
public sealed class OutcomeUnderTest {
 public bool LivingCosmosActive=true, coopLocalPreview=true, soloExpeditionPlaying=true, coopResultSubmitted;
 public int coopResultScore, coopResultMmrDelta, lastMmrDelta, bestScore=999, mmr=1337;
 public float mmrResultTimer;
 public string coopResultFingerprint, coopResultRunId="test", playerNickname="test", currentRunId;
 public int coopPreviewRunSeed=7;
 public LivingCosmosCheckpointStore livingCheckpointStore;
 public FakeSessions multiplayerSessions;
 public FakeFirebase firebaseScores=new FakeFirebase();
 static int ComputeCoopScore(SectorLayout layout) => 456;
 int ComputeLivingScore() => 456;
 static string ComputeCoopResultFingerprint(SectorLayout layout,string run,int score) => "fixture";
'@
$fixture += $outcome.Replace('private void','public void').Replace('PlayerPrefs.','ForbiddenPrefs.') + @'
}
public sealed class FakeSessions { public int RunSeed; }
public sealed class FakeFirebase { public void SubmitProgress(int score,int mmr,string name,string run,string hash) { throw new Exception("Unranked Firebase write"); } }
public static class ForbiddenPrefs {
 public static void SetInt(string key,int value) { throw new Exception("Unranked profile write"); }
 public static void Save() { throw new Exception("Unranked profile save"); }
}
'@
[IO.File]::WriteAllText("$qaRoot\Assets\Scripts\OutcomeUnderTest.cs", $fixture)
[IO.File]::WriteAllText("$qaRoot\Assets\Scripts\FixtureRules.cs", 'namespace OrbitalRift { public static class CoopRoomRules { public const float RoomClearDelay=1.8f; } }')
[IO.File]::WriteAllText("$qaRoot\Packages\manifest.json", '{"dependencies":{"com.unity.ugui":"2.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0"}}')
$arguments = "-batchmode -force-d3d11 -projectPath `"$qaRoot`" -executeMethod LivingCosmosRegression.Run -logFile `"$qaRoot\qa.log`""
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WorkingDirectory $qaRoot -WindowStyle Hidden -PassThru
Write-Output "QA project: $qaRoot"
Write-Output "Unity PID: $($process.Id)"
