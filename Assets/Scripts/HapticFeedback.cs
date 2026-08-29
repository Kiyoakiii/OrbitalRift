using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Короткая необязательная виброотдача для важных игровых событий.</summary>
    public static class HapticFeedback
    {
        private const string EnabledKey = "orbital_rift_haptics_enabled";

        public static bool Enabled { get; private set; } = true;

        public static void Load()
        {
            Enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;
        }

        public static bool Toggle()
        {
            Enabled = !Enabled;
            PlayerPrefs.SetInt(EnabledKey, Enabled ? 1 : 0);
            PlayerPrefs.Save();
            if (Enabled) Pulse(18);
            return Enabled;
        }

        public static void Pulse(long milliseconds)
        {
            if (!Enabled || milliseconds <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 26)
                        {
                            using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, -1))
                                vibrator.Call("vibrate", effect);
                        }
                        else vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Haptic feedback is unavailable: " + exception.Message);
            }
#endif
        }
    }
}
