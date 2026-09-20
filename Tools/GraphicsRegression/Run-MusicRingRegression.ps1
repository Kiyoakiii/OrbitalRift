param(
    [Parameter(Mandatory = $true)][string]$BeforeShader,
    [string]$Unity = 'A:\GameDev\Unity\Editors\6000.3.22f1\Editor\Unity.exe',
    [ValidateSet('d3d11','glcore')][string]$GraphicsApi = 'd3d11'
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qaRoot = Join-Path $env:TEMP ('OrbitalRift-RingQA-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$qaRoot\Assets\Editor", "$qaRoot\Assets\Resources", "$qaRoot\Packages", "$qaRoot\ProjectSettings" | Out-Null
Copy-Item -LiteralPath $BeforeShader -Destination "$qaRoot\Assets\Resources\Before.shader"
Copy-Item -LiteralPath "$project\Assets\Resources\MusicSpaceWaterDistortion.shader" -Destination "$qaRoot\Assets\Resources\After.shader"
Copy-Item -LiteralPath "$project\ProjectSettings\ProjectVersion.txt" -Destination "$qaRoot\ProjectSettings\ProjectVersion.txt"
Copy-Item -LiteralPath "$PSScriptRoot\MusicRingGpuRegression.cs" -Destination "$qaRoot\Assets\Editor\MusicRingGpuRegression.cs"
# Generate the isolated test fixture from the production method, not a second implementation.
$source = Get-Content -LiteralPath "$project\Assets\Scripts\MusicReactiveVisualDirector.cs" -Raw
$method = [regex]::Match($source, '(?s)private static float PeriodicWave\(.*?\n        \}').Value
if (!$method) { throw 'Production PeriodicWave method not found.' }
$fixture = 'using UnityEngine; public static class RingWaveUnderTest { ' + $method.Replace('private static', 'public static') + ' }'
[IO.File]::WriteAllText("$qaRoot\Assets\Editor\RingWaveUnderTest.cs", $fixture)
[IO.File]::WriteAllText("$qaRoot\Packages\manifest.json", '{"dependencies":{"com.unity.modules.imageconversion":"1.0.0","com.unity.modules.imgui":"1.0.0"}}')
$arguments = "-batchmode -force-$GraphicsApi -projectPath `"$qaRoot`" -executeMethod MusicRingGpuRegression.Run -logFile `"$qaRoot\qa.log`""
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WorkingDirectory $qaRoot -WindowStyle Hidden -PassThru
Write-Output "QA project: $qaRoot"
Write-Output "Unity PID: $($process.Id)"
Write-Output 'Read qa.log and Results/report.txt on completion. Exit code 0 means the GPU comparison and geometry regression passed.'
