using System;
using System.Globalization;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Small, allocation-free presentation frame produced by the Android capture plug-in.</summary>
    public readonly struct ExternalMusicFrame
    {
        public readonly bool Capturing;
        public readonly bool HasSignal;
        public readonly float Energy;
        public readonly float Bass;
        public readonly float Mid;
        public readonly float Treble;
        public readonly float Beat;

        public ExternalMusicFrame(bool capturing, float energy, float bass, float mid, float treble, float beat)
        {
            Capturing = capturing;
            Energy = Mathf.Clamp01(energy);
            Bass = Mathf.Clamp01(bass);
            Mid = Mathf.Clamp01(mid);
            Treble = Mathf.Clamp01(treble);
            Beat = Mathf.Clamp01(beat);
            // Quiet ambient tracks still deserve their distant rays and slow starfield.
            HasSignal = capturing && Energy > .003f;
        }
    }

    /// <summary>
    /// Android bridge for AudioPlaybackCapture. It never exposes, saves, or transmits samples:
    /// native code reduces them to five normalized values before C# reads them.
    /// </summary>
    public static class ExternalMusicAudioBridge
    {
        private const string BridgeClassName = "com.orbitalrift.musicreactive.ExternalAudioCapture";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static float editorTime;
        private static bool loggedNativeFailure;

        public static bool IsAndroidCaptureSupported =>
            Application.platform == RuntimePlatform.Android && !Application.isEditor &&
            GetAndroidSdkLevel() >= 29;

        public static void RequestCapture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAndroidCaptureSupported) return;
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(BridgeClassName);
                bridge.CallStatic("requestCapture", activity);
            }
            catch (Exception exception)
            {
                LogNativeFailure(exception);
            }
#endif
        }

        public static void StopCapture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(BridgeClassName);
                bridge.CallStatic("stopCapture", activity);
            }
            catch (Exception exception)
            {
                LogNativeFailure(exception);
            }
#endif
        }

        public static ExternalMusicFrame Poll(bool useEditorPreview)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAndroidCaptureSupported) return default;
            try
            {
                using var bridge = new AndroidJavaClass(BridgeClassName);
                return Parse(bridge.CallStatic<string>("pollFrame"));
            }
            catch (Exception exception)
            {
                LogNativeFailure(exception);
                return default;
            }
#else
            if (!useEditorPreview) return default;
            editorTime += Time.unscaledDeltaTime;
            // Editor-only synthetic frame: lets us author and test the visual language without
            // pretending to have captured the user's music on a desktop.
            var phrase = .5f + .5f * Mathf.Sin(editorTime * .46f);
            var beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(editorTime * 2.7f)), 18f);
            var bass = Mathf.Clamp01(.34f + phrase * .38f + beat * .28f);
            var treble = Mathf.Clamp01(.20f + .24f * (.5f + .5f * Mathf.Sin(editorTime * 1.21f)) + beat * .34f);
            var mid = Mathf.Clamp01(.30f + phrase * .32f);
            return new ExternalMusicFrame(true, .22f + phrase * .45f + beat * .18f, bass, mid, treble, beat);
#endif
        }

        public static string StatusLabel
        {
            get
            {
                if (Application.isEditor) return "ПРЕВЬЮ В РЕДАКТОРЕ: ПО ВЫКЛЮЧЕНИЮ МУЗЫКИ";
                if (Application.platform != RuntimePlatform.Android) return "ДОСТУПНО В ANDROID-СБОРКЕ";
                if (GetAndroidSdkLevel() < 29) return "НУЖЕН ANDROID 10 ИЛИ НОВЕЕ";
                return "ВЫКЛЮЧИ МУЗЫКУ ИГРЫ — ANDROID ПОПРОСИТ РАЗРЕШЕНИЕ";
            }
        }

        private static ExternalMusicFrame Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return default;
            var parts = value.Split('|');
            if (parts.Length != 6 || !int.TryParse(parts[0], NumberStyles.Integer, Invariant, out var active)) return default;
            return new ExternalMusicFrame(active != 0,
                ParseUnit(parts[1]), ParseUnit(parts[2]), ParseUnit(parts[3]), ParseUnit(parts[4]), ParseUnit(parts[5]));
        }

        private static float ParseUnit(string value)
        {
            return float.TryParse(value, NumberStyles.Float, Invariant, out var result) ? Mathf.Clamp01(result) : 0f;
        }

        private static int GetAndroidSdkLevel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT");
            }
            catch (Exception exception)
            {
                LogNativeFailure(exception);
            }
#endif
            return 0;
        }

        private static void LogNativeFailure(Exception exception)
        {
            if (loggedNativeFailure) return;
            loggedNativeFailure = true;
            Debug.LogWarning("External music visualizer is unavailable: " + exception.Message);
        }
    }
}
