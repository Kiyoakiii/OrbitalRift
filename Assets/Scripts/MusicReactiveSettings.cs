using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Persistent preference for the external-music visualizer. The visualizer is intentionally
    /// independent from SFX: a player can keep combat sounds while replacing only the soundtrack.
    /// </summary>
    public static class MusicReactiveSettings
    {
        private const string EnabledKey = "orbital_rift_external_music_visuals_enabled";

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
            return Enabled;
        }
    }
}
