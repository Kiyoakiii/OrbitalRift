using System;
using System.Globalization;
using System.Runtime.InteropServices;
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
        private static bool loggedNativeFailure;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static bool windowsCaptureRequested;

        [DllImport("OrbitalRiftAudioLoopback", CallingConvention = CallingConvention.Cdecl)]
        private static extern int OR_StartAudioLoopback();

        [DllImport("OrbitalRiftAudioLoopback", CallingConvention = CallingConvention.Cdecl)]
        private static extern void OR_StopAudioLoopback();

        [DllImport("OrbitalRiftAudioLoopback", CallingConvention = CallingConvention.Cdecl)]
        private static extern int OR_PollAudioFrame(out float energy, out float bass, out float mid, out float treble, out float beat);
#endif

        public static bool IsAndroidCaptureSupported =>
            Application.platform == RuntimePlatform.Android && !Application.isEditor &&
            GetAndroidSdkLevel() >= 29;

        public static bool IsWindowsCaptureSupported
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                return Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
#else
                return false;
#endif
            }
        }

        public static void RequestCapture()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (IsWindowsCaptureSupported)
            {
                try
                {
                    windowsCaptureRequested = OR_StartAudioLoopback() != 0;
                    if (!windowsCaptureRequested) Debug.LogWarning("System audio loopback could not start. No reactive effects will be shown.");
                }
                catch (Exception exception)
                {
                    windowsCaptureRequested = false;
                    LogNativeFailure(exception);
                }
                return;
            }
#endif
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
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (IsWindowsCaptureSupported)
            {
                try { OR_StopAudioLoopback(); }
                catch (Exception exception) { LogNativeFailure(exception); }
                windowsCaptureRequested = false;
                return;
            }
#endif
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

        public static ExternalMusicFrame Poll()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (IsWindowsCaptureSupported && windowsCaptureRequested)
            {
                try
                {
                    var active = OR_PollAudioFrame(out var energy, out var bass, out var mid, out var treble, out var beat) != 0;
                    return new ExternalMusicFrame(active, energy, bass, mid, treble, beat);
                }
                catch (Exception exception)
                {
                    windowsCaptureRequested = false;
                    LogNativeFailure(exception);
                    return default;
                }
            }
#endif
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
            return default;
#endif
        }

        public static string StatusLabel
        {
            get
            {
                if (IsWindowsCaptureSupported)
                {
                    return "ПК: ВЫКЛЮЧИ МУЗЫКУ ИГРЫ, ЗАТЕМ ВКЛЮЧИ ТРЕК";
                }
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
