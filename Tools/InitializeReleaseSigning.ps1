[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KeystorePath,
    [string]$Alias = 'orbitalrift-upload',
    [string]$SigningConfig = "$env:LOCALAPPDATA\OrbitalRift\upload-signing.credential.xml",
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$KeystorePath = [System.IO.Path]::GetFullPath($KeystorePath)

if (-not (Test-Path -LiteralPath $KeystorePath)) {
    throw "Keystore not found: $KeystorePath"
}
if ((Test-Path -LiteralPath $SigningConfig) -and -not $Force) {
    throw "Signing configuration already exists: $SigningConfig. Use -Force only when you intend to replace it."
}

$securePassword = Read-Host 'Enter the upload-keystore password' -AsSecureString
$credential = [System.Management.Automation.PSCredential]::new($Alias, $securePassword)
$signing = [pscustomobject]@{
    KeystorePath = $KeystorePath
    Alias = $Alias
    Credential = $credential
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $SigningConfig) | Out-Null
$signing | Export-Clixml -LiteralPath $SigningConfig
Write-Host "Local signing configuration created for the current Windows user: $SigningConfig"
