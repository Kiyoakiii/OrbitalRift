using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Persistent player-facing audio preferences kept outside GameManager.</summary>
    public static class GameAudioSettings
    {
        private const string MusicKey = "orbital_rift_music_enabled";
        private const string EffectsKey = "orbital_rift_effects_enabled";

        public const float MusicVolume = .42f;
        public const float EffectsVolume = .70f;

        public static bool MusicEnabled { get; private set; } = true;
        public static bool EffectsEnabled { get; private set; } = true;

        public static void Load()
        {
            MusicEnabled = PlayerPrefs.GetInt(MusicKey, 1) != 0;
            EffectsEnabled = PlayerPrefs.GetInt(EffectsKey, 1) != 0;
        }

        public static bool ToggleMusic()
        {
            MusicEnabled = !MusicEnabled;
            Save(MusicKey, MusicEnabled);
            return MusicEnabled;
        }

        public static bool ToggleEffects()
        {
            EffectsEnabled = !EffectsEnabled;
            Save(EffectsKey, EffectsEnabled);
            return EffectsEnabled;
        }

        private static void Save(string key, bool enabled)
        {
            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
