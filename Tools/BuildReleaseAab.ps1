[CmdletBinding()]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$UnityPath = 'A:\GameDev\Unity\Editors\6000.3.22f1\Editor\Unity.exe',
    [string]$SigningConfig = "$env:LOCALAPPDATA\OrbitalRift\upload-signing.credential.xml"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable not found: $UnityPath"
}
if (-not (Test-Path -LiteralPath $SigningConfig)) {
    throw "Local signing configuration not found: $SigningConfig"
}
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
    throw 'Close the Unity Editor before running the batch release build.'
}

$signing = Import-Clixml -LiteralPath $SigningConfig
$plainPassword = $signing.Credential.GetNetworkCredential().Password
if ([string]::IsNullOrWhiteSpace($plainPassword)) {
    throw 'The local signing credential could not be decrypted for this Windows user.'
}
if (-not (Test-Path -LiteralPath $signing.KeystorePath)) {
    throw "Keystore not found: $($signing.KeystorePath)"
}

$env:ORBITALRIFT_KEYSTORE_PATH = $signing.KeystorePath
$env:ORBITALRIFT_KEYSTORE_PASS = $plainPassword
$env:ORBITALRIFT_KEYALIAS_NAME = $signing.Alias
$env:ORBITALRIFT_KEYALIAS_PASS = $plainPassword

$logPath = Join-Path $ProjectPath 'Builds\QA\release-aab-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null

try {
    & $UnityPath -batchmode -nographics -quit `
        -projectPath $ProjectPath `
        -executeMethod OrbitalRift.BuildAndroid.BuildReleaseAab `
        -logFile $logPath

    $deadline = (Get-Date).AddMinutes(15)
    do {
        Start-Sleep -Seconds 2
        $running = Get-Process -Name Unity -ErrorAction SilentlyContinue
    } while ($running -and (Get-Date) -lt $deadline)

    if ($running) {
        throw "Unity release build did not finish within 15 minutes. See $logPath"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Builds\OrbitalRift-release.aab'))) {
        throw "Release AAB was not created. See $logPath"
    }
    if (-not (Select-String -LiteralPath $logPath -SimpleMatch 'Orbital Rift build succeeded' -Quiet)) {
        throw "Unity did not report a successful release build. See $logPath"
    }

    Write-Host "Release AAB created: $(Join-Path $ProjectPath 'Builds\OrbitalRift-release.aab')"
}
finally {
    Remove-Item Env:ORBITALRIFT_KEYSTORE_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:ORBITALRIFT_KEYSTORE_PASS -ErrorAction SilentlyContinue
    Remove-Item Env:ORBITALRIFT_KEYALIAS_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:ORBITALRIFT_KEYALIAS_PASS -ErrorAction SilentlyContinue
    $plainPassword = $null
}
