using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Сохраняемые accessibility-настройки визуальных эффектов.</summary>
    public static class GameVisualSettings
    {
        private const string ScreenShakeKey = "orbital_rift_screen_shake_enabled";

        public static bool ScreenShakeEnabled { get; private set; } = true;

        public static void Load()
        {
            ScreenShakeEnabled = PlayerPrefs.GetInt(ScreenShakeKey, 1) != 0;
        }

        public static bool ToggleScreenShake()
        {
            ScreenShakeEnabled = !ScreenShakeEnabled;
            PlayerPrefs.SetInt(ScreenShakeKey, ScreenShakeEnabled ? 1 : 0);
            PlayerPrefs.Save();
            return ScreenShakeEnabled;
        }
    }
}
