#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace OrbitalRift
{
    public static class BuildAndroid
    {
        [MenuItem("Orbital Rift/Build Android Debug APK")]
        public static void BuildDebugApk()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            var outputDirectory = "Builds";
            Directory.CreateDirectory(outputDirectory);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity" },
                locationPathName = Path.Combine(outputDirectory, "OrbitalRift-debug.apk"),
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Android build failed: " + result.summary.result);
        }

        [MenuItem("Orbital Rift/Build Windows Development")]
        public static void BuildWindows()
        {
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
                throw new System.Exception("Windows build failed: " + result.summary.result);
        }
    }
}
#endif
