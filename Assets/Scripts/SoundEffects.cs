using UnityEngine;

namespace OrbitalRift
{
    // Короткие синтезированные эффекты: не требуют сторонних аудиофайлов.
    public static class SoundEffects
    {
        public static AudioClip CreateEnemyHit()
        {
            return Create("Enemy hit", .11f, (t, length) =>
            {
                var envelope = Mathf.Clamp01(1f - t / length);
                var frequency = Mathf.Lerp(650f, 1180f, t / length);
                return Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * .28f;
            });
        }

        public static AudioClip CreateEnemyDeath()
        {
            // A continuous, zero-crossing burst with explicit attack/release.
            // The old Perlin transient could leave a click on Android mixers.
            return Create("Enemy destroyed smooth", .30f, (t, length) =>
            {
                var normalized = Mathf.Clamp01(t / length);
                var attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .014f));
                var release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((length - t) / .085f));
                var envelope = attack * release * Mathf.Pow(1f - normalized, 1.15f);
                var rumble = Mathf.Sin(t * Mathf.Lerp(330f, 72f, normalized) * Mathf.PI * 2f);
                var grit = Mathf.Sin(t * 617f * Mathf.PI * 2f) *
                           Mathf.Sin(t * 173f * Mathf.PI * 2f);
                return (rumble * .76f + grit * .24f) * envelope * .32f;
            });
        }

        public static AudioClip CreatePlayerDamage()
        {
            return Create("Shield impact", .18f, (t, length) =>
            {
                var envelope = Mathf.Pow(Mathf.Clamp01(1f - t / length), 2.2f);
                var tone = Mathf.Sin(t * 110f * Mathf.PI * 2f) * .65f;
                var noise = Mathf.PerlinNoise(t * 900f, 2.3f) * 2f - 1f;
                return (tone + noise * .45f) * envelope * .38f;
            });
        }

        public static AudioClip CreateCoopBump()
        {
            return Create("Coop ship bump", .22f, (t, length) =>
            {
                var normalized = Mathf.Clamp01(t / length);
                var envelope = Mathf.Pow(1f - normalized, 1.65f);
                var frequency = Mathf.Lerp(210f, 72f, normalized);
                var rubberTone = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                var chirp = Mathf.Sin(t * Mathf.Lerp(680f, 250f, normalized) * Mathf.PI * 2f) * .22f;
                return (rubberTone * .72f + chirp) * envelope * .38f;
            });
        }

        public static AudioClip CreateTetherOverload()
        {
            return Create("Energy tether overload", .38f, (t, length) =>
            {
                var normalized = Mathf.Clamp01(t / length);
                var envelope = Mathf.Pow(1f - normalized, 1.45f);
                var sweep = Mathf.Sin(t * Mathf.Lerp(1180f, 170f, normalized) * Mathf.PI * 2f);
                var harmonic = Mathf.Sin(t * Mathf.Lerp(310f, 760f, normalized) * Mathf.PI * 2f) * .34f;
                var noise = (Mathf.PerlinNoise(t * 2100f, 7.1f) * 2f - 1f) * .22f;
                return (sweep * .62f + harmonic + noise) * envelope * .34f;
            });
        }

        public static AudioClip CreateFriendlyRicochet()
        {
            return Create("Friendly elemental ricochet", .20f, (t, length) =>
            {
                var normalized = Mathf.Clamp01(t / length);
                var envelope = Mathf.Pow(1f - normalized, 1.7f);
                var ping = Mathf.Sin(t * Mathf.Lerp(1320f, 610f, normalized) * Mathf.PI * 2f);
                var sparkle = Mathf.Sin(t * Mathf.Lerp(2140f, 980f, normalized) * Mathf.PI * 2f) * .28f;
                return (ping * .68f + sparkle) * envelope * .30f;
            });
        }

        private static AudioClip Create(string name, float length, System.Func<float, float, float> sample)
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(length * sampleRate);
            var samples = new float[count];
            for (var i = 0; i < count; i++) samples[i] = sample(i / (float)sampleRate, length);
            if (count > 1)
            {
                samples[0] = 0f;
                samples[count - 1] = 0f;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
