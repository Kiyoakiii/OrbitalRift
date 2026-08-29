#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OrbitalRift
{
    public static class BuildAndroid
    {
        private const string BootScene = "Assets/Scenes/Boot.unity";
        private const string KeystorePathEnvironment = "ORBITALRIFT_KEYSTORE_PATH";
        private const string KeystorePasswordEnvironment = "ORBITALRIFT_KEYSTORE_PASS";
        private const string KeyAliasEnvironment = "ORBITALRIFT_KEYALIAS_NAME";
        private const string KeyAliasPasswordEnvironment = "ORBITALRIFT_KEYALIAS_PASS";

        [MenuItem("Orbital Rift/Build Android Debug APK")]
        public static void BuildDebugApk()
        {
            BuildAndroidPlayer("OrbitalRift-debug.apk", false, BuildOptions.Development | BuildOptions.AllowDebugging, false);
        }

        [MenuItem("Orbital Rift/Build Android Release AAB")]
        public static void BuildReleaseAab()
        {
            BuildAndroidPlayer("OrbitalRift-release.aab", true, BuildOptions.None, true);
        }

        [MenuItem("Orbital Rift/Build Windows Development")]
        public static void BuildWindows()
        {
            ProductReadinessValidator.ValidateBootScene();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            var outputDirectory = "Builds";
            Directory.CreateDirectory(outputDirectory);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity" },
                locationPathName = Path.Combine(outputDirectory, "OrbitalRift.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows build failed: " + result.summary.result);
            LogSuccess(result, Path.Combine(outputDirectory, "OrbitalRift.exe"));
        }

        private static void BuildAndroidPlayer(string fileName, bool appBundle, BuildOptions options, bool requireSigning)
        {
            // Keep one explicit, store-safe entry point. A missing entry produces an
            // installable APK with no launcher icon and no Activity to start.
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            var signingOverride = ApplySigningEnvironmentOverride(requireSigning);
            try
            {
                ProductReadinessValidator.ValidateAndroid(requireSigning);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new BuildFailedException("Unity could not switch the active build target to Android.");

                const string outputDirectory = "Builds";
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, fileName);
                var previousAppBundle = EditorUserBuildSettings.buildAppBundle;
                try
                {
                    EditorUserBuildSettings.buildAppBundle = appBundle;
                    var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { BootScene },
                        locationPathName = outputPath,
                        target = BuildTarget.Android,
                        options = options
                    });
                    if (result.summary.result != BuildResult.Succeeded)
                        throw new BuildFailedException("Android build failed: " + result.summary.result);
                    LogSuccess(result, outputPath);
                }
                finally
                {
                    EditorUserBuildSettings.buildAppBundle = previousAppBundle;
                }
            }
            finally
            {
                signingOverride.Restore();
            }
        }

        private static SigningOverride ApplySigningEnvironmentOverride(bool requireSigning)
        {
            var state = new SigningOverride();
            if (!requireSigning) return state;

            var path = Environment.GetEnvironmentVariable(KeystorePathEnvironment);
            var storePassword = Environment.GetEnvironmentVariable(KeystorePasswordEnvironment);
            var alias = Environment.GetEnvironmentVariable(KeyAliasEnvironment);
            var aliasPassword = Environment.GetEnvironmentVariable(KeyAliasPasswordEnvironment);
            var anyProvided = !string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(storePassword) ||
                              !string.IsNullOrWhiteSpace(alias) || !string.IsNullOrWhiteSpace(aliasPassword);
            if (!anyProvided) return state;

            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(storePassword) ||
                string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(aliasPassword))
                throw new BuildFailedException(
                    $"Release signing environment is incomplete. Set {KeystorePathEnvironment}, {KeystorePasswordEnvironment}, " +
                    $"{KeyAliasEnvironment}, and {KeyAliasPasswordEnvironment} together.");

            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new BuildFailedException("Release keystore was not found: " + path);

            state.Capture();
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = path;
            PlayerSettings.Android.keystorePass = storePassword;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPassword;
            Debug.Log("Release signing loaded from local environment for alias: " + alias);
            return state;
        }

        private sealed class SigningOverride
        {
            private bool captured;
            private bool useCustomKeystore;
            private string keystoreName;
            private string keystorePass;
            private string keyaliasName;
            private string keyaliasPass;

            public void Capture()
            {
                captured = true;
                useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
                keystoreName = PlayerSettings.Android.keystoreName;
                keystorePass = PlayerSettings.Android.keystorePass;
                keyaliasName = PlayerSettings.Android.keyaliasName;
                keyaliasPass = PlayerSettings.Android.keyaliasPass;
            }

            public void Restore()
            {
                if (!captured) return;
                PlayerSettings.Android.useCustomKeystore = useCustomKeystore;
                PlayerSettings.Android.keystoreName = keystoreName;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = keyaliasName;
                PlayerSettings.Android.keyaliasPass = keyaliasPass;
            }
        }

        private static void LogSuccess(BuildReport report, string outputPath)
        {
            var absolutePath = Path.GetFullPath(outputPath);
            Debug.Log($"Orbital Rift build succeeded: {absolutePath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalTime.TotalSeconds:0.0}s)");
        }
    }
}
#endif
