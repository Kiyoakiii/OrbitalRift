# Orbital Rift — Android release signing

Upload signing is deliberately kept outside the Git repository. The project reads it only for the release build process and restores the previous Unity Player Settings afterwards.

## This workstation

- Primary keystore: `A:\GameDev\Keystores\OrbitalRift-upload.jks`.
- Alias: `orbitalrift-upload`.
- DPAPI-protected local build configuration: `%LOCALAPPDATA%\OrbitalRift\upload-signing.credential.xml`.
- Human-readable recovery copy: `%LOCALAPPDATA%\OrbitalRift\upload-signing-recovery.txt`.
- Second local keystore copy: `%LOCALAPPDATA%\OrbitalRift\Backup\OrbitalRift-upload.jks`.

The three sensitive local files have inherited permissions disabled and grant access only to the current Windows user. None of them is stored in Git.

The release certificate uses RSA-4096 with SHA-256 and is valid until `2054-01-15`. Its SHA-256 fingerprint is:

```text
E7:F9:C5:93:F8:47:8A:BC:F3:11:23:56:42:73:23:6D:AE:F9:2C:D3:7F:B5:7D:9B:B4:FF:92:DE:E6:FA:A4:F6
```

## Build a signed AAB

Close the Unity Editor, then run from the project root:

```powershell
& .\Tools\BuildReleaseAab.ps1
```

The script decrypts the password for the current Windows user, exposes it only to the Unity process, clears the four environment variables in `finally`, and writes `Builds\OrbitalRift-release.aab`.

## Move signing to another Windows PC

1. Copy `OrbitalRift-upload.jks` through a secure channel.
2. Put it outside the cloned repository.
3. From the project root run:

```powershell
& .\Tools\InitializeReleaseSigning.ps1 `
  -KeystorePath '<absolute-path-to-OrbitalRift-upload.jks>' `
  -Alias 'orbitalrift-upload'
```

4. Enter the password from the recovery record when prompted. PowerShell stores a new DPAPI configuration tied to that Windows user.
5. Run `& .\Tools\BuildReleaseAab.ps1`, passing a different `-UnityPath` when Unity is installed elsewhere.

## Required backup

The two copies currently remain on this computer and are not an offline backup. Store the keystore plus its alias/password in a trusted password manager and on one encrypted external or cloud backup. Losing this key can prevent publishing updates unless Google Play upload-key reset is available for the app.
