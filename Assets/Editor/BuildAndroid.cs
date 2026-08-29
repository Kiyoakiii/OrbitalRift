#if UNITY_EDITOR
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

        private static void LogSuccess(BuildReport report, string outputPath)
        {
            var absolutePath = Path.GetFullPath(outputPath);
            Debug.Log($"Orbital Rift build succeeded: {absolutePath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalTime.TotalSeconds:0.0}s)");
        }
    }
}
#endif
