#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Fast, deterministic checks that run before local and CI builds.</summary>
    public static class ProductReadinessValidator
    {
        private const string BootScene = "Assets/Scenes/Boot.unity";
        private const string GoogleServices = "Assets/google-services.json";
        private const string AndroidGradleTemplate = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string AndroidManifest = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string AndroidLauncherManifest = "Assets/Plugins/Android/LauncherManifest.xml";
        private static readonly string[] RequiredAssets =
        {
            "Assets/Resources/ship.png",
            "Assets/Resources/projectile.png",
            "Assets/Resources/bonus_pickup.png",
            "Assets/Resources/enemy_orange.png",
            "Assets/Resources/enemy_pink_can.png",
            "Assets/Resources/boss_dreadnought.png",
            "Assets/Resources/deep_space_drift.mp3",
            "Assets/Resources/Ranks/rank_navigator.png",
            "Assets/Resources/Ranks/rank_guardian.png",
            "Assets/Resources/Ranks/rank_legend.png",
            "Assets/Resources/Ranks/rank_overlord.png",
            "Assets/Resources/Ranks/rank_divinity.png"
        };

        [MenuItem("Orbital Rift/Validate Product Readiness")]
        public static void ValidateFromMenu()
        {
            ValidateAndroid(false);
            Debug.Log("Orbital Rift validation passed. Debug Android builds are ready.");
        }

        public static void ValidateBootScene()
        {
            if (!File.Exists(BootScene))
                throw new BuildFailedException($"Required scene is missing: {BootScene}");

            var sceneEnabled = false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && scene.path == BootScene) { sceneEnabled = true; break; }
            if (!sceneEnabled)
                throw new BuildFailedException($"Required scene is not enabled in Build Settings: {BootScene}");
        }

        public static void ValidateAndroid(bool requireSigning)
        {
            ValidateBootScene();
            var errors = new List<string>();
            var warnings = new List<string>();

            var identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrWhiteSpace(identifier) || !identifier.Contains("."))
                errors.Add("Set a valid Android application identifier.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                errors.Add("Android scripting backend must be IL2CPP.");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                errors.Add("ARM64 must be enabled for Android.");
            if ((int)PlayerSettings.Android.minSdkVersion < 25)
                errors.Add("Minimum Android API level must be 25 or newer.");
            if (!File.Exists(GoogleServices))
                warnings.Add($"Firebase configuration is missing: {GoogleServices}");
            if (PlayerSettings.Android.bundleVersionCode < 1)
                errors.Add("Android version code must be at least 1.");
            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
                errors.Add("Bundle version cannot be empty.");
            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
                errors.Add("Android must use exactly one Activity application entry point.");

            var launcherManifestText = File.Exists(AndroidLauncherManifest)
                ? File.ReadAllText(AndroidLauncherManifest)
                : string.Empty;
            if (!launcherManifestText.Contains("com.unity3d.player.UnityPlayerActivity") ||
                !launcherManifestText.Contains("android.intent.action.MAIN") ||
                !launcherManifestText.Contains("android.intent.category.LAUNCHER") ||
                !launcherManifestText.Contains("android:exported=\"true\""))
                errors.Add("Android launcher manifest must export UnityPlayerActivity with MAIN/LAUNCHER intent filters.");

            ValidateGameContent(errors, warnings);
            GameplayRulesValidator.Validate(errors);

            if (requireSigning)
            {
                if (!PlayerSettings.Android.useCustomKeystore)
                    errors.Add("Release AAB requires a custom Android keystore.");
                if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName))
                    errors.Add("Release AAB requires a keystore path.");
                if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                    errors.Add("Release AAB requires a key alias.");
            }

            foreach (var warning in warnings) Debug.LogWarning("Product readiness: " + warning);
            if (errors.Count > 0)
                throw new BuildFailedException("Product readiness validation failed:\n- " + string.Join("\n- ", errors));

            Debug.Log($"Android validation passed: {identifier}, version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), min API {(int)PlayerSettings.Android.minSdkVersion}, ARM64 + IL2CPP.");
        }

        private static void ValidateGameContent(List<string> errors, List<string> warnings)
        {
            foreach (var assetPath in RequiredAssets)
                if (!File.Exists(assetPath)) errors.Add("Required game asset is missing: " + assetPath);

            var navigator = MmrSettings.NavigatorThreshold;
            var guardian = MmrSettings.GuardianThreshold;
            var legend = MmrSettings.LegendThreshold;
            var overlord = MmrSettings.OverlordThreshold;
            var divinity = MmrSettings.DivinityThreshold;
            var startingMmr = MmrSettings.StartingMmr;
            var minimumMmr = MmrSettings.MinimumMmr;
            var minimumGain = MmrSettings.MinimumGain;
            var maximumGain = MmrSettings.MaximumGain;
            var minimumLoss = MmrSettings.MinimumLoss;
            var maximumLoss = MmrSettings.MaximumLoss;
            if (!(navigator < guardian && guardian < legend && legend < overlord && overlord < divinity))
                errors.Add("MMR rank thresholds must be strictly increasing.");
            if (startingMmr < minimumMmr || minimumGain < 1 || maximumGain < minimumGain ||
                minimumLoss < 1 || maximumLoss < minimumLoss)
                errors.Add("MMR progression settings are invalid.");
            if (guardian != 1000 || legend != 2000 || overlord != 3000 || divinity != 4000)
                errors.Add("The current rank design requires 1000-MMR rank steps and Divinity at 4000 MMR.");
            if (maximumGain != 150 || maximumLoss != 150)
                errors.Add("Per-run MMR gain and loss caps must both remain at 150.");

            var alphaMin = StarStreamSettings.StreamAlphaMin;
            var alphaMax = StarStreamSettings.StreamAlphaMax;
            var purpleChance = StarStreamSettings.PurpleChance;
            if (alphaMin < 0f || alphaMax > 1f || alphaMin > alphaMax)
                errors.Add("Star stream alpha range must stay between 0 and 1.");
            if (purpleChance < 0f || purpleChance > 1f)
                errors.Add("Purple star probability must stay between 0 and 1.");

            var analyticsIncluded = File.Exists(AndroidGradleTemplate) &&
                File.ReadAllText(AndroidGradleTemplate).Contains("firebase-analytics");
            var manifestText = File.Exists(AndroidManifest) ? File.ReadAllText(AndroidManifest) : string.Empty;
            var advertisingIdsRemoved =
                manifestText.Contains("com.google.android.gms.permission.AD_ID\" tools:node=\"remove\"") &&
                manifestText.Contains("android.permission.ACCESS_ADSERVICES_AD_ID\" tools:node=\"remove\"") &&
                manifestText.Contains("android.permission.ACCESS_ADSERVICES_ATTRIBUTION\" tools:node=\"remove\"") &&
                manifestText.Contains("com.google.android.finsky.permission.BIND_GET_INSTALL_REFERRER_SERVICE\" tools:node=\"remove\"");
            if (analyticsIncluded && !advertisingIdsRemoved)
                warnings.Add("Firebase Auth currently brings firebase-analytics into the APK. Review Play Data safety and AD_ID usage before production release.");
        }
    }

    /// <summary>Fails CI early instead of spending minutes on a broken player build.</summary>
    public sealed class ProductReadinessBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ProductReadinessValidator.ValidateBootScene();
            if (report.summary.platform == BuildTarget.Android)
                ProductReadinessValidator.ValidateAndroid(EditorUserBuildSettings.buildAppBundle && !EditorUserBuildSettings.development);
        }
    }
}
#endif
