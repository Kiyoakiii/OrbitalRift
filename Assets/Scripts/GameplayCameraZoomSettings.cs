using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Persistent world-only zoom. UI is drawn by Canvas/OnGUI and is not affected.</summary>
    public static class GameplayCameraZoomSettings
    {
        private const string Key = "orbital_rift_gameplay_camera_zoom";
        public static float Default => GameplayCameraZoomSettingsProfile.Current.Default;
        public static float Minimum => GameplayCameraZoomSettingsProfile.Current.Minimum;
        public static float Maximum => GameplayCameraZoomSettingsProfile.Current.Maximum;
        public static float Step => GameplayCameraZoomSettingsProfile.Current.Step;

        public static float Value { get; private set; } = Default;

        public static void Load()
        {
            Value = Mathf.Clamp(PlayerPrefs.GetFloat(Key, Default), Minimum, Maximum);
        }

        public static float Adjust(float direction)
        {
            Value = Mathf.Clamp(Value + Mathf.Sign(direction) * Step, Minimum, Maximum);
            PlayerPrefs.SetFloat(Key, Value);
            PlayerPrefs.Save();
            return Value;
        }

        public static string PercentLabel => Mathf.RoundToInt(Value * 100f) + "%";
    }
}
